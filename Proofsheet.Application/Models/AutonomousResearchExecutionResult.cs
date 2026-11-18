// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

namespace Proofsheet.Application.Models;

public sealed record AutonomousResearchExecutionResult(
    CompletedResearchResult Result,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<ResearchTraceEvent> Trace);
