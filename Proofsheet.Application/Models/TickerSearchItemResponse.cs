// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

namespace Proofsheet.Application.Models;

public sealed record TickerSearchItemResponse(
    string Symbol,
    string CompanyName,
    string? Exchange,
    string Cik);
