// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

namespace Proofsheet.Application.Models;

public sealed record IdentifierValidationResult(
    bool IsValid,
    string? NormalizedIdentifier,
    ProblemDescriptor? Problem)
{
    public static IdentifierValidationResult Success(string normalizedIdentifier) =>
        new(true, normalizedIdentifier, null);

    public static IdentifierValidationResult Failure(ProblemDescriptor problem) =>
        new(false, null, problem);
}
