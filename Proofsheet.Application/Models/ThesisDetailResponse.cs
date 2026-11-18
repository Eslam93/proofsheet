// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

namespace Proofsheet.Application.Models;

public sealed record ThesisDetailResponse(
    string Summary,
    IReadOnlyList<string> SourceIds);
