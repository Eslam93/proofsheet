// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

using Proofsheet.Domain.Enums;

namespace Proofsheet.Domain.Models;

public sealed record MarketSnapshot(
    string Identifier,
    string SourceId,
    decimal PriceUsd,
    decimal OneYearReturnPercent,
    string MomentumSummary,
    string RiskSummary,
    string Title,
    DateTimeOffset TimestampUtc,
    TimestampType TimestampType,
    decimal QualityScore,
    decimal StanceScore);
