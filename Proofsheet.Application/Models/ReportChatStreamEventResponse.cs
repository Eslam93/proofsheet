// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

namespace Proofsheet.Application.Models;

public sealed record ReportChatStreamEventResponse(
    string ThreadId,
    Guid JobId,
    string StreamId,
    long Sequence,
    string Kind,
    string ProviderEventType,
    string Delta,
    string Error,
    DateTimeOffset CreatedUtc);
