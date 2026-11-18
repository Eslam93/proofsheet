// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

using System.Globalization;
using System.Text.Json;

namespace Proofsheet.Tests.Unit.Measurement;

/// <summary>
/// The fact selection Proofsheet used before commit ea4d756, kept only as the measurement's baseline. The concept lists
/// and the selection are ported unchanged from SecEdgarClient and HybridFinancialStatementProvider at commit 3dc6852,
/// quirks included: the first concept that exists wins whatever its dates, any fact from an annual form or tagged
/// fp=FY counts as a year, a missing start date reads as 0001-01-01, and a concept without USD facts falls back to its
/// first unit.
/// </summary>
internal static class PreFixSelectionRule
{
    private static readonly string[] RevenueConcepts = ["Revenues", "RevenueFromContractWithCustomerExcludingAssessedTax", "SalesRevenueNet"];
    private static readonly string[] OperatingIncomeConcepts = ["OperatingIncomeLoss"];
    private static readonly string[] CashFromOperationsConcepts = ["NetCashProvidedByUsedInOperatingActivities", "NetCashProvidedByUsedInOperatingActivitiesContinuingOperations"];
    private static readonly string[] CapitalExpenditureConcepts = ["PaymentsToAcquirePropertyPlantAndEquipment", "PropertyPlantAndEquipmentAdditions", "CapitalExpendituresIncurredButNotYetPaid"];
    private static readonly string[] CashConcepts = ["CashAndCashEquivalentsAtCarryingValue", "CashCashEquivalentsRestrictedCashAndRestrictedCashEquivalents"];
    private static readonly string[] TotalDebtConcepts = ["LongTermDebtAndCapitalLeaseObligations", "LongTermDebtAndFinanceLeaseObligations", "LongTermDebtAndCapitalLeaseObligationsIncludingCurrentMaturities", "LongTermDebt"];
    private static readonly string[] CurrentDebtConcepts = ["LongTermDebtCurrent", "ShortTermBorrowings", "LongTermDebtAndCapitalLeaseObligationsCurrent"];
    private static readonly string[] NonCurrentDebtConcepts = ["LongTermDebtNoncurrent", "LongTermDebtAndCapitalLeaseObligationsNoncurrent", "LongTermDebtAndFinanceLeaseObligationsNoncurrent"];
    private static readonly string[] AnnualForms = ["10-K", "10-K/A", "10-KT", "20-F", "20-F/A", "40-F", "40-F/A"];
    private static readonly string[] PreferredUnits = ["USD"];

    /// <summary>All concepts the old rule reads, so a capture can keep everything the baseline needs.</summary>
    public static IReadOnlyList<string> ConceptsRead { get; } =
    [
        .. RevenueConcepts, .. OperatingIncomeConcepts, .. CashFromOperationsConcepts, .. CapitalExpenditureConcepts,
        .. CashConcepts, .. TotalDebtConcepts, .. CurrentDebtConcepts, .. NonCurrentDebtConcepts
    ];

    public static RuleOutcome Build(JsonElement companyFacts)
    {
        if (!TryGetLatestTwoAnnualFacts(companyFacts, RevenueConcepts, out var latestRevenue, out var previousRevenue) ||
            !TryGetLatestAnnualFact(companyFacts, OperatingIncomeConcepts, out var latestOperatingIncome) ||
            !TryGetLatestAnnualFact(companyFacts, CashFromOperationsConcepts, out var latestOperatingCashFlow) ||
            !TryGetLatestAnnualFact(companyFacts, CapitalExpenditureConcepts, out var latestCapitalExpenditure) ||
            !TryGetLatestInstantFact(companyFacts, CashConcepts, out var latestCash))
        {
            return RuleOutcome.Refused("SEC EDGAR did not return enough financial facts.");
        }

        var hasDebt = TryGetLatestInstantFact(companyFacts, TotalDebtConcepts, out var latestDebt);
        if (!hasDebt &&
            TryGetLatestInstantFact(companyFacts, CurrentDebtConcepts, out var currentDebt) &&
            TryGetLatestInstantFact(companyFacts, NonCurrentDebtConcepts, out var nonCurrentDebt))
        {
            latestDebt = currentDebt with { Value = currentDebt.Value + nonCurrentDebt.Value };
            hasDebt = true;
        }

        if (!hasDebt || latestRevenue.Value == 0m)
        {
            return RuleOutcome.Refused("SEC EDGAR did not return enough financial facts.");
        }

        return RuleOutcome.Built(
            latestRevenue.EndDate,
            new Dictionary<string, decimal>
            {
                [Figure.Revenue] = latestRevenue.Value,
                [Figure.PriorRevenue] = previousRevenue.Value,
                [Figure.OperatingIncome] = latestOperatingIncome.Value,
                [Figure.OperatingCashFlow] = latestOperatingCashFlow.Value,
                [Figure.CapitalExpenditure] = latestCapitalExpenditure.Value,
                [Figure.Cash] = latestCash.Value,
                [Figure.Debt] = latestDebt.Value
            });
    }

