// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

using Proofsheet.Application.Models;
using Proofsheet.Domain.Enums;

namespace Proofsheet.Application.Abstractions;

public interface IResearchJobSubmissionService
{
    Task<ResearchJobSubmissionResult> SubmitAsync(string? identifier, ResearchMode? researchMode, CancellationToken cancellationToken);
}
