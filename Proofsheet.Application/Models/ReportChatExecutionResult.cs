// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

namespace Proofsheet.Application.Models;

public sealed record ReportChatExecutionResult(
    string ResponseId,
    string Answer,
    IReadOnlyList<string> CitedSourceIds);
