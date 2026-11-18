// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

namespace Proofsheet.Api.RateLimiting;

/// <summary>Per-address request limits. Every limit counts requests from one client address in a fixed window.</summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public bool Enabled { get; init; } = true;

    /// <summary>Requests of any kind except static files, per address per minute. Polling a running job uses 30.</summary>
    public int RequestsPerMinute { get; init; } = 120;

    /// <summary>Research jobs submitted per address per hour. Each job can call the data providers and a model.</summary>
    public int JobSubmissionsPerHour { get; init; } = 20;

    /// <summary>Chat threads opened plus chat messages sent, per address per hour. Each message is a model call.</summary>
    public int ChatRequestsPerHour { get; init; } = 30;

    /// <summary>
    /// A request header that carries the client's address, such as <c>CF-Connecting-IP</c>, set by a reverse proxy in
    /// front of the app. Leave it empty unless the app can be reached only through that proxy: anyone who can reach
    /// the app directly can put any address in the header. When empty, the connection's remote address is used.
    /// </summary>
    public string ClientAddressHeader { get; init; } = string.Empty;
}
