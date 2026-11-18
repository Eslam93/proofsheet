// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

using Proofsheet.Application.Abstractions;
using Proofsheet.Application.Models;
using Proofsheet.Domain.Enums;
using Proofsheet.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Proofsheet.Infrastructure.Chat;

public sealed class OpenAiResponsesReportChatAvailabilityService : IReportChatAvailabilityService
{
    private readonly OpenAiResponsesOptions _options;
    private readonly bool _recorded;

    public OpenAiResponsesReportChatAvailabilityService(IOptions<OpenAiResponsesOptions> options, IOptions<ResearchExecutionOptions> executionOptions)
    {
        _options = options.Value;
        _recorded = executionOptions.Value.Mode == ResearchExecutionMode.Recorded;
    }

    // The recorded demo never calls a model, so chat stays off there even if a key is configured.
    public bool IsConfigured =>
        !_recorded
        && _options.Enabled
        && !string.IsNullOrWhiteSpace(_options.ApiKey)
        && !string.IsNullOrWhiteSpace(string.IsNullOrWhiteSpace(_options.ChatModel) ? _options.Model : _options.ChatModel);

    public string UnavailableReason => _recorded
        ? "Chat is off in the recorded demo, which calls no model."
        : "Report chat is not configured. Set OpenAiResponses:Enabled, OpenAiResponses:ApiKey, and OpenAiResponses:ChatModel or OpenAiResponses:Model.";
}
