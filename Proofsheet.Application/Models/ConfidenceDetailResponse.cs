// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

namespace Proofsheet.Application.Models;

public sealed record ConfidenceDetailResponse(
    string Rationale,
    int BoundedFrom,
    int BoundedTo,
    IReadOnlyList<string> SourceIds);
