// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Proofsheet.Infrastructure.Clients;

/// <summary>SEC company facts recorded on one date: a folder of companyfacts-TICKER.json files.</summary>
public sealed class RecordedSecData
{
    private const string FilePrefix = "companyfacts-";

    private readonly Lazy<IReadOnlyList<RecordedCompany>> _companies;

    public RecordedSecData(string folder, DateOnly recordedOn)
    {
        Folder = folder;
        RecordedOn = recordedOn;
        _companies = new Lazy<IReadOnlyList<RecordedCompany>>(Load);
    }

    public string Folder { get; }

    public DateOnly RecordedOn { get; }

    public IReadOnlyList<RecordedCompany> Companies => _companies.Value;

    public RecordedCompany? FindByCik(string cikPadded) =>
        Companies.FirstOrDefault(company => company.CikPadded == cikPadded);

    // The ticker comes from the file name; the CIK and name from the recorded facts themselves.
    private IReadOnlyList<RecordedCompany> Load()
    {
        if (!Directory.Exists(Folder))
        {
            return [];
        }

        return Directory.EnumerateFiles(Folder, $"{FilePrefix}*.json")
            .Select(path =>
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                var root = document.RootElement;
                var ticker = Path.GetFileNameWithoutExtension(path)[FilePrefix.Length..].ToUpperInvariant();
                var cik = root.GetProperty("cik").GetInt64().ToString(CultureInfo.InvariantCulture).PadLeft(10, '0');
                return new RecordedCompany(ticker, root.GetProperty("entityName").GetString() ?? ticker, cik, path);
            })
            .OrderBy(company => company.Ticker, StringComparer.Ordinal)
            .ToArray();
    }
}

public sealed record RecordedCompany(string Ticker, string Name, string CikPadded, string FilePath);

/// <summary>
/// Answers the SEC client from <see cref="RecordedSecData"/>, so the real client and selection rule run on recorded
/// data. The ticker map lists the recorded companies and company facts come from their files; anything else was not
/// recorded and gets 404. No request reaches the network.
/// </summary>
public sealed partial class RecordedSecHandler(RecordedSecData data) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        if (path.EndsWith("/company_tickers_exchange.json", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(Json(TickerMap()));
        }

        var companyFacts = CompanyFactsPath().Match(path);
        if (companyFacts.Success && data.FindByCik(companyFacts.Groups["cik"].Value) is { } company)
        {
            return Task.FromResult(Json(File.ReadAllText(company.FilePath)));
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request });
    }

    private string TickerMap()
    {
        var rows = new JsonArray();
        foreach (var company in data.Companies)
        {
            rows.Add(new JsonArray(long.Parse(company.CikPadded, CultureInfo.InvariantCulture), company.Name, company.Ticker, null));
        }

        return new JsonObject
        {
            ["fields"] = new JsonArray("cik", "name", "ticker", "exchange"),
            ["data"] = rows
        }.ToJsonString();
    }

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [GeneratedRegex(@"/api/xbrl/companyfacts/CIK(?<cik>\d{10})\.json$", RegexOptions.IgnoreCase)]
    private static partial Regex CompanyFactsPath();
}
