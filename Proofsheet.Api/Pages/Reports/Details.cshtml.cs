// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

using Proofsheet.Application.Abstractions;
using Proofsheet.Domain.Enums;
using Proofsheet.Infrastructure.Clients;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Proofsheet.Api.Pages.Reports;

public sealed class DetailsModel : PageModel
{
    private readonly IReportChatAvailabilityService _reportChatAvailabilityService;
    private readonly IResearchExecutionModeResolver _executionModeResolver;
    private readonly RecordedSecData _recordedData;

    public DetailsModel(
        IReportChatAvailabilityService reportChatAvailabilityService,
        IResearchExecutionModeResolver executionModeResolver,
        RecordedSecData recordedData)
    {
        _reportChatAvailabilityService = reportChatAvailabilityService;
        _executionModeResolver = executionModeResolver;
        _recordedData = recordedData;
    }

    /// <summary>The recording's date when this server replays recorded SEC data; empty otherwise.</summary>
    public string RecordedOn => _executionModeResolver.Resolve() == ResearchExecutionMode.Recorded
        ? _recordedData.RecordedOn.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
        : string.Empty;

    [FromRoute]
    public Guid JobId { get; set; }

    public bool IsReportChatConfigured => _reportChatAvailabilityService.IsConfigured;

    public string ReportChatUnavailableReason => _reportChatAvailabilityService.UnavailableReason;

    public void OnGet()
    {
    }
}
