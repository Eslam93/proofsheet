// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

using Proofsheet.Domain.Enums;

namespace Proofsheet.Application.Models;

public sealed record ResearchJobAcceptedResponse(
    Guid JobId,
    JobStatus Status,
    DateTimeOffset CreatedAtUtc,
    string StatusUrl,
    string ResultUrl);
