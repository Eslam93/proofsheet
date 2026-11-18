using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Proofsheet.Application.Abstractions;
using Proofsheet.Application.Models;

namespace Proofsheet.Infrastructure.Persistence;

/// <summary>
/// Keeps research jobs in memory. A finished job is dropped once it is older than the retention period, and when the
/// store is full the oldest finished jobs make room, so memory stays bounded however many jobs are submitted. Queued
/// and running jobs are never dropped; the queue's capacity bounds them.
/// </summary>
public sealed class InMemoryResearchJobStore : IResearchJobStore
{
    private readonly ConcurrentDictionary<Guid, ResearchJobRecord> _records = new();
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _retention;
    private readonly int _maxStoredJobs;
    private readonly object _admissionSync = new();

    public InMemoryResearchJobStore(TimeProvider timeProvider, IOptions<ResearchExecutionOptions> options)
    {
        _timeProvider = timeProvider;
        _retention = TimeSpan.FromMinutes(Math.Max(options.Value.JobRetentionMinutes, 1));
        _maxStoredJobs = Math.Max(options.Value.MaxStoredJobs, 1);
    }

    public bool TryAdd(ResearchJobRecord record)
    {
        lock (_admissionSync)
        {
            RemoveExpired();
            var excess = _records.Count - _maxStoredJobs + 1;
            if (excess > 0)
            {
                foreach (var oldest in _records.Values
                             .Where(item => item.CompletedAtUtc is not null)
                             .OrderBy(item => item.CompletedAtUtc)
                             .Take(excess)
                             .ToArray())
                {
                    _records.TryRemove(oldest.JobId, out _);
                }
            }

            return _records.TryAdd(record.JobId, record);
        }
    }

    public bool TryRemove(Guid jobId) => _records.TryRemove(jobId, out _);

    public ResearchJobRecord? Get(Guid jobId)
    {
        if (!_records.TryGetValue(jobId, out var record))
        {
            return null;
        }

        if (IsExpired(record))
        {
            _records.TryRemove(jobId, out _);
            return null;
        }

        return record;
    }

    public bool TryUpdate(Guid jobId, Func<ResearchJobRecord, ResearchJobRecord> update)
    {
        while (true)
        {
            if (!_records.TryGetValue(jobId, out var current))
            {
                return false;
            }

            var next = update(current);
            if (_records.TryUpdate(jobId, next, current))
            {
                return true;
            }
        }
    }

    private void RemoveExpired()
    {
        foreach (var pair in _records)
        {
            if (IsExpired(pair.Value))
            {
                _records.TryRemove(pair.Key, out _);
            }
        }
    }

    private bool IsExpired(ResearchJobRecord record) =>
        record.CompletedAtUtc is { } completedAt && _timeProvider.GetUtcNow() - completedAt > _retention;
}
