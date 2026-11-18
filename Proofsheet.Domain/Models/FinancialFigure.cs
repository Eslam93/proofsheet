// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

namespace Proofsheet.Domain.Models;

/// <summary>
/// One reported number behind a financial statement, and where to check it: the XBRL concept, the period it covers,
/// and the filing it came from. <see cref="PeriodStart"/> is null for a balance at a date.
/// </summary>
public sealed record FinancialFigure(
    string Label,
    decimal Value,
    string Unit,
    string Concept,
    DateTimeOffset? PeriodStart,
    DateTimeOffset PeriodEnd,
    string? Form,
    string? AccessionNumber,
    DateTimeOffset FiledDate,
    string? FilingUrl);
