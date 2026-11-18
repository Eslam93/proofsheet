// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

namespace Proofsheet.Domain.Enums;

public enum ResearchExecutionMode
{
    Auto,
    Mock,
    Hybrid,
    LiveRequired,

    /// <summary>
    /// SEC data recorded on an earlier date, replayed through the real client and selection rule with no network
    /// calls and no keys. Market data and news were not recorded, and a template writes the report.
    /// </summary>
    Recorded
}
