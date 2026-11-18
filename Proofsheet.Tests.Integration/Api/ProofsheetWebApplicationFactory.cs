// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

using Proofsheet.Domain.Contracts;
using Proofsheet.Domain.Enums;
using Proofsheet.Domain.Models;
using Proofsheet.Infrastructure.Providers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Proofsheet.Tests.Integration.Api;

internal sealed class ProofsheetWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly Dictionary<string, string?> _configurationOverrides = new(StringComparer.OrdinalIgnoreCase);
    private readonly Action<IServiceCollection>? _configureTestServices;

    public ProofsheetWebApplicationFactory(
        IDictionary<string, string?>? configurationOverrides = null,
        Action<IServiceCollection>? configureTestServices = null)
    {
        _configurationOverrides["ResearchJobs:Mode"] = "Mock";
        _configurationOverrides["OpenAiResponses:Enabled"] = "false";
        _configurationOverrides["SecEdgar:Enabled"] = "false";
        _configurationOverrides["TwelveData:Enabled"] = "false";
        _configurationOverrides["AlphaVantage:Enabled"] = "false";
        // Blank every key and contact, so a developer's user secrets (loaded because the host runs as Development)
        // can never reach a live service from a test. A test that needs a key sets a fake one.
        _configurationOverrides["OpenAiResponses:ApiKey"] = "";
        _configurationOverrides["SecEdgar:UserAgent"] = "";
        _configurationOverrides["TwelveData:ApiKey"] = "";
        _configurationOverrides["AlphaVantage:ApiKey"] = "";
        // Tests poll quickly from one address; the limits have their own tests, which turn them back on.
        _configurationOverrides["RateLimiting:Enabled"] = "false";

        if (configurationOverrides is not null)
        {
            foreach (var pair in configurationOverrides)
            {
                _configurationOverrides[pair.Key] = pair.Value;
            }
        }

        _configureTestServices = configureTestServices;
    }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(_configurationOverrides);
        });

        if (_configureTestServices is not null)
        {
            builder.ConfigureTestServices(_configureTestServices);
        }
    }
}

internal sealed class DelayedCompanyProfileProvider : ICompanyProfileProvider
{
    private readonly MockResearchSeedCatalog _catalog;
    private readonly TimeSpan _delay;

    public DelayedCompanyProfileProvider(MockResearchSeedCatalog catalog, TimeSpan delay)
    {
        _catalog = catalog;
        _delay = delay;
    }

    public async Task<ProviderResult<CompanyProfile>> GetAsync(string identifier, CancellationToken cancellationToken)
    {
        await Task.Delay(_delay, cancellationToken);
        return _catalog.TryGet(identifier, out var seed)
            ? ProviderResult<CompanyProfile>.Success(seed.CompanyProfile, ProviderOrigin.Mock, "SeededMock")
            : ProviderResult<CompanyProfile>.Empty(origin: ProviderOrigin.Mock, providerName: "SeededMock");
    }
}

internal sealed class EmptyFinancialStatementProvider : IFinancialStatementProvider
{
    public Task<ProviderResult<FinancialStatement>> GetAsync(string identifier, CancellationToken cancellationToken) =>
        Task.FromResult(ProviderResult<FinancialStatement>.Empty(origin: ProviderOrigin.Mock, providerName: "SeededMock"));
}

/// <summary>Returns a sample company's statement with the given figures attached, as a live statement would carry them.</summary>
internal sealed class FiguredFinancialStatementProvider(MockResearchSeedCatalog catalog, params FinancialFigure[] figures) : IFinancialStatementProvider
{
    public Task<ProviderResult<FinancialStatement>> GetAsync(string identifier, CancellationToken cancellationToken) =>
        Task.FromResult(catalog.TryGet(identifier, out var seed)
            ? ProviderResult<FinancialStatement>.Success(seed.FinancialStatement with { Figures = figures }, ProviderOrigin.Live, "SEC EDGAR")
            : ProviderResult<FinancialStatement>.Empty(origin: ProviderOrigin.Live, providerName: "SEC EDGAR"));
}
