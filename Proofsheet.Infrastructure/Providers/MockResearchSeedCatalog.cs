// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

using Proofsheet.Domain.Enums;
using Proofsheet.Domain.Models;

namespace Proofsheet.Infrastructure.Providers;

/// <summary>
/// Sample data for running Proofsheet without any keys. Every company here is fictional, and so is every figure,
/// headline and filing excerpt. There is deliberately no sample data for real companies: a report about a listed
/// company must come from its filings or from nothing.
/// </summary>
/// <remarks>
/// The identifiers are longer than any US ticker, so none can collide with a listed company. The figures repeat the
/// shapes the pipeline needs: a strong company, two ordinary ones, and one whose market data fails once and whose news
/// is thin, which exercises retries and the low-news branch.
/// </remarks>
public sealed class MockResearchSeedCatalog
{
    public const string ProviderName = "SeededMock";

    /// <summary>The sample company the home page offers first: complete data and no failures.</summary>
    public const string FeaturedIdentifier = "NORTHWIND";

    /// <summary>The identifier whose market snapshot fails once per job and whose news coverage is thin.</summary>
    public const string DegradedIdentifier = "CONTOSO";

    private static readonly IReadOnlyDictionary<string, MockResearchSeed> Seeds = BuildSeeds();

    private static readonly string[] SampleIdentifiers = [.. Seeds.Keys.Order(StringComparer.Ordinal)];

    public IReadOnlyCollection<string> Identifiers => SampleIdentifiers;

    public bool TryGet(string identifier, out MockResearchSeed seed) =>
        Seeds.TryGetValue(identifier, out seed!);

    public static ProviderResult<T> Sample<T>(T value) =>
        ProviderResult<T>.Success(value, ProviderOrigin.Mock, ProviderName);

    /// <summary>The answer for any company without sample data, which includes every real company.</summary>
    public static ProviderResult<T> NoSample<T>(string identifier) =>
        ProviderResult<T>.Empty(
            errorMessage: $"There is no sample data for {identifier}. Sample data covers only the fictional companies {string.Join(", ", SampleIdentifiers)}.",
            origin: ProviderOrigin.Mock,
            providerName: ProviderName);

