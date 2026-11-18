// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

using Proofsheet.Domain.Enums;

namespace Proofsheet.Domain.Models;

public sealed record FinancialStatement(
    string Identifier,
    string SourceId,
    decimal RevenueGrowthPercent,
    decimal OperatingMarginPercent,
    decimal FreeCashFlowBillions,
    decimal CashBillions,
    decimal DebtBillions,
    string EfficiencySummary,
    string BalanceSheetSummary,
    string Title,
    DateTimeOffset TimestampUtc,
    TimestampType TimestampType,
    decimal QualityScore,
    decimal StanceScore)
{
    /// <summary>The reported figures the statement was computed from, each with its filing. Empty for sample data.</summary>
    public IReadOnlyList<FinancialFigure> Figures { get; init; } = [];
}
