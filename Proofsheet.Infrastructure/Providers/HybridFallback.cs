// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

using System.Text.RegularExpressions;
using Proofsheet.Domain.Enums;
using Proofsheet.Domain.Models;

namespace Proofsheet.Infrastructure.Providers;

/// <summary>
/// What a hybrid provider returns when its live source is off or fails. Sample data exists only for the fictional
/// sample companies, so only they fall back to it. A real company never gets invented figures: it gets the failure,
/// and the report leaves that part out or the job fails, as it would in LiveRequired mode.
/// </summary>
internal static partial class HybridFallback
{
    /// <summary>The live source failed. <paramref name="reason"/> names the failure and the sample that replaces it.</summary>
    public static ProviderResult<T> FromSample<T>(ProviderResult<T> sample, string reason, string liveProviderName) =>
        sample.Value is not null
            ? ProviderResult<T>.Success(sample.Value, sample.Origin, sample.ProviderName, fallbackOccurred: true, fallbackReason: reason)
            : ProviderResult<T>.Empty(errorMessage: SampleClause().Replace(reason, "."), origin: ProviderOrigin.Live, providerName: liveProviderName);

    /// <summary>The live source is switched off. Sample companies keep their sample data; any other company gets none.</summary>
    public static ProviderResult<T> WhenDisabled<T>(ProviderResult<T> sample, string disabledMessage, string liveProviderName) =>
        sample.Value is not null
            ? sample
            : ProviderResult<T>.Empty(errorMessage: disabledMessage, origin: ProviderOrigin.Live, providerName: liveProviderName);

    // Reasons read "<failure>, so the seeded mock <data> was used." Without a sample, only the failure is true.
    [GeneratedRegex(", so the seeded mock [a-z ]+ (?:was|were) used\\.$")]
    private static partial Regex SampleClause();
}
