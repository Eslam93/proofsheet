// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

namespace Proofsheet.Application.Models;

public sealed record EvidenceGapReview(
    bool ReadyForHandoff,
    IReadOnlyList<string> MissingRequiredDomains,
    IReadOnlyList<string> MissingAttemptDomains,
    IReadOnlyList<string> Concerns,
    string Summary);
