// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Proofsheet.Application.Abstractions;
using Proofsheet.Application.Models;
using Proofsheet.Domain.Enums;
using Proofsheet.Domain.Models;
using Proofsheet.Infrastructure.Options;
using Proofsheet.Infrastructure.Reasoning;

namespace Proofsheet.Tests.Unit.Reasoning;

// The report gate is the backend's check on what the model writes. These tests drive it through the reasoner, as
// production does: a rejected draft gets one repair attempt with the reasons, and a draft that fails twice never
// reaches a report.
public sealed class ReportGateTests
{
    private const string Identifier = "NORTHWIND";
    private const string Financials = "financials:NORTHWIND";
    private const string Profile = "profile:NORTHWIND";
    private const string Filing = "filing:NORTHWIND:001";
    private const string News = "news:NORTHWIND:001";
    private const string Unknown = "financials:AAPL";

    public static TheoryData<string, string, string> BrokenDrafts => new()
    {
        { "no thesis", Draft(thesis: ""), "Thesis is required." },
        { "unknown signal", Draft(signal: "StrongBuy"), "Signal must be Bullish, Neutral, or Bearish." },
        { "confidence over 100", Draft(confidence: 101), "Confidence must be between 0 and 100." },
        { "one insight", Draft(insightCount: 1), "Insights must contain between 2 and 5 items." },
        { "six insights", Draft(insightCount: 6), "Insights must contain between 2 and 5 items." },
        { "details do not match insights", Draft(detailCount: 2), "Insight details must match the insight count exactly." },
        { "thesis cites an unknown source", Draft(thesisSources: [Financials, Unknown]), "Thesis details must reference valid source ids." },
        { "signal cites an unknown source", Draft(signalSources: [Unknown]), "Signal details must reference valid source ids." },
        { "confidence cites an unknown source", Draft(confidenceSources: [Unknown]), "Confidence details must reference valid source ids." },
        { "an insight cites an unknown source", Draft(secondInsightSources: [Unknown]), "Insight detail 2 must reference valid source ids." },
    };

    [Theory]
    [MemberData(nameof(BrokenDrafts))]
    public async Task Gate_Rejects_A_Broken_Draft_And_It_Never_Reaches_A_Report(string broken, string draft, string reason)
    {
        var api = new QueuedResponsesApi(draft, draft);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateReasoner(api).SynthesizeAsync(TestEvidenceFactory.Create(Identifier), CancellationToken.None));

