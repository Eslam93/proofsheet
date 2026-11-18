// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

using Proofsheet.Application.Abstractions;
using Proofsheet.Application.Models;
using Proofsheet.Domain.Enums;
using Proofsheet.Infrastructure.Clients;
using Proofsheet.Infrastructure.Providers;

namespace Proofsheet.Infrastructure.Services;

public sealed class TickerDirectoryService : ITickerDirectoryService
{
    private readonly SecEdgarClient _secEdgarClient;
    private readonly MockResearchSeedCatalog _seedCatalog;
    private readonly IResearchExecutionModeResolver _executionModeResolver;

    public TickerDirectoryService(SecEdgarClient secEdgarClient, MockResearchSeedCatalog seedCatalog, IResearchExecutionModeResolver executionModeResolver)
    {
        _secEdgarClient = secEdgarClient;
        _seedCatalog = seedCatalog;
        _executionModeResolver = executionModeResolver;
    }

    // Only Mock and Hybrid have sample data to give the fictional companies; elsewhere they would only fail later.
    private bool OffersSampleCompanies => _executionModeResolver.Resolve() is ResearchExecutionMode.Mock or ResearchExecutionMode.Hybrid;

    public async Task<ResolvedTickerInfo?> ResolveAsync(string identifier, CancellationToken cancellationToken = default)
    {
        var profile = await _secEdgarClient.GetTickerProfileAsync(identifier, cancellationToken);
        if (profile is not null)
        {
            return new ResolvedTickerInfo(profile.Identifier, profile.CompanyName, profile.Exchange, profile.CikPadded);
        }

        return OffersSampleCompanies && _seedCatalog.TryGet(identifier, out var seed)
            ? new ResolvedTickerInfo(seed.CompanyProfile.Identifier, seed.CompanyProfile.CompanyName, seed.CompanyProfile.Exchange, seed.CompanyProfile.CikPadded)
            : null;
    }

    public async Task<IReadOnlyList<TickerSearchItemResponse>> SearchAsync(string query, int limit, CancellationToken cancellationToken = default)
    {
        var items = await _secEdgarClient.SearchTickerProfilesAsync(query, limit, cancellationToken);
        if (items.Count > 0)
        {
            return items
                .Select(item => new TickerSearchItemResponse(item.Identifier, item.CompanyName, item.Exchange, item.CikPadded))
                .ToArray();
        }

        return OffersSampleCompanies ? SeedSearch(query, limit) : Array.Empty<TickerSearchItemResponse>();
    }

    private IReadOnlyList<TickerSearchItemResponse> SeedSearch(string query, int limit)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<TickerSearchItemResponse>();
        }

        var normalized = query.Trim();
        return _seedCatalog.Identifiers
            .Select(symbol => _seedCatalog.TryGet(symbol, out var seed)
                ? new TickerSearchItemResponse(seed.CompanyProfile.Identifier, seed.CompanyProfile.CompanyName, seed.CompanyProfile.Exchange, seed.CompanyProfile.CikPadded)
                : null)
            .Where(item => item is not null)
            .Select(item => item!)
            .Where(item =>
                item.Symbol.Contains(normalized, StringComparison.OrdinalIgnoreCase) ||
                item.CompanyName.Contains(normalized, StringComparison.OrdinalIgnoreCase))
            .Take(Math.Clamp(limit, 1, 20))
            .ToArray();
    }
}
