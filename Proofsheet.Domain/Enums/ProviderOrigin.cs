// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

namespace Proofsheet.Domain.Enums;

public enum ProviderOrigin
{
    Mock,
    Live,

    /// <summary>Real data captured from the provider on an earlier date and replayed from disk.</summary>
    Recorded
}
