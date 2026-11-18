// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

using Proofsheet.Application.Abstractions;
using Proofsheet.Application.Models;
using Proofsheet.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Proofsheet.Application.Services;

public sealed class ResearchJobSubmissionService : IResearchJobSubmissionService
{
    // SEC began requiring XBRL financial data in 2009; before it there is nothing to select from.
    private static readonly DateOnly EarliestAsOf = new(2009, 6, 15);

    private readonly IIdentifierValidator _identifierValidator;
    private readonly ITickerDirectoryService _tickerDirectoryService;
    private readonly IResearchExecutionModeResolver _executionModeResolver;
    private readonly IResearchJobStore _jobStore;
    private readonly IResearchJobQueue _jobQueue;
    private readonly TimeProvider _timeProvider;
    private readonly ResearchExecutionOptions _options;
    private readonly ILogger<ResearchJobSubmissionService> _logger;

    public ResearchJobSubmissionService(
        IIdentifierValidator identifierValidator,
        ITickerDirectoryService tickerDirectoryService,
        IResearchExecutionModeResolver executionModeResolver,
        IResearchJobStore jobStore,
        IResearchJobQueue jobQueue,
        TimeProvider timeProvider,
        IOptions<ResearchExecutionOptions> options,
        ILogger<ResearchJobSubmissionService> logger)
    {
        _identifierValidator = identifierValidator;
        _tickerDirectoryService = tickerDirectoryService;
        _executionModeResolver = executionModeResolver;
        _jobStore = jobStore;
        _jobQueue = jobQueue;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    public ResearchJobSubmissionService(
        IIdentifierValidator identifierValidator,
        ITickerDirectoryService tickerDirectoryService,
        IResearchExecutionModeResolver executionModeResolver,
        IResearchJobStore jobStore,
        IResearchJobQueue jobQueue,
        TimeProvider timeProvider)
        : this(
            identifierValidator,
            tickerDirectoryService,
            executionModeResolver,
            jobStore,
            jobQueue,
            timeProvider,
            Options.Create(new ResearchExecutionOptions()),
            NullLogger<ResearchJobSubmissionService>.Instance)
    {
    }

    public async Task<ResearchJobSubmissionResult> SubmitAsync(
        string? identifier,
        ResearchMode? researchMode,
        DateOnly? asOf,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validation = _identifierValidator.Validate(identifier);
        if (!validation.IsValid)
        {
            return ResearchJobSubmissionResult.Rejected(validation.Problem!);
        }

        // The date is fixed at submission, so the job reads the same filings however long it waits in the queue. A
        // recording holds nothing filed after the day it was made, so recorded reports default to that day.
        var executionMode = _executionModeResolver.Resolve();
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        var latestAsOf = executionMode == ResearchExecutionMode.Recorded ? _options.RecordedOn : today;
        var effectiveAsOf = asOf ?? latestAsOf;
        if (effectiveAsOf > today)
        {
            return ResearchJobSubmissionResult.Rejected(InvalidAsOf("The as-of date cannot be in the future."));
        }

        if (effectiveAsOf > latestAsOf)
        {
            return ResearchJobSubmissionResult.Rejected(InvalidAsOf($"This server replays SEC data recorded on {latestAsOf:yyyy-MM-dd}, so it has nothing filed after that date."));
        }

        if (effectiveAsOf < EarliestAsOf)
        {
            return ResearchJobSubmissionResult.Rejected(InvalidAsOf($"The as-of date cannot be before {EarliestAsOf:yyyy-MM-dd}, when SEC XBRL financial data begins."));
        }

        var normalizedIdentifier = validation.NormalizedIdentifier!;
        var resolvedTicker = await _tickerDirectoryService.ResolveAsync(normalizedIdentifier, cancellationToken);
        if (resolvedTicker is null)
        {
            return ResearchJobSubmissionResult.Rejected(
                new ProblemDescriptor(
                    422,
                    "/problems/unsupported-identifier",
                    "Unsupported identifier.",
                    "The identifier is syntactically valid but could not be resolved as a supported US-listed operating company.",
                    "UnsupportedIdentifier",
                    Identifier: normalizedIdentifier));
        }

        var effectiveResearchMode = researchMode ?? ResearchMode.Standard;
        ResearchJobRecord record;
        do
        {
            record = new ResearchJobRecord(
                Guid.NewGuid(),
                normalizedIdentifier,
                JobStatus.Queued,
                _timeProvider.GetUtcNow(),
                null,
                null,
                JobStatus.Queued.ToString(),
                Array.Empty<string>(),
                null,
                Guid.NewGuid().ToString("N"),
                Array.Empty<ResearchTraceEvent>(),
                null,
                effectiveResearchMode,
                AgentRole.None,
                string.Empty);
        }
        while (!_jobStore.TryAdd(record));

        if (!_jobQueue.TryEnqueue(new ResearchJobQueueItem(record.JobId, record.Identifier, record.CorrelationId, effectiveResearchMode, executionMode, resolvedTicker, effectiveAsOf)))
        {
            _jobStore.TryRemove(record.JobId);
            _logger.LogWarning(
                "Queue admission failed for {JobId} {Identifier} {CorrelationId}",
                record.JobId,
                record.Identifier,
                record.CorrelationId);

            return ResearchJobSubmissionResult.Rejected(
                new ProblemDescriptor(
                    503,
                    "/problems/queue-capacity-exceeded",
                    "Queue capacity exceeded.",
                    "The service is temporarily unable to accept additional research jobs.",
                    "QueueCapacityExceeded",
                    Identifier: record.Identifier,
                    CorrelationId: record.CorrelationId));
        }

        _logger.LogInformation(
            "Accepted research job {JobId} for {Identifier} with correlation {CorrelationId} in {ResearchMode} mode",
            record.JobId,
            record.Identifier,
            record.CorrelationId,
            effectiveResearchMode);

        return ResearchJobSubmissionResult.Accepted(
            new ResearchJobAcceptedResponse(
                record.JobId,
                JobStatus.Queued,
                record.CreatedAtUtc,
                $"/api/v1/research-jobs/{record.JobId}",
                $"/api/v1/research-jobs/{record.JobId}/result"));
    }

    private static ProblemDescriptor InvalidAsOf(string detail) =>
        new(400, "/problems/invalid-request", "Invalid request.", detail, "InvalidRequest");
}
