// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

namespace Proofsheet.Application.Models;

public sealed record ReportChatThreadCreatedResponse(
    string ThreadId,
    Guid JobId,
    DateTimeOffset CreatedAtUtc);
