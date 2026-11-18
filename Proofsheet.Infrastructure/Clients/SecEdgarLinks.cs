// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

namespace Proofsheet.Infrastructure.Clients;

public static class SecEdgarLinks
{
    /// <summary>
    /// The EDGAR index page of a filing, which lists its documents (the 10-K itself and its exhibits). The archive
    /// folder is the company's CIK and the accession number without dashes, whoever submitted the filing.
    /// </summary>
    public static string? FilingIndex(string cikPadded, string? accessionNumber)
    {
        if (string.IsNullOrWhiteSpace(accessionNumber))
        {
            return null;
        }

        var cik = cikPadded.TrimStart('0');
        var folder = accessionNumber.Replace("-", string.Empty, StringComparison.Ordinal);
        return $"https://www.sec.gov/Archives/edgar/data/{(cik.Length == 0 ? "0" : cik)}/{folder}/{accessionNumber}-index.htm";
    }
}
