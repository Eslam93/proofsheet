// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Proofsheet.Application.Models;
using Proofsheet.Application.Services;
using Proofsheet.Domain.Enums;
using Proofsheet.Infrastructure.Clients;
using Proofsheet.Infrastructure.Options;
using Proofsheet.Infrastructure.Providers;
using Proofsheet.Tests.Unit.Chat;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Proofsheet.Tests.Unit.Providers;

// Real SEC EDGAR company facts captured on 2026-11-06 (see Fixtures/Sec/README.md).
public sealed class SecFinancialStatementFixtureTests
{
    private static readonly DateTimeOffset CapturedOn = new(2026, 11, 6, 0, 0, 0, TimeSpan.Zero);

    // Ticker, CIK, fiscal year end, revenue growth %, operating margin %, free cash flow, cash and debt (billions of USD).
    public static TheoryData<string, string, string, decimal, decimal, decimal, decimal, decimal> SupportedCompanies => new()
    {
        { "AAPL", "0000320193", "2026-09-26", 15.06m, 33.01m, 136.06m, 36.31m, 78.11m },
        { "MSFT", "0000789019", "2026-06-30", 17.79m, 46.78m, 66.99m, 20.94m, 40.29m },
        { "AMZN", "0001018724", "2025-12-31", 12.38m, 11.16m, 7.70m, 86.81m, 68.84m },
        { "COST", "0000909832", "2026-08-30", 10.14m, 3.85m, 9.38m, 20.21m, 6.16m },
        { "NVDA", "0001045810", "2026-01-25", 65.47m, 60.38m, 96.68m, 10.61m, 8.47m },
        { "AMD", "0000002488", "2025-12-27", 34.34m, 10.66m, 6.74m, 5.54m, 3.22m },
        { "GOOGL", "0001652044", "2025-12-31", 15.09m, 32.03m, 73.27m, 30.71m, 49.09m },
        { "TSLA", "0001318605", "2025-12-31", -2.93m, 4.59m, 6.22m, 16.51m, 6.58m },
        { "UNH", "0000731766", "2025-12-31", 11.81m, 4.24m, 16.08m, 24.37m, 78.39m },
        // Debt equals the filing's own total: DebtAndCapitalLeaseObligations 54.098.
        { "BA", "0000012927", "2025-12-31", 34.50m, 4.79m, -1.88m, 10.92m, 54.10m },
        // Debt is long-term 22.842 plus debt due within one year 11.296, which includes commercial paper.
        { "PG", "0000080424", "2026-06-30", 3.26m, 22.69m, 15.15m, 9.94m, 34.14m },
    };

