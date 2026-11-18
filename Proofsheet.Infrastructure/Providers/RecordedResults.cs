// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

using Proofsheet.Domain.Enums;
using Proofsheet.Domain.Models;

namespace Proofsheet.Infrastructure.Providers;

/// <summary>Provider results in Recorded mode.</summary>
internal static class RecordedResults
{
    /// <summary>A domain the recording does not hold. The report says so instead of filling the gap.</summary>
    public static ProviderResult<T> NotRecorded<T>(string what, string providerName) =>
        ProviderResult<T>.Empty(
            errorMessage: $"{what} was not recorded, so this report has none.",
            origin: ProviderOrigin.Recorded,
            providerName: providerName);

    /// <summary>
    /// A result the real client and selection rule produced from recorded data. It is a live result in every respect
    /// except where the data came from, which the origin and provider name say, with the recording's date.
    /// </summary>
    public static ProviderResult<T> AsRecorded<T>(ProviderResult<T> result, DateOnly recordedOn) =>
        new()
        {
            Status = result.Status,
            Origin = ProviderOrigin.Recorded,
            ProviderName = $"{result.ProviderName}, recorded {recordedOn:yyyy-MM-dd}",
            Value = result.Value,
            ErrorMessage = result.ErrorMessage,
            IsRetryable = false
        };
}