    private static IReadOnlyDictionary<string, MockResearchSeed> BuildSeeds() =>
        new Dictionary<string, MockResearchSeed>(StringComparer.OrdinalIgnoreCase)
        {
            ["NORTHWIND"] = new MockResearchSeed(
                Profile("NORTHWIND", "Northwind Instruments (fictional)", 0.90m),
                new MarketSnapshot("NORTHWIND", "market:NORTHWIND", 221.18m, 18.6m, "The share price has held its premium multiple on steady instrument demand.", "Valuation remains sensitive to the next product cycle.", "Northwind Instruments market snapshot (sample data)", Utc(2026, 3, 15), TimestampType.AsOf, 0.82m, 0.50m),
                new FinancialStatement("NORTHWIND", "financials:NORTHWIND", 6.4m, 30.8m, 102.4m, 67.3m, 11.2m, "high margins and recurring service revenue support resilient cash generation", "net cash and buyback capacity support downside resilience", "Northwind Instruments financial statements (sample data)", Utc(2026, 1, 31), TimestampType.AsOf, 0.92m, 1.00m),
                new NewsCoverage("NORTHWIND", "Northwind Instruments news coverage (sample data)", Utc(2026, 3, 14), TimestampType.Published, 0.68m, 0.50m,
                [
                    new NewsItem("news:NORTHWIND:001", "Service revenue keeps growing faster than hardware", "Sample headline: subscriptions are a growing share of revenue.", null, Utc(2026, 3, 14), TimestampType.Published, 0.70m, 0.50m),
                    new NewsItem("news:NORTHWIND:002", "Supply chain stays stable into the next launch", "Sample headline: supply constraints look manageable.", null, Utc(2026, 3, 10), TimestampType.Published, 0.66m, 0.50m)
                ]),
                new FilingCoverage("NORTHWIND", "Northwind Instruments filing coverage (sample data)", Utc(2026, 2, 2), TimestampType.Published, 0.85m, 0.50m,
                [
                    new FilingExcerpt("filing:NORTHWIND:001", "Northwind Instruments quarterly report excerpt (sample data)", "Sample excerpt: management reiterated disciplined capital returns.", null, Utc(2026, 2, 2), TimestampType.Published, 0.85m, 0.50m)
                ])),

            ["FABRIKAM"] = new MockResearchSeed(
                Profile("FABRIKAM", "Fabrikam Software (fictional)", 0.91m),
                new MarketSnapshot("FABRIKAM", "market:FABRIKAM", 446.72m, 21.3m, "Market leadership remains tied to cloud execution.", "Multiple expansion leaves little room for slower growth.", "Fabrikam Software market snapshot (sample data)", Utc(2026, 3, 15), TimestampType.AsOf, 0.80m, 0.50m),
                new FinancialStatement("FABRIKAM", "financials:FABRIKAM", 11.2m, 42.1m, 79.4m, 81.7m, 49.1m, "software mix and cloud scale continue to drive operating leverage", "the balance sheet gives room to invest without stressing returns", "Fabrikam Software financial statements (sample data)", Utc(2026, 1, 31), TimestampType.AsOf, 0.90m, 1.00m),
                new NewsCoverage("FABRIKAM", "Fabrikam Software news coverage (sample data)", Utc(2026, 3, 13), TimestampType.Published, 0.70m, 0.50m,
                [
                    new NewsItem("news:FABRIKAM:001", "Cloud demand stays durable across enterprise workloads", "Sample headline: enterprise cloud use keeps rising.", null, Utc(2026, 3, 13), TimestampType.Published, 0.71m, 0.50m),
                    new NewsItem("news:FABRIKAM:002", "New assistant features reach more business workflows", "Sample headline: adoption widens across existing customers.", null, Utc(2026, 3, 11), TimestampType.Published, 0.69m, 0.50m)
                ]),
                new FilingCoverage("FABRIKAM", "Fabrikam Software filing coverage (sample data)", Utc(2026, 2, 1), TimestampType.Published, 0.86m, 0.50m,
                [
                    new FilingExcerpt("filing:FABRIKAM:001", "Fabrikam Software quarterly report excerpt (sample data)", "Sample excerpt: infrastructure investment continues alongside cost discipline.", null, Utc(2026, 2, 1), TimestampType.Published, 0.86m, 0.50m)
                ])),

            ["TAILSPIN"] = new MockResearchSeed(
                Profile("TAILSPIN", "Tailspin Semiconductors (fictional)", 0.88m),
                new MarketSnapshot("TAILSPIN", "market:TAILSPIN", 1173.54m, 46.8m, "Momentum reflects sustained data-center demand.", "Valuation sensitivity rises if large customers slow their orders.", "Tailspin Semiconductors market snapshot (sample data)", Utc(2026, 3, 15), TimestampType.AsOf, 0.84m, 1.00m),
                new FinancialStatement("TAILSPIN", "financials:TAILSPIN", 38.4m, 55.7m, 46.3m, 28.5m, 9.8m, "data-center growth and high gross margins continue to compound cash generation", "net cash and capex flexibility support a rapid product cadence", "Tailspin Semiconductors financial statements (sample data)", Utc(2026, 1, 31), TimestampType.AsOf, 0.89m, 1.00m),
                new NewsCoverage("TAILSPIN", "Tailspin Semiconductors news coverage (sample data)", Utc(2026, 3, 12), TimestampType.Published, 0.74m, 0.50m,
                [
                    new NewsItem("news:TAILSPIN:001", "Accelerator orders stay elevated", "Sample headline: suppliers see no sudden pause in orders.", null, Utc(2026, 3, 12), TimestampType.Published, 0.76m, 0.50m),
                    new NewsItem("news:TAILSPIN:002", "New platform roadmap widens enterprise adoption", "Sample headline: use cases spread beyond the largest customers.", null, Utc(2026, 3, 9), TimestampType.Published, 0.72m, 0.50m)
                ]),
                new FilingCoverage("TAILSPIN", "Tailspin Semiconductors filing coverage (sample data)", Utc(2026, 2, 3), TimestampType.Published, 0.81m, 0.50m,
                [
                    new FilingExcerpt("filing:TAILSPIN:001", "Tailspin Semiconductors quarterly report excerpt (sample data)", "Sample excerpt: management described customer concentration risk.", null, Utc(2026, 2, 3), TimestampType.Published, 0.81m, 0.50m)
                ])),

            [DegradedIdentifier] = new MockResearchSeed(
                Profile(DegradedIdentifier, "Contoso Motors (fictional)", 0.80m),
                new MarketSnapshot(DegradedIdentifier, $"market:{DegradedIdentifier}", 181.44m, -12.1m, "The market is weighing long-term optionality against softer near-term sentiment.", "Volatility stays high because execution and margin expectations shift quickly.", "Contoso Motors market snapshot (sample data)", Utc(2026, 3, 15), TimestampType.AsOf, 0.72m, -0.50m),
                new FinancialStatement(DegradedIdentifier, $"financials:{DegradedIdentifier}", 1.8m, 11.4m, 8.2m, 25.6m, 6.1m, "cash generation remains positive but less convincing than the strongest peers", "liquidity supports continued investment, but margin compression limits enthusiasm", "Contoso Motors financial statements (sample data)", Utc(2026, 1, 31), TimestampType.AsOf, 0.62m, 0.00m),
                new NewsCoverage(DegradedIdentifier, "Contoso Motors news coverage (sample data)", Utc(2026, 3, 12), TimestampType.Published, 0.28m, -0.50m,
                [
                    new NewsItem($"news:{DegradedIdentifier}:001", "Price cuts cloud the near-term delivery outlook", "Sample headline: margin pressure and little new evidence.", null, Utc(2026, 3, 12), TimestampType.Published, 0.28m, -0.50m)
                ]),
                new FilingCoverage(DegradedIdentifier, "Contoso Motors filing coverage (sample data)", Utc(2026, 2, 5), TimestampType.Published, 0.74m, 0.00m,
                [
                    new FilingExcerpt($"filing:{DegradedIdentifier}:001", "Contoso Motors quarterly report excerpt (sample data)", "Sample excerpt: the filing highlighted demand sensitivity and execution risk.", null, Utc(2026, 2, 5), TimestampType.Published, 0.74m, 0.00m)
                ]))
        };

    private static CompanyProfile Profile(string identifier, string companyName, decimal qualityScore) =>
        new(identifier, $"profile:{identifier}", companyName, "Fictional", "n/a", $"{companyName} company profile (sample data)", Utc(2026, 3, 1), TimestampType.Captured, qualityScore, 0.50m);

    private static DateTimeOffset Utc(int year, int month, int day) =>
        new(year, month, day, 0, 0, 0, TimeSpan.Zero);
}

public sealed record MockResearchSeed(
    CompanyProfile CompanyProfile,
    MarketSnapshot MarketSnapshot,
    FinancialStatement FinancialStatement,
    NewsCoverage NewsCoverage,
    FilingCoverage FilingCoverage);
