// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Proofsheet.Application.Models;
using Proofsheet.Application.Services;
using Proofsheet.Domain.Enums;
using Proofsheet.Infrastructure.Clients;
using Proofsheet.Infrastructure.Options;
using Proofsheet.Infrastructure.Providers;
using Proofsheet.Tests.Unit.Chat;

namespace Proofsheet.Tests.Unit.Measurement;

internal static class Figure
{
    public const string Revenue = "revenue";
    public const string PriorRevenue = "priorRevenue";
    public const string OperatingIncome = "operatingIncome";
    public const string OperatingCashFlow = "operatingCashFlow";
    public const string CapitalExpenditure = "capitalExpenditure";
    public const string Cash = "cash";
    public const string Debt = "debt";

    public static readonly string[] All = [Revenue, PriorRevenue, OperatingIncome, OperatingCashFlow, CapitalExpenditure, Cash, Debt];
}

/// <summary>What one selection rule made of one company: a statement's raw figures in US dollars, or a refusal.</summary>
internal sealed record RuleOutcome(bool IsBuilt, string? RefusalReason, DateTimeOffset? FiscalYearEnd, IReadOnlyDictionary<string, decimal> Figures)
{
    public static RuleOutcome Built(DateTimeOffset fiscalYearEnd, IReadOnlyDictionary<string, decimal> figures) => new(true, null, fiscalYearEnd, figures);

    public static RuleOutcome Refused(string reason) => new(false, reason, null, new Dictionary<string, decimal>());
}

/// <summary>The current rule, run through the production provider exactly as a report would run it.</summary>
internal static class CurrentSelectionRule
{
    public static async Task<RuleOutcome> BuildAsync(string ticker, string cikPadded, string companyFactsJson, DateOnly asOf)
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var client = new SecEdgarClient(
            new HttpClient(new CapturedFactsHandler(ticker, cikPadded, companyFactsJson)),
            Options.Create(new SecEdgarOptions { Enabled = true, UserAgent = "Proofsheet measurement (no network)" }),
            cache,
            NullLogger<SecEdgarClient>.Instance);
        var context = new JobExecutionContextAccessor
        {
            Current = new JobExecutionContext
            {
                JobId = Guid.NewGuid(),
                Identifier = ticker,
                CorrelationId = "measurement",
                ExecutionMode = ResearchExecutionMode.LiveRequired,
                ResolvedTicker = new(ticker, ticker, null, cikPadded),
                AsOf = asOf
            }
        };
        var provider = new HybridFinancialStatementProvider(
            new MockFinancialStatementProvider(new MockResearchSeedCatalog()),
            context,
            client,
            new FakeTimeProvider(new DateTimeOffset(asOf.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)),
            NullLogger<HybridFinancialStatementProvider>.Instance);

        var result = await provider.GetAsync(ticker, CancellationToken.None);
        if (result.Value is not { } statement)
        {
            return RuleOutcome.Refused(result.ErrorMessage ?? "No statement.");
        }

        decimal Single(string label) => statement.Figures.Single(figure => figure.Label == label).Value;
        return RuleOutcome.Built(
            statement.TimestampUtc,
            new Dictionary<string, decimal>
            {
                [Figure.Revenue] = Single("Revenue"),
                [Figure.PriorRevenue] = Single("Revenue, prior year"),
                [Figure.OperatingIncome] = Single("Operating income"),
                [Figure.OperatingCashFlow] = Single("Operating cash flow"),
                [Figure.CapitalExpenditure] = Single("Capital expenditure"),
                [Figure.Cash] = Single("Cash and cash equivalents"),
                [Figure.Debt] = statement.Figures.Where(figure => figure.Label.StartsWith("Debt", StringComparison.Ordinal)).Sum(figure => figure.Value)
            });
    }

    private sealed class CapturedFactsHandler(string ticker, string cikPadded, string companyFactsJson) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!.AbsoluteUri;
            var json = uri.Contains("company_tickers_exchange.json", StringComparison.Ordinal)
                ? $$"""{"fields":["cik","name","ticker","exchange"],"data":[[{{long.Parse(cikPadded, CultureInfo.InvariantCulture)}},"{{ticker}}","{{ticker}}",null]]}"""
                : uri.Contains($"/companyfacts/CIK{cikPadded}.json", StringComparison.Ordinal)
                    ? companyFactsJson
                    : null;
            return Task.FromResult(json is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }
}

