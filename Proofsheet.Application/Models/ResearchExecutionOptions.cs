// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

using Proofsheet.Domain.Enums;

namespace Proofsheet.Application.Models;

public sealed class ResearchExecutionOptions
{
    public const string SectionName = "ResearchJobs";

    public int QueueCapacity { get; init; } = 100;

    public int MaxConcurrentJobs { get; init; } = 2;

    public int ProviderTimeoutMs { get; init; } = 2000;

    public int RetryDelayMs { get; init; } = 250;

    public int MaxRetries { get; init; } = 1;

    public int JobTimeoutMs { get; init; } = 90000;

    public int OverloadRetryAfterSeconds { get; init; } = 5;

    public ResearchExecutionMode Mode { get; init; } = ResearchExecutionMode.Auto;

    public int TickerSearchMinimumQueryLength { get; init; } = 2;

    public int TickerSearchDefaultLimit { get; init; } = 10;

    public int TickerSearchMaxLimit { get; init; } = 20;

    /// <summary>How long a finished job stays readable, in minutes.</summary>
    public int JobRetentionMinutes { get; init; } = 1440;

    /// <summary>The most jobs kept at once. When the store is full, the oldest finished jobs are dropped first.</summary>
    public int MaxStoredJobs { get; init; } = 500;

    /// <summary>In Recorded mode, the folder of recorded SEC company facts, relative to the app's directory.</summary>
    public string RecordedSecDataPath { get; init; } = "RecordedData/Sec";

    /// <summary>
    /// The day the recorded SEC data was captured. Recorded reports are built as of this date by default, and a later
    /// date is refused because the recording holds nothing filed after it.
    /// </summary>
    public DateOnly RecordedOn { get; init; } = new(2026, 11, 6);
}
