// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

using Proofsheet.Application.Models;

namespace Proofsheet.Application.Abstractions;

public interface ITickerDirectoryService
{
    Task<ResolvedTickerInfo?> ResolveAsync(string identifier, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TickerSearchItemResponse>> SearchAsync(string query, int limit, CancellationToken cancellationToken = default);
}
