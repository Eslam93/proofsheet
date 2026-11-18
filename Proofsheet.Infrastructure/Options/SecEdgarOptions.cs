// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

namespace Proofsheet.Infrastructure.Options;

public sealed class SecEdgarOptions
{
    public const string SectionName = "SecEdgar";

    public bool Enabled { get; set; }

    public string DataBaseUrl { get; init; } = "https://data.sec.gov/";

    public string MappingUrl { get; init; } = "https://www.sec.gov/files/company_tickers_exchange.json";

    public string? UserAgent { get; set; } = "Proofsheet/1.0 local@example.com";

    /// <summary>Set in Recorded mode: requests are answered from data captured on this date, never from the network.</summary>
    public DateOnly? RecordedOn { get; set; }
}