    private static bool TryGetLatestAnnualFact(JsonElement root, IReadOnlyList<string> concepts, out FactPoint fact)
    {
        fact = Empty;
        var annual = FilterAnnualFacts(GetFactPoints(root, concepts))
            .OrderByDescending(item => item.EndDate)
            .ThenByDescending(item => item.FiledDate)
            .FirstOrDefault();
        if (annual is null)
        {
            return false;
        }

        fact = annual;
        return true;
    }

    private static bool TryGetLatestTwoAnnualFacts(JsonElement root, IReadOnlyList<string> concepts, out FactPoint latest, out FactPoint previous)
    {
        latest = Empty;
        previous = Empty;
        var annual = FilterAnnualFacts(GetFactPoints(root, concepts))
            .OrderByDescending(item => item.EndDate)
            .ThenByDescending(item => item.FiledDate)
            .Take(2)
            .ToArray();
        if (annual.Length < 2)
        {
            return false;
        }

        latest = annual[0];
        previous = annual[1];
        return true;
    }

    private static bool TryGetLatestInstantFact(JsonElement root, IReadOnlyList<string> concepts, out FactPoint fact)
    {
        fact = Empty;
        var facts = GetFactPoints(root, concepts)
            .Where(item => item.EndDate != default)
            .OrderByDescending(item => item.EndDate)
            .ThenByDescending(item => item.FiledDate)
            .ToArray();
        if (facts.Length == 0)
        {
            return false;
        }

        fact = facts[0];
        return true;
    }

    private static IReadOnlyList<FactPoint> GetFactPoints(JsonElement root, IReadOnlyList<string> concepts)
    {
        if (!root.TryGetProperty("facts", out var factsElement))
        {
            return [];
        }

        foreach (var taxonomyName in new[] { "us-gaap", "ifrs-full" })
        {
            if (!factsElement.TryGetProperty(taxonomyName, out var taxonomyElement))
            {
                continue;
            }

            foreach (var conceptName in concepts)
            {
                if (!taxonomyElement.TryGetProperty(conceptName, out var conceptElement) ||
                    !conceptElement.TryGetProperty("units", out var unitsElement))
                {
                    continue;
                }

                foreach (var preferredUnit in PreferredUnits)
                {
                    if (unitsElement.TryGetProperty(preferredUnit, out var unitElement))
                    {
                        return ParseFactPoints(unitElement);
                    }
                }

                foreach (var unitProperty in unitsElement.EnumerateObject())
                {
                    return ParseFactPoints(unitProperty.Value);
                }
            }
        }

        return [];
    }

    private static IReadOnlyList<FactPoint> ParseFactPoints(JsonElement unitElement)
    {
        if (unitElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var points = new List<FactPoint>();
        foreach (var item in unitElement.EnumerateArray())
        {
            if (!TryGetDecimal(item, "val", out var value))
            {
                continue;
            }

            points.Add(new FactPoint(
                value,
                TryGetString(item, "form"),
                TryGetString(item, "fp"),
                TryGetDate(item, "start"),
                TryGetDate(item, "end"),
                TryGetDate(item, "filed")));
        }

        return points;
    }

    private static IEnumerable<FactPoint> FilterAnnualFacts(IEnumerable<FactPoint> facts) =>
        facts.Where(item =>
            (!string.IsNullOrWhiteSpace(item.FiscalPeriod) && item.FiscalPeriod.Equals("FY", StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(item.Form) && AnnualForms.Contains(item.Form, StringComparer.OrdinalIgnoreCase)) ||
            (item.StartDate is not null && item.EndDate != default && (item.EndDate - item.StartDate.Value).TotalDays >= 300));

    private static bool TryGetDecimal(JsonElement element, string propertyName, out decimal value)
    {
        value = 0m;
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number => property.TryGetDecimal(out value),
            JsonValueKind.String => decimal.TryParse(property.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value),
            _ => false
        };
    }

    // As before the fix, a missing date reads as default(DateTimeOffset), never as null.
    private static DateTimeOffset TryGetDate(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == JsonValueKind.String &&
        DateTimeOffset.TryParse(property.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value)
            ? value
            : default;

    private static string? TryGetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static readonly FactPoint Empty = new(0m, null, null, null, default, default);

    private sealed record FactPoint(decimal Value, string? Form, string? FiscalPeriod, DateTimeOffset? StartDate, DateTimeOffset EndDate, DateTimeOffset FiledDate);
}
