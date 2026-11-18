// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

using System.Globalization;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Proofsheet.Application.Models;

namespace Proofsheet.Api.RateLimiting;

/// <summary>
/// Per-address limits for the HTTP API and for the report chat hub. The hub needs its own check because messages on
/// an open WebSocket do not pass through the HTTP rate-limiting middleware.
/// </summary>
public static class ClientRateLimits
{
    private const string JobsPath = "/api/v1/research-jobs";

    public static IServiceCollection AddClientRateLimits(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RateLimitingOptions>(configuration.GetSection(RateLimitingOptions.SectionName));
        services.AddSingleton<ReportChatRateLimiter>();
        services.AddRateLimiter(limiterOptions =>
        {
            limiterOptions.OnRejected = WriteRejectionAsync;
        });
        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<RateLimitingOptions>>((limiterOptions, options) =>
            {
                var limits = options.Value;
                if (!limits.Enabled)
                {
                    return;
                }

                limiterOptions.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                    PartitionedRateLimiter.Create<HttpContext, string>(context =>
                        RateLimitPartition.GetFixedWindowLimiter(
                            GetClientAddress(context, limits),
                            _ => FixedWindow(limits.RequestsPerMinute, TimeSpan.FromMinutes(1)))),
                    PartitionedRateLimiter.Create<HttpContext, string>(context =>
                        IsJobSubmission(context.Request)
                            ? RateLimitPartition.GetFixedWindowLimiter(
                                GetClientAddress(context, limits),
                                _ => FixedWindow(limits.JobSubmissionsPerHour, TimeSpan.FromHours(1)))
                            : RateLimitPartition.GetNoLimiter(string.Empty)));
            });

        return services;
    }

    public static string GetClientAddress(HttpContext? context, RateLimitingOptions options)
    {
        if (context is null)
        {
            return "unknown";
        }

        if (!string.IsNullOrWhiteSpace(options.ClientAddressHeader) &&
            context.Request.Headers.TryGetValue(options.ClientAddressHeader, out var forwarded) &&
            !string.IsNullOrWhiteSpace(forwarded.ToString()))
        {
            // A proxy chain lists the original client first.
            return forwarded.ToString().Split(',')[0].Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    internal static FixedWindowRateLimiterOptions FixedWindow(int permitLimit, TimeSpan window) => new()
    {
        PermitLimit = Math.Max(permitLimit, 1),
        Window = window,
        QueueLimit = 0,
        AutoReplenishment = true
    };

    private static bool IsJobSubmission(HttpRequest request) =>
        HttpMethods.IsPost(request.Method) &&
        request.Path.Equals(JobsPath, StringComparison.OrdinalIgnoreCase);

    private static async ValueTask WriteRejectionAsync(OnRejectedContext rejected, CancellationToken cancellationToken)
    {
        var context = rejected.HttpContext;
        var retryAfterSeconds = rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? (int)Math.Ceiling(retryAfter.TotalSeconds)
            : 60;
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
        context.Response.ContentType = "application/problem+json";

        var problem = ProblemDetailsFactoryExtensions.BuildProblemDetails(
            context,
            new ProblemDescriptor(
                StatusCodes.Status429TooManyRequests,
                "/problems/too-many-requests",
                "Too many requests.",
                $"Too many requests from this address. Try again in {retryAfterSeconds} seconds.",
                "TooManyRequests"));
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem), cancellationToken);
    }
}

/// <summary>Counts chat threads opened and chat messages sent per client address.</summary>
public sealed class ReportChatRateLimiter : IDisposable
{
    private readonly RateLimitingOptions _options;
    private readonly PartitionedRateLimiter<string> _limiter;

    public ReportChatRateLimiter(IOptions<RateLimitingOptions> options)
    {
        _options = options.Value;
        _limiter = PartitionedRateLimiter.Create<string, string>(address =>
            RateLimitPartition.GetFixedWindowLimiter(
                address,
                _ => ClientRateLimits.FixedWindow(_options.ChatRequestsPerHour, TimeSpan.FromHours(1))));
    }

    /// <summary>Takes one permit for the caller's address, or throws a request error the browser may read.</summary>
    public void Acquire(HttpContext? context)
    {
        if (!_options.Enabled)
        {
            return;
        }

        using var lease = _limiter.AttemptAcquire(ClientRateLimits.GetClientAddress(context, _options));
        if (!lease.IsAcquired)
        {
            throw new ReportChatRequestException("Too many chat requests from this address. Try again later.");
        }
    }

    public void Dispose() => _limiter.Dispose();
}
