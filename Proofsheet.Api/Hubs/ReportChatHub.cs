using Proofsheet.Application.Abstractions;
using Proofsheet.Application.Models;
using Microsoft.AspNetCore.SignalR;

namespace Proofsheet.Api.Hubs;

public sealed class ReportChatHub : Hub
{
    private readonly IReportChatOrchestrator _reportChatOrchestrator;
    private readonly ILogger<ReportChatHub> _logger;

    public ReportChatHub(IReportChatOrchestrator reportChatOrchestrator, ILogger<ReportChatHub> logger)
    {
        _reportChatOrchestrator = reportChatOrchestrator;
        _logger = logger;
    }

    public Task<ReportChatThreadCreatedResponse> CreateThread(Guid jobId) =>
        InvokeAsync(nameof(CreateThread), () => _reportChatOrchestrator.CreateThreadAsync(jobId, Context.ConnectionAborted));

    public async Task SendMessage(string threadId, string message)
    {
        try
        {
            await InvokeAsync(nameof(SendMessage), async () =>
            {
                var callbacks = new ReportChatStreamingCallbacks
                {
                    OnStreamEvent = streamEvent => Clients.Caller.SendAsync("ReceiveStreamEvent", streamEvent, Context.ConnectionAborted),
                    OnChatMessage = chatMessage => Clients.Caller.SendAsync("ReceiveChatMessage", chatMessage, Context.ConnectionAborted)
                };

                await _reportChatOrchestrator.SendMessageAsync(threadId, message, callbacks, Context.ConnectionAborted);
                return true;
            });
        }
        catch (OperationCanceledException)
        {
        }
    }

    public Task<ReportChatStreamResumeResponse> ResumeStream(string threadId, long lastSequence) =>
        InvokeAsync(nameof(ResumeStream), () => _reportChatOrchestrator.ResumeStreamAsync(threadId, lastSequence, Context.ConnectionAborted));

    public Task<bool> CancelStream(string threadId) =>
        InvokeAsync(nameof(CancelStream), async () =>
        {
            var canceledEvent = await _reportChatOrchestrator.CancelStreamAsync(threadId, Context.ConnectionAborted);
            await Clients.Caller.SendAsync("ReceiveStreamEvent", canceledEvent, Context.ConnectionAborted);
            return true;
        });

    // Only the chat's own fixed messages reach the browser. Anything else is logged in full under a reference, and the
    // browser gets that reference so a report of the failure can be matched to the log line.
    private async Task<T> InvokeAsync<T>(string operation, Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (ReportChatRequestException ex)
        {
            throw new HubException(ex.Message);
        }
        catch (ReportChatFailedException ex)
        {
            _logger.LogError(ex.InnerException, "Report chat {Operation} failed. Reference {Reference}.", operation, ex.Reference);
            throw new HubException(ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var reference = ReportChatFailedException.NewReference();
            _logger.LogError(ex, "Report chat {Operation} failed. Reference {Reference}.", operation, reference);
            throw new HubException(ReportChatFailedException.MessageFor(reference));
        }
    }
}
