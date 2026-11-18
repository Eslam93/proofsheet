// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

using Proofsheet.Domain.Enums;

namespace Proofsheet.Application.Models;

public sealed record ResearchJobQueueItem(
    Guid JobId,
    string Identifier,
    string CorrelationId,
    ResearchMode ResearchMode,
    ResearchExecutionMode ExecutionMode,
    ResolvedTickerInfo ResolvedTicker);
