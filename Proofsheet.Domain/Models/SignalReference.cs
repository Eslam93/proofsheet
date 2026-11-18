// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

using Proofsheet.Domain.Enums;

namespace Proofsheet.Domain.Models;

public sealed record SignalReference(
    InvestmentSignal Signal,
    string Rationale,
    IReadOnlyList<string> SourceIds,
    bool OverrideApplied);
