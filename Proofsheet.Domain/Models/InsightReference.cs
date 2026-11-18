// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

namespace Proofsheet.Domain.Models;

public sealed record InsightReference(
    string Insight,
    IReadOnlyList<string> SourceIds);