internal sealed record MeasurementList(string Name, string SelectionRule, string CapturedOn, IReadOnlyList<MeasuredCompany> Companies);

internal sealed record MeasuredCompany(string Ticker, string Cik, string Set);

/// <summary>Values read by hand from one 10-K, with where each one is printed.</summary>
internal sealed record TruthFile(
    string Ticker,
    string Form,
    string AccessionNumber,
    string FiscalYearEnd,
    decimal Scale,
    string ScaleLabel,
    string KeyedOn,
    IReadOnlyDictionary<string, TruthValue> Values);

internal sealed record TruthValue(string Statement, string Line, string Column, decimal Value, IReadOnlyList<TruthLine>? Lines = null);

internal sealed record TruthLine(string Line, decimal Value);

/// <summary>
/// Runs both selection rules over a captured list and compares each with the values read by hand from the 10-Ks.
/// The output is deterministic: no clock, no ordering by anything but ticker.
/// </summary>
internal static class SelectionMeasurement
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<string> RunAsync(string repositoryRoot, string listFile)
    {
        var list = JsonSerializer.Deserialize<MeasurementList>(File.ReadAllText(Path.Combine(repositoryRoot, listFile)), JsonOptions)!;
        var asOf = DateOnly.ParseExact(list.CapturedOn, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var factsFolder = Path.Combine(repositoryRoot, "data", "sec", list.CapturedOn);
        var truthFolder = Path.Combine(repositoryRoot, "data", "measurement", "truth");

        var rows = new List<Row>();
        foreach (var company in list.Companies.OrderBy(company => company.Ticker, StringComparer.Ordinal))
        {
            var json = File.ReadAllText(Path.Combine(factsFolder, $"companyfacts-{company.Ticker}.json"));
            using var document = JsonDocument.Parse(json);
            var truthPath = Path.Combine(truthFolder, $"{company.Ticker}.json");
            var truth = File.Exists(truthPath) ? JsonSerializer.Deserialize<TruthFile>(File.ReadAllText(truthPath), JsonOptions) : null;
            rows.Add(new Row(
                company,
                truth,
                PreFixSelectionRule.Build(document.RootElement),
                await CurrentSelectionRule.BuildAsync(company.Ticker, company.Cik, json, asOf)));
        }

        return Render(list, rows);
    }

    private static string Render(MeasurementList list, IReadOnlyList<Row> rows)
    {
        var text = new StringBuilder();
        text.AppendLine($"# Selection measurement: {list.Name}");
        text.AppendLine();
        text.AppendLine("Generated by `SelectionMeasurementTests` from the files in this folder and in `data/sec`. Do not edit by hand; CI fails when a fresh run differs from this file.");
        text.AppendLine();
        text.AppendLine($"- Companies: {rows.Count}. Selection rule: {list.SelectionRule}");
        text.AppendLine($"- Company facts captured on {list.CapturedOn}; both rules read them as of that date.");
        text.AppendLine($"- Values read by hand from the 10-K: {rows.Count(row => row.Truth is not null)} of {rows.Count} companies.");
        text.AppendLine();

        text.AppendLine("## Result");
        text.AppendLine();
        text.AppendLine("A refusal is no answer, not a correct one. A statement is fully correct only when its fiscal year is the 10-K's and all seven figures match the 10-K at the precision it prints.");
        text.AppendLine();
        text.AppendLine("| Rule | Built | Fully correct | Built with a wrong figure | Refused | Not yet checked by hand |");
        text.AppendLine("|---|---|---|---|---|---|");
        foreach (var (name, pick) in Rules)
        {
            var outcomes = rows.Select(row => (row, outcome: pick(row))).ToArray();
            var built = outcomes.Where(item => item.outcome.IsBuilt).ToArray();
            var keyed = built.Where(item => item.row.Truth is not null).ToArray();
            var correct = keyed.Count(item => Mismatches(item.outcome, item.row.Truth!).Count == 0);
            text.AppendLine($"| {name} | {built.Length} | {correct} | {keyed.Length - correct} | {outcomes.Length - built.Length} | {built.Length - keyed.Length} |");
        }

        text.AppendLine();
        text.AppendLine("## Refusals, by reason");
        text.AppendLine();
        foreach (var (name, pick) in Rules)
        {
            var refusals = rows.Where(row => !pick(row).IsBuilt).ToArray();
            text.AppendLine($"- {name}: {(refusals.Length == 0 ? "none" : string.Join("; ", refusals.Select(row => $"{row.Company.Ticker} ({pick(row).RefusalReason})")))}");
        }

        text.AppendLine();
        text.AppendLine("## By company");
        text.AppendLine();
        text.AppendLine("Figures in USD millions. A figure marked `≠` differs from the other rule; once a 10-K has been read, `✗` marks a figure that differs from the 10-K.");
        text.AppendLine();
        text.AppendLine("| Company | Set | Rule | Fiscal year end | Revenue | Prior revenue | Operating income | Operating cash flow | Capex | Cash | Debt | Against the 10-K |");
        text.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (var row in rows)
        {
            foreach (var (name, pick) in Rules)
            {
                var outcome = pick(row);
                var other = name == Rules[0].Name ? row.Current : row.PreFix;
                if (!outcome.IsBuilt)
                {
                    text.AppendLine($"| {row.Company.Ticker} | {row.Company.Set} | {name} | refused | | | | | | | | {(row.Truth is null ? "not read yet" : "no answer")} |");
                    continue;
                }

                var mismatches = row.Truth is null ? null : Mismatches(outcome, row.Truth);
                var cells = Figure.All.Select(figure =>
                {
                    var value = Millions(outcome.Figures[figure]);
                    var mark = mismatches?.Contains(figure) == true ? " ✗" : other.IsBuilt && Millions(other.Figures[figure]) != value ? " ≠" : string.Empty;
                    return $"{value.ToString("N0", CultureInfo.InvariantCulture)}{mark}";
                });
                var verdict = mismatches is null ? "not read yet" : mismatches.Count == 0 ? "fully correct" : $"wrong: {string.Join(", ", mismatches)}";
                text.AppendLine($"| {row.Company.Ticker} | {row.Company.Set} | {name} | {outcome.FiscalYearEnd:yyyy-MM-dd} | {string.Join(" | ", cells)} | {verdict} |");
            }
        }

        return text.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    // Each figure is compared at the precision the 10-K prints it; a wrong fiscal year makes every figure wrong.
    private static IReadOnlyList<string> Mismatches(RuleOutcome outcome, TruthFile truth)
    {
        var fiscalYearEnd = DateTimeOffset.ParseExact(truth.FiscalYearEnd, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
        if (outcome.FiscalYearEnd is not { } end || (end - fiscalYearEnd).Duration() > TimeSpan.FromDays(7))
        {
            return ["fiscal year", .. Figure.All];
        }

        return Figure.All
            .Where(figure => !truth.Values.TryGetValue(figure, out var value) ||
                Math.Round(outcome.Figures[figure] / truth.Scale, 0, MidpointRounding.AwayFromZero) != value.Value)
            .ToArray();
    }

    private static decimal Millions(decimal value) => Math.Round(value / 1_000_000m, 0, MidpointRounding.AwayFromZero);

    private static readonly (string Name, Func<Row, RuleOutcome> Pick)[] Rules =
    [
        ("Before the fix (3dc6852)", row => row.PreFix),
        ("Current rule", row => row.Current)
    ];

    private sealed record Row(MeasuredCompany Company, TruthFile? Truth, RuleOutcome PreFix, RuleOutcome Current);
}
