// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

namespace Proofsheet.Domain.Models;

public sealed record ResearchScoringInput(
    CompanyProfile? CompanyProfile,
    MarketSnapshot? MarketSnapshot,
    FinancialStatement? FinancialStatement,
    NewsCoverage? NewsCoverage,
    FilingCoverage? FilingCoverage,
    int RetryCount,
    int MissingNonCriticalSourceCount,
    bool OpenAiFallbackOccurred);
