using Proofsheet.Application.Abstractions;
using Proofsheet.Application.Models;
using Proofsheet.Application.Services;
using Proofsheet.Domain.Contracts;
using Proofsheet.Domain.Enums;
using Proofsheet.Domain.Models;
using Proofsheet.Infrastructure.Clients;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Proofsheet.Infrastructure.Providers;

public sealed class HybridCompanyProfileProvider : ICompanyProfileProvider
{
    private readonly MockCompanyProfileProvider _mockProvider;
    private readonly IJobExecutionContextAccessor _jobExecutionContextAccessor;
    private readonly SecEdgarClient _secEdgarClient;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<HybridCompanyProfileProvider> _logger;

    [ActivatorUtilitiesConstructor]
    public HybridCompanyProfileProvider(
        MockCompanyProfileProvider mockProvider,
        IJobExecutionContextAccessor jobExecutionContextAccessor,
        SecEdgarClient secEdgarClient,
        TimeProvider timeProvider,
        ILogger<HybridCompanyProfileProvider> logger)
    {
        _mockProvider = mockProvider;
        _jobExecutionContextAccessor = jobExecutionContextAccessor;
        _secEdgarClient = secEdgarClient;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public HybridCompanyProfileProvider(
        MockCompanyProfileProvider mockProvider,
        SecEdgarClient secEdgarClient,
        TimeProvider timeProvider,
        ILogger<HybridCompanyProfileProvider> logger)
        : this(mockProvider, CreateHybridContextAccessor(), secEdgarClient, timeProvider, logger)
    {
    }

    public async Task<ProviderResult<CompanyProfile>> GetAsync(string identifier, CancellationToken cancellationToken)
    {
        var mode = _jobExecutionContextAccessor.Current?.ExecutionMode ?? ResearchExecutionMode.Mock;
        var fallback = await _mockProvider.GetAsync(identifier, cancellationToken);
        if (mode == ResearchExecutionMode.Mock)
        {
            return fallback;
        }

        if (!_secEdgarClient.Enabled)
        {
            return mode == ResearchExecutionMode.LiveRequired
                ? ProviderResult<CompanyProfile>.TransientFailure("SEC EDGAR company profile provider is disabled.", false, ProviderOrigin.Live, "SEC EDGAR")
                : HybridFallback.WhenDisabled(fallback, "SEC EDGAR company profile provider is disabled.", "SEC EDGAR");
        }

        if (!_secEdgarClient.IsConfigured)
        {
            return mode == ResearchExecutionMode.LiveRequired
                ? ProviderResult<CompanyProfile>.TransientFailure("SEC EDGAR was enabled but no valid User-Agent was configured.", false, ProviderOrigin.Live, "SEC EDGAR")
                : HybridFallback.FromSample(fallback, "SEC EDGAR was enabled but no valid User-Agent was configured.", "SEC EDGAR");
        }

        try
        {
            var tickerProfile = await _secEdgarClient.GetTickerProfileAsync(identifier, cancellationToken);
            var submissions = await _secEdgarClient.GetSubmissionsAsync(identifier, cancellationToken);
            if (tickerProfile is null && submissions is null)
            {
                return mode == ResearchExecutionMode.LiveRequired
                    ? ProviderResult<CompanyProfile>.Empty(errorMessage: "SEC EDGAR did not return company profile data.", origin: ProviderOrigin.Live, providerName: "SEC EDGAR")
                    : HybridFallback.FromSample(fallback, "SEC EDGAR did not return company profile data, so the seeded mock profile was used.", "SEC EDGAR");
            }

            var companyName = submissions?.CompanyName ?? tickerProfile?.CompanyName ?? identifier;
            var exchange = submissions?.Exchanges.FirstOrDefault() ?? tickerProfile?.Exchange ?? _jobExecutionContextAccessor.Current?.ResolvedTicker.Exchange;
            var cikPadded = tickerProfile?.CikPadded ?? submissions?.CikPadded ?? _jobExecutionContextAccessor.Current?.ResolvedTicker.CikPadded ?? string.Empty;

            return ProviderResult<CompanyProfile>.Success(
                new CompanyProfile(
                    identifier,
                    $"profile:{identifier}",
                    companyName,
                    exchange,
                    cikPadded,
                    $"{companyName} company profile",
                    _timeProvider.GetUtcNow(),
                    TimestampType.Captured,
                    0.88m,
                    0.15m),
                ProviderOrigin.Live,
                "SEC EDGAR");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falling back to mock company profile for {Identifier}.", identifier);
            return mode == ResearchExecutionMode.LiveRequired
                ? ProviderResult<CompanyProfile>.TransientFailure("SEC EDGAR company profile retrieval failed.", false, ProviderOrigin.Live, "SEC EDGAR")
                : HybridFallback.FromSample(fallback, "SEC EDGAR company profile retrieval failed, so the seeded mock profile was used.", "SEC EDGAR");
        }
    }

    private static IJobExecutionContextAccessor CreateHybridContextAccessor()
    {
        var accessor = new JobExecutionContextAccessor();
        accessor.Current = new JobExecutionContext
        {
            JobId = Guid.NewGuid(),
            Identifier = "TEST",
            CorrelationId = Guid.NewGuid().ToString("N"),
            ExecutionMode = ResearchExecutionMode.Hybrid,
            ResolvedTicker = new("TEST", "Test Company", "NASDAQ", "0000000000")
        };

        return accessor;
    }
}
