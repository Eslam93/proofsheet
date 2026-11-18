// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

namespace Proofsheet.Application.Models;

public sealed record ResolvedTickerInfo(
    string Symbol,
    string CompanyName,
    string? Exchange,
    string CikPadded);
