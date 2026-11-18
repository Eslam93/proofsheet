// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

using Proofsheet.Domain.Models;

namespace Proofsheet.Domain.Contracts;

public interface ICompanyProfileProvider
{
    Task<ProviderResult<CompanyProfile>> GetAsync(string identifier, CancellationToken cancellationToken);
}
