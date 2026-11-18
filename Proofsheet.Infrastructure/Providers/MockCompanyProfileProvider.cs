// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

using Proofsheet.Domain.Contracts;
using Proofsheet.Domain.Models;

namespace Proofsheet.Infrastructure.Providers;

public sealed class MockCompanyProfileProvider : ICompanyProfileProvider
{
    private readonly MockResearchSeedCatalog _catalog;

    public MockCompanyProfileProvider(MockResearchSeedCatalog catalog)
    {
        _catalog = catalog;
    }

    public Task<ProviderResult<CompanyProfile>> GetAsync(string identifier, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_catalog.TryGet(identifier, out var seed)
            ? MockResearchSeedCatalog.Sample(seed.CompanyProfile)
            : MockResearchSeedCatalog.NoSample<CompanyProfile>(identifier));
    }
}
