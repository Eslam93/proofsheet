// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Proofsheet.Api.Models;

namespace Proofsheet.Tests.Integration.Api;

public sealed class RateLimitingTests
{
    private const string ClientHeader = "X-Test-Client";

    [Fact]
    public async Task Job_Submissions_Over_The_Hourly_Limit_Get_429_With_Retry_After()
    {
        await using var factory = new ProofsheetWebApplicationFactory(Limits(jobSubmissionsPerHour: 2));
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(client)).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(client)).StatusCode);
        using var refused = await SubmitAsync(client);

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Equal("application/problem+json", refused.Content.Headers.ContentType?.MediaType);
        Assert.True(refused.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        using var problem = JsonDocument.Parse(await refused.Content.ReadAsStringAsync());
        Assert.Equal("TooManyRequests", problem.RootElement.GetProperty("errorCode").GetString());

        // Reading is still allowed: only submissions have the hourly limit.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
    }

    [Fact]
    public async Task Requests_Over_The_Per_Minute_Limit_Get_429()
    {
        await using var factory = new ProofsheetWebApplicationFactory(Limits(requestsPerMinute: 3));
        using var client = factory.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync("/health")).StatusCode);
    }

    [Fact]
    public async Task Each_Client_Address_Has_Its_Own_Limit()
    {
        await using var factory = new ProofsheetWebApplicationFactory(Limits(jobSubmissionsPerHour: 1, clientAddressHeader: ClientHeader));
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(client, "198.51.100.1")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await SubmitAsync(client, "198.51.100.1")).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(client, "198.51.100.2")).StatusCode);
    }

    internal static Dictionary<string, string?> Limits(
        int requestsPerMinute = 1000,
        int jobSubmissionsPerHour = 1000,
        int chatRequestsPerHour = 1000,
        string clientAddressHeader = "") => new()
    {
        ["RateLimiting:Enabled"] = "true",
        ["RateLimiting:RequestsPerMinute"] = requestsPerMinute.ToString(),
        ["RateLimiting:JobSubmissionsPerHour"] = jobSubmissionsPerHour.ToString(),
        ["RateLimiting:ChatRequestsPerHour"] = chatRequestsPerHour.ToString(),
        ["RateLimiting:ClientAddressHeader"] = clientAddressHeader
    };

    private static async Task<HttpResponseMessage> SubmitAsync(HttpClient client, string? clientAddress = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/research-jobs")
        {
            Content = JsonContent.Create(new SubmitResearchJobRequest { Identifier = "NORTHWIND" })
        };
        if (clientAddress is not null)
        {
            request.Headers.Add(ClientHeader, clientAddress);
        }

        return await client.SendAsync(request);
    }
}
