// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

namespace Proofsheet.Application.Models;

public sealed record ReportChatStreamResumeResponse(
    string ThreadId,
    string StreamId,
    bool InProgress,
    long LatestSequence,
    IReadOnlyList<ReportChatStreamEventResponse> Events);
