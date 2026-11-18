// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

using Proofsheet.Domain.Models;

namespace Proofsheet.Application.Models;

/// <summary>A reported figure in a completed report, with the XBRL concept, the period and the filing to check it in.</summary>
public sealed record FinancialFigureResponse(
    string Label,
    decimal Value,
    string Unit,
    string Concept,
    DateTimeOffset? PeriodStart,
    DateTimeOffset PeriodEnd,
    string? Form,
    string? AccessionNumber,
    DateTimeOffset FiledDate,
    string? FilingUrl)
{
    public static IReadOnlyList<FinancialFigureResponse> From(FinancialStatement statement) =>
        statement.Figures
            .Select(figure => new FinancialFigureResponse(
                figure.Label,
                figure.Value,
                figure.Unit,
                figure.Concept,
                figure.PeriodStart,
                figure.PeriodEnd,
                figure.Form,
                figure.AccessionNumber,
                figure.FiledDate,
                figure.FilingUrl))
            .ToArray();
}
