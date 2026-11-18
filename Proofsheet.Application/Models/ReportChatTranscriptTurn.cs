// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

namespace Proofsheet.Application.Models;

public sealed record ReportChatTranscriptTurn(
    string UserMessage,
    string AssistantAnswer,
    IReadOnlyList<string> CitedSourceIds);