    [Theory]
    [MemberData(nameof(SupportedCompanies))]
    public async Task Live_Statement_Reports_The_Latest_Fiscal_Year(
        string ticker,
        string cik,
        string fiscalYearEnd,
        decimal revenueGrowthPercent,
        decimal operatingMarginPercent,
        decimal freeCashFlowBillions,
        decimal cashBillions,
        decimal debtBillions)
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());

        var result = await CreateProvider(ticker, cik, cache, CapturedOn).GetAsync(ticker, CancellationToken.None);

        Assert.False(result.FallbackOccurred, result.FallbackReason);
        Assert.Equal(ProviderOrigin.Live, result.Origin);
        var statement = result.Value!;
        Assert.Equal(DateTimeOffset.Parse($"{fiscalYearEnd}T00:00:00Z"), statement.TimestampUtc);
        Assert.Equal(revenueGrowthPercent, statement.RevenueGrowthPercent);
        Assert.Equal(operatingMarginPercent, statement.OperatingMarginPercent);
        Assert.Equal(freeCashFlowBillions, statement.FreeCashFlowBillions);
        Assert.Equal(cashBillions, statement.CashBillions);
        Assert.Equal(debtBillions, statement.DebtBillions);
    }

    [Theory]
    [InlineData("JPM", "0000019617")]
    [InlineData("XOM", "0000034088")]
    [InlineData("TSM", "0001046179")]
    public async Task Live_Statement_Is_Unavailable_When_Required_Facts_Are_Missing(string ticker, string cik)
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());

        var result = await CreateProvider(ticker, cik, cache, CapturedOn).GetAsync(ticker, CancellationToken.None);

        // A real company never gets invented figures: the statement is refused, with the reason.
        Assert.Null(result.Value);
        Assert.False(result.FallbackOccurred);
        Assert.Equal(ProviderOrigin.Live, result.Origin);
        Assert.Contains("financial facts were incomplete", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Live_Statement_Is_Unavailable_When_The_Latest_Fiscal_Year_Is_Stale()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var twentyMonthsAfterFiscalYearEnd = new DateTimeOffset(2028, 5, 26, 0, 0, 0, TimeSpan.Zero);

        var result = await CreateProvider("AAPL", "0000320193", cache, twentyMonthsAfterFiscalYearEnd).GetAsync("AAPL", CancellationToken.None);

        Assert.Null(result.Value);
        Assert.Contains("the latest fiscal year ended on 2026-09-26, more than 18 months ago", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Live_Statement_Is_Unavailable_When_The_Newest_Revenue_Uses_An_Unread_Concept()
    {
        // Simulate a 10-K that tags revenue with a concept the provider does not read: drop Apple's FY2026 revenue
        // while its FY2026 operating income and cash flows stay. The statement must not fall back to FY2025.
        var companyFacts = JsonNode.Parse(ReadFixture("AAPL"))!;
        var revenue = companyFacts["facts"]!["us-gaap"]!["RevenueFromContractWithCustomerExcludingAssessedTax"]!["units"]!["USD"]!.AsArray();
        foreach (var fact in revenue.Where(fact => (string?)fact!["end"] == "2026-09-26").ToArray())
        {
            revenue.Remove(fact);
        }

        using var cache = new MemoryCache(new MemoryCacheOptions());

        var result = await CreateProvider("AAPL", "0000320193", cache, CapturedOn, companyFacts.ToJsonString()).GetAsync("AAPL", CancellationToken.None);

        Assert.Null(result.Value);
        Assert.Contains("no revenue for the fiscal year ending 2026-09-26; the newest revenue on file is for 2025-09-27", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Live_Statement_As_Of_A_Date_Uses_Only_What_Was_Filed_By_Then()
    {
        // Apple filed its FY2026 10-K on 2026-11-03. The day before, FY2025 is the latest fiscal year: revenue
        // 416,161 against 391,035 (USD millions) is 6.43% growth.
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var dayBefore = await CreateProvider("AAPL", "0000320193", cache, CapturedOn, asOf: new DateOnly(2026, 11, 2)).GetAsync("AAPL", CancellationToken.None);
        var dayOf = await CreateProvider("AAPL", "0000320193", cache, CapturedOn, asOf: new DateOnly(2026, 11, 3)).GetAsync("AAPL", CancellationToken.None);

        Assert.Equal(DateTimeOffset.Parse("2025-09-27T00:00:00Z"), dayBefore.Value!.TimestampUtc);
        Assert.Equal(6.43m, dayBefore.Value.RevenueGrowthPercent);
        Assert.Equal(DateTimeOffset.Parse("2026-09-26T00:00:00Z"), dayOf.Value!.TimestampUtc);
        Assert.Equal(15.06m, dayOf.Value.RevenueGrowthPercent);
    }

    private static string ReadFixture(string ticker) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Sec", $"companyfacts-{ticker}.json"));

    private static HybridFinancialStatementProvider CreateProvider(string ticker, string cik, IMemoryCache cache, DateTimeOffset now, string? companyFactsJson = null, DateOnly? asOf = null)
    {
        var companyFacts = companyFactsJson ?? ReadFixture(ticker);
        var secClient = new SecEdgarClient(
            new HttpClient(new FixtureHandler(ticker, cik, companyFacts)),
            Options.Create(new SecEdgarOptions
            {
                Enabled = true,
                DataBaseUrl = "https://data.sec.gov/",
                MappingUrl = "https://www.sec.gov/files/company_tickers_exchange.json",
                UserAgent = "Proofsheet-Test/1.0 test@example.com"
            }),
            cache,
            NullLogger<SecEdgarClient>.Instance);

        var context = new JobExecutionContextAccessor
        {
            Current = new JobExecutionContext
            {
                JobId = Guid.NewGuid(),
                Identifier = ticker,
                CorrelationId = Guid.NewGuid().ToString("N"),
                ExecutionMode = ResearchExecutionMode.Hybrid,
                ResolvedTicker = new(ticker, $"{ticker} Inc.", "Nasdaq", cik),
                AsOf = asOf
            }
        };

        return new HybridFinancialStatementProvider(
            new MockFinancialStatementProvider(new MockResearchSeedCatalog()),
            context,
            secClient,
            new FakeTimeProvider(now),
            NullLogger<HybridFinancialStatementProvider>.Instance);
    }

    private sealed class FixtureHandler(string ticker, string cik, string companyFactsJson) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!.AbsoluteUri;
            if (uri.Contains("company_tickers_exchange.json", StringComparison.Ordinal))
            {
                return Task.FromResult(Json($$"""{"fields":["cik","name","ticker","exchange"],"data":[[{{long.Parse(cik)}},"{{ticker}} Inc.","{{ticker}}","Nasdaq"]]}"""));
            }

            if (uri.Contains($"/companyfacts/CIK{cik}.json", StringComparison.Ordinal))
            {
                return Task.FromResult(Json(companyFactsJson));
            }

            throw new InvalidOperationException($"Unexpected URI {uri}");
        }

        private static HttpResponseMessage Json(string json) =>
            new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }
}
