// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

using Proofsheet.Domain.Enums;

namespace Proofsheet.Application.Models;

public sealed class JobExecutionContext
{
    private readonly HashSet<string> _consumedTransientFailureKeys = new(StringComparer.OrdinalIgnoreCase);

    public required Guid JobId { get; init; }

    public required string Identifier { get; init; }

    public required string CorrelationId { get; init; }

    public required ResearchExecutionMode ExecutionMode { get; init; }

    public required ResolvedTickerInfo ResolvedTicker { get; init; }

    /// <summary>Only data filed on or before this date counts. Null means today.</summary>
    public DateOnly? AsOf { get; init; }

    public bool ConsumeTransientFailure(string key) => _consumedTransientFailureKeys.Add(key);
}
