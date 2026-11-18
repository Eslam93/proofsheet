// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

using Proofsheet.Application.Abstractions;
using Proofsheet.Domain.Contracts;
using Proofsheet.Domain.Enums;
using Proofsheet.Domain.Models;

namespace Proofsheet.Infrastructure.Providers;

public sealed class MockMarketSnapshotProvider : IMarketSnapshotProvider
{
    private readonly MockResearchSeedCatalog _catalog;
    private readonly IJobExecutionContextAccessor _jobExecutionContextAccessor;

    public MockMarketSnapshotProvider(MockResearchSeedCatalog catalog, IJobExecutionContextAccessor jobExecutionContextAccessor)
    {
        _catalog = catalog;
        _jobExecutionContextAccessor = jobExecutionContextAccessor;
    }

    public Task<ProviderResult<MarketSnapshot>> GetAsync(string identifier, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_catalog.TryGet(identifier, out var seed))
        {
            return Task.FromResult(MockResearchSeedCatalog.NoSample<MarketSnapshot>(identifier));
        }

        // The degraded sample company's market data fails once per job, so the retry path runs without a live provider.
        if (identifier.Equals(MockResearchSeedCatalog.DegradedIdentifier, StringComparison.OrdinalIgnoreCase) &&
            _jobExecutionContextAccessor.Current?.ConsumeTransientFailure($"market:{MockResearchSeedCatalog.DegradedIdentifier}") == true)
        {
            return Task.FromResult(ProviderResult<MarketSnapshot>.TransientFailure("Market snapshot timed out.", true, ProviderOrigin.Mock, MockResearchSeedCatalog.ProviderName));
        }

        return Task.FromResult(MockResearchSeedCatalog.Sample(seed.MarketSnapshot));
    }
}
