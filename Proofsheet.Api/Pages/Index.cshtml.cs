// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Proofsheet.Application.Abstractions;
using Proofsheet.Domain.Enums;
using Proofsheet.Infrastructure.Clients;
using Proofsheet.Infrastructure.Options;
using Proofsheet.Infrastructure.Providers;

namespace Proofsheet.Api.Pages;

public sealed class IndexModel : PageModel
{
    private readonly IResearchExecutionModeResolver _executionModeResolver;
    private readonly MockResearchSeedCatalog _sampleCatalog;
    private readonly RecordedSecData _recordedData;
    private readonly OpenAiResponsesOptions _modelOptions;

    public IndexModel(
        IResearchExecutionModeResolver executionModeResolver,
        MockResearchSeedCatalog sampleCatalog,
        RecordedSecData recordedData,
        IOptions<OpenAiResponsesOptions> modelOptions)
    {
        _executionModeResolver = executionModeResolver;
        _sampleCatalog = sampleCatalog;
        _recordedData = recordedData;
        _modelOptions = modelOptions.Value;
    }

    public ResearchExecutionMode Mode { get; private set; }

    /// <summary>True when this server has no live data configured, so every report uses fictional sample data.</summary>
    public bool UsesSampleData => Mode == ResearchExecutionMode.Mock;

    /// <summary>True when this server replays recorded SEC data instead of calling SEC.</summary>
    public bool UsesRecordedData => Mode == ResearchExecutionMode.Recorded;

    public IReadOnlyCollection<string> SampleCompanies => _sampleCatalog.Identifiers;

    public IReadOnlyList<RecordedCompany> RecordedCompanies => _recordedData.Companies;

    public DateOnly RecordedOn => _recordedData.RecordedOn;

    /// <summary>Autonomous research needs a model; it is offered only where one is configured for live data.</summary>
    public bool OffersAutonomousMode =>
        Mode is ResearchExecutionMode.Hybrid or ResearchExecutionMode.LiveRequired &&
        _modelOptions.Enabled &&
        !string.IsNullOrWhiteSpace(_modelOptions.ApiKey);

    public string DefaultIdentifier => Mode switch
    {
        ResearchExecutionMode.Mock => MockResearchSeedCatalog.FeaturedIdentifier,
        ResearchExecutionMode.Recorded => RecordedCompanies.FirstOrDefault()?.Ticker ?? string.Empty,
        _ => string.Empty
    };

    public void OnGet()
    {
        Mode = _executionModeResolver.Resolve();
    }
}
