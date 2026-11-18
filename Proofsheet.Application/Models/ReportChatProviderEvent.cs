// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

namespace Proofsheet.Application.Models;

public sealed record ReportChatProviderEvent(
    string Type,
    string ResponseId,
    string TextDelta,
    string PayloadJson);
