// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

using Proofsheet.Domain.Models;

namespace Proofsheet.Domain.Contracts;

public interface IMarketSnapshotProvider
{
    Task<ProviderResult<MarketSnapshot>> GetAsync(string identifier, CancellationToken cancellationToken);
}
