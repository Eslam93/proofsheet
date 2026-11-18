// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Proofsheet.Api.Models;
using Proofsheet.Application.Models;
using Proofsheet.Domain.Enums;

namespace Proofsheet.Tests.Integration.Api;

// Recorded mode replays the SEC company facts recorded on 2026-11-06 (data/sec/2026-11-06) through the real SEC client
// and selection rule. Nothing here reaches the network: the factory blanks every key and contact.
public sealed class RecordedModeTests
{
    private static readonly JsonSerializerOptions JsonOptions = BuildJsonOptions();

    [Fact]
    public async Task Recorded_Report_Replays_The_Real_Selection_With_Every_Figure_Linked()
    {
        await using var factory = new ProofsheetWebApplicationFactory(Recorded());
        using var client = factory.CreateClient();

        var accepted = await SubmitAsync(client, "AAPL");
        var result = await WaitForCompletedResultAsync(client, accepted.JobId);

        Assert.Equal("Apple Inc.", result.Company);
        Assert.Equal(new DateOnly(2026, 11, 6), result.AsOf);
        Assert.Equal("deterministic", result.SynthesisDetails.Model);
        var financials = Assert.Single(result.ProviderUsage, usage => usage.Domain == SourceType.FinancialStatement);
        Assert.Equal(ProviderResultStatus.Success, financials.Status);
        Assert.Equal(ProviderOrigin.Recorded, financials.Origin);
        Assert.Equal("SEC EDGAR, recorded 2026-11-06", financials.ProviderName);
        Assert.All(
            result.ProviderUsage.Where(usage => usage.Status == ProviderResultStatus.Success),
            usage => Assert.Equal(ProviderOrigin.Recorded, usage.Origin));

        // Market data and news were not recorded, so the report has none instead of filling the gap.
        Assert.All(
            result.ProviderUsage.Where(usage => usage.Domain is SourceType.MarketSnapshot or SourceType.News),
            usage => Assert.NotEqual(ProviderResultStatus.Success, usage.Status));

        var revenue = Assert.Single(result.Figures, figure => figure.Label == "Revenue");
        Assert.Equal(478_834_000_000m, revenue.Value);
        Assert.Equal("0000320193-26-000026", revenue.AccessionNumber);
        Assert.All(result.Figures, figure => Assert.StartsWith("https://www.sec.gov/Archives/edgar/data/320193/", figure.FilingUrl));
    }

    [Fact]
    public async Task Recorded_Mode_Knows_Only_The_Recorded_Companies_And_Dates()
    {
        await using var factory = new ProofsheetWebApplicationFactory(Recorded());
        using var client = factory.CreateClient();

        using var sample = await client.PostAsJsonAsync("/api/v1/research-jobs", new SubmitResearchJobRequest { Identifier = "NORTHWIND" });
        using var notRecorded = await client.PostAsJsonAsync("/api/v1/research-jobs", new SubmitResearchJobRequest { Identifier = "ORCL" });
        using var afterRecording = await client.PostAsJsonAsync("/api/v1/research-jobs", new SubmitResearchJobRequest { Identifier = "MSFT", AsOf = new DateOnly(2026, 11, 7) });
        using var onRecording = await client.PostAsJsonAsync("/api/v1/research-jobs", new SubmitResearchJobRequest { Identifier = "MSFT", AsOf = new DateOnly(2026, 11, 6) });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, sample.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, notRecorded.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, afterRecording.StatusCode);
        Assert.Contains("recorded on 2026-11-06", await afterRecording.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Accepted, onRecording.StatusCode);
    }

    [Fact]
    public async Task Recorded_Bank_Is_Refused_With_The_Reason()
    {
        await using var factory = new ProofsheetWebApplicationFactory(Recorded());
        using var client = factory.CreateClient();

        var accepted = await SubmitAsync(client, "JPM");
        var status = await WaitForTerminalStatusAsync(client, accepted.JobId);

        Assert.Equal(JobStatus.Failed, status.GetProperty("status").Deserialize<JobStatus>(JsonOptions));
        Assert.Contains("no operating income", status.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Recorded_Mode_Turns_Chat_Off_And_Says_What_The_Data_Is()
    {
        await using var factory = new ProofsheetWebApplicationFactory(Recorded());
        using var client = factory.CreateClient();

        var home = await client.GetStringAsync("/");
        var accepted = await SubmitAsync(client, "AAPL");
        var reportPage = await client.GetStringAsync($"/reports/{accepted.JobId}");

        Assert.Contains("This demo replays SEC data recorded on 2026-11-06 for 14 companies", home, StringComparison.Ordinal);
        Assert.Contains("value=\"AAPL\"", home, StringComparison.Ordinal);
        Assert.DoesNotContain("research-mode", home, StringComparison.Ordinal);
        Assert.Contains("data-recorded-on=\"2026-11-06\"", reportPage, StringComparison.Ordinal);
        Assert.Contains("Chat is off in the recorded demo", reportPage, StringComparison.Ordinal);
    }

    private static Dictionary<string, string?> Recorded() => new() { ["ResearchJobs:Mode"] = "Recorded" };

    private static async Task<ResearchJobAcceptedResponse> SubmitAsync(HttpClient client, string identifier)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/research-jobs", new SubmitResearchJobRequest { Identifier = identifier });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ResearchJobAcceptedResponse>(JsonOptions))!;
    }

    private static async Task<CompletedResearchResult> WaitForCompletedResultAsync(HttpClient client, Guid jobId)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(15);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            using var response = await client.GetAsync($"/api/v1/research-jobs/{jobId}/result");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                return (await response.Content.ReadFromJsonAsync<CompletedResearchResult>(JsonOptions))!;
            }

            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            await Task.Delay(50);
        }

        throw new TimeoutException("Timed out waiting for completed result.");
    }

    private static async Task<JsonElement> WaitForTerminalStatusAsync(HttpClient client, Guid jobId)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(15);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            var status = JsonDocument.Parse(await client.GetStringAsync($"/api/v1/research-jobs/{jobId}")).RootElement;
            if (status.GetProperty("status").GetString() is "Completed" or "Failed")
            {
                return status;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException("Timed out waiting for the job to finish.");
    }

    private static JsonSerializerOptions BuildJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