        Assert.Contains("validation failed after 2 attempts", failure.Message, StringComparison.Ordinal);
        Assert.Contains(reason, failure.Message, StringComparison.Ordinal);
        Assert.Equal(2, api.Requests.Count);
        Assert.False(string.IsNullOrEmpty(broken));
    }

    [Fact]
    public async Task Rejected_Draft_Is_Repaired_With_The_Reason_And_Only_The_Repair_Is_Used()
    {
        var api = new QueuedResponsesApi(
            Draft(thesis: "Draft citing a source the report does not have.", thesisSources: [Financials, Unknown]),
            Draft(thesis: "Repaired draft."));

        var result = await CreateReasoner(api).SynthesizeAsync(TestEvidenceFactory.Create(Identifier), CancellationToken.None);

        Assert.Equal("Repaired draft.", result.Thesis);
        Assert.Equal(1, result.SynthesisDetails.ValidationRetries);
        Assert.DoesNotContain(Unknown, result.ThesisDetails.SourceIds);
        var repairInstructions = api.Requests[1]["instructions"]!.GetValue<string>();
        Assert.Contains("Thesis details must reference valid source ids.", repairInstructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Confidence_Is_Clamped_To_Ten_Points_Either_Side_Of_The_Baseline()
    {
        // Two missing sources cost 20 points, which puts the sample's baseline mid-range so neither end of the band
        // is cut off at 0 or 100.
        var evidence = TestEvidenceFactory.Create(Identifier);
        evidence = evidence with { Assessment = evidence.Assessment with { MissingNonCriticalSourceCount = 2 } };

        var high = await CreateReasoner(new QueuedResponsesApi(Draft(confidence: 100))).SynthesizeAsync(evidence, CancellationToken.None);
        var low = await CreateReasoner(new QueuedResponsesApi(Draft(confidence: 0))).SynthesizeAsync(evidence, CancellationToken.None);
        var middle = (high.Confidence + low.Confidence) / 2;
        var inside = await CreateReasoner(new QueuedResponsesApi(Draft(confidence: middle))).SynthesizeAsync(evidence, CancellationToken.None);

        Assert.True(high.Confidence < 100, $"high {high.Confidence}");
        Assert.True(low.Confidence > 0, $"low {low.Confidence}");
        Assert.Equal(20, high.Confidence - low.Confidence);
        Assert.Equal((100, high.Confidence), (high.ConfidenceDetails.BoundedFrom, high.ConfidenceDetails.BoundedTo));
        Assert.Equal((0, low.Confidence), (low.ConfidenceDetails.BoundedFrom, low.ConfidenceDetails.BoundedTo));
        Assert.Equal(middle, inside.Confidence);
    }

    [Theory]
    [InlineData(54, "Bullish", InvestmentSignal.Neutral, true)]
    [InlineData(54, "Bearish", InvestmentSignal.Neutral, true)]
    [InlineData(54, "Neutral", InvestmentSignal.Neutral, false)]
    [InlineData(55, "Bullish", InvestmentSignal.Bullish, false)]
    [InlineData(55, "Bearish", InvestmentSignal.Bearish, false)]
    public async Task Weak_Evidence_Overrides_A_Directional_Signal(int evidenceScore, string draftSignal, InvestmentSignal expected, bool overridden)
    {
        var evidence = TestEvidenceFactory.Create(Identifier);
        evidence = evidence with { Assessment = evidence.Assessment with { EvidenceScore = evidenceScore } };

        var result = await CreateReasoner(new QueuedResponsesApi(Draft(signal: draftSignal))).SynthesizeAsync(evidence, CancellationToken.None);

        Assert.Equal(expected, result.Signal);
        Assert.Equal(expected, result.SignalDetails.Signal);
        Assert.Equal(overridden, result.SignalDetails.OverrideApplied);
    }

    // A draft in the shape the model is asked for, valid unless an argument breaks it.
    private static string Draft(
        string thesis = "Northwind converts revenue into cash.",
        string signal = "Bullish",
        int confidence = 84,
        int insightCount = 3,
        int? detailCount = null,
        string[]? thesisSources = null,
        string[]? signalSources = null,
        string[]? confidenceSources = null,
        string[]? secondInsightSources = null)
    {
        var insights = Enumerable.Range(1, insightCount).Select(index => $"Insight {index}.").ToArray();
        var details = new JsonArray();
        foreach (var index in Enumerable.Range(0, detailCount ?? insightCount))
        {
            var sources = index == 1 && secondInsightSources is not null ? secondInsightSources : [index % 2 == 0 ? Financials : News];
            details.Add(new JsonObject { ["insight"] = $"Insight {index + 1}.", ["sourceIds"] = Ids(sources) });
        }

        var draft = new JsonObject
        {
            ["thesis"] = thesis,
            ["signal"] = signal,
            ["confidence"] = confidence,
            ["insights"] = new JsonArray(insights.Select(insight => (JsonNode)insight).ToArray()),
            ["thesisDetails"] = new JsonObject { ["summary"] = "Financials and profile.", ["sourceIds"] = Ids(thesisSources ?? [Financials, Profile]) },
            ["insightDetails"] = details,
            ["signalDetails"] = new JsonObject { ["rationale"] = "Financials support it.", ["sourceIds"] = Ids(signalSources ?? [Financials]) },
            ["confidenceDetails"] = new JsonObject { ["rationale"] = "Evidence is complete.", ["sourceIds"] = Ids(confidenceSources ?? [Financials, Filing]) }
        };

        return new JsonObject { ["id"] = "resp_draft", ["output_text"] = draft.ToJsonString() }.ToJsonString();
    }

    private static JsonArray Ids(IEnumerable<string> ids) => new(ids.Select(id => (JsonNode)id).ToArray());

    private static OpenAiResearchReasoner CreateReasoner(IResponsesApi api) =>
        new(
            api,
            new StubPromptProvider(),
            Options.Create(new OpenAiResponsesOptions { Enabled = true, ApiKey = "test-key", ReportModel = "test-model", ReportValidationRetries = 1 }),
            new DeterministicResearchReasoner());

    private sealed class QueuedResponsesApi(params string[] responses) : IResponsesApi
    {
        private readonly Queue<string> _responses = new(responses);

        public List<JsonObject> Requests { get; } = [];

        public Task<JsonDocument> CreateResponseAsync(JsonObject body, int timeoutSeconds, CancellationToken cancellationToken = default)
        {
            Requests.Add(body);
            return Task.FromResult(JsonDocument.Parse(_responses.Dequeue()));
        }

        public Task<JsonDocument> CreateResponseStreamAsync(JsonObject body, int timeoutSeconds, Func<ReportChatProviderEvent, Task>? onStreamEvent, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubPromptProvider : IReportSynthesisPromptProvider
    {
        public string GetPrompt() => "Return JSON only.";
    }
}
