// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

namespace Proofsheet.Application.Models;

public sealed class ReportChatStreamingCallbacks
{
    public Func<ReportChatStreamEventResponse, Task>? OnStreamEvent { get; init; }

    public Func<ReportChatMessageResponse, Task>? OnChatMessage { get; init; }
}
