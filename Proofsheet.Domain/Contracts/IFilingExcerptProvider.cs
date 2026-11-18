// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

using Proofsheet.Domain.Models;

namespace Proofsheet.Domain.Contracts;

public interface IFilingExcerptProvider
{
    Task<ProviderResult<FilingCoverage>> GetAsync(string identifier, CancellationToken cancellationToken);
}
