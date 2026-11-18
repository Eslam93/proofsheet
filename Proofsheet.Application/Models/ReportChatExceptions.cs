// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

namespace Proofsheet.Application.Models;

/// <summary>
/// A report chat request that cannot be served for a reason the caller may read, such as an expired thread. The
/// message is fixed text written for the browser and never carries text from an upstream service.
/// </summary>
public sealed class ReportChatRequestException(string message) : InvalidOperationException(message);

/// <summary>
/// A report chat request that failed for a reason the caller must not read, such as an error body from the model
/// provider. The browser gets <see cref="Exception.Message"/>, a fixed sentence with <see cref="Reference"/>; the original
/// error stays in <see cref="Exception.InnerException"/> for the server log, under the same reference.
/// </summary>
public sealed class ReportChatFailedException(string reference, Exception innerException)
    : Exception(MessageFor(reference), innerException)
{
    public string Reference { get; } = reference;

    public static string NewReference() => Guid.NewGuid().ToString("N");

    public static string MessageFor(string reference) =>
        $"The chat could not answer. Reference: {reference}.";
}
