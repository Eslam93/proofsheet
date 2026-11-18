// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

using Proofsheet.Domain.Enums;

namespace Proofsheet.Api.Models;

public sealed class SubmitResearchJobRequest
{
    public string? Identifier { get; init; }

    public ResearchMode? ResearchMode { get; init; }
}
