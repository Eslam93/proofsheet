// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

using Proofsheet.Application.Models;
using Proofsheet.Domain.Enums;

namespace Proofsheet.Application.Abstractions;

public interface IResearchJobSubmissionService
{
    /// <param name="asOf">Only data filed on or before this date counts; null means today.</param>
    Task<ResearchJobSubmissionResult> SubmitAsync(string? identifier, ResearchMode? researchMode, DateOnly? asOf, CancellationToken cancellationToken);
}
