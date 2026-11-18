// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

namespace Proofsheet.Application.Models;

public sealed record ReportChatMessageResponse(
    string ThreadId,
    Guid JobId,
    string ResponseId,
    string Provider,
    string Answer,
    IReadOnlyList<string> CitedSourceIds,
    ReportChatContextResponse ReportContext);
