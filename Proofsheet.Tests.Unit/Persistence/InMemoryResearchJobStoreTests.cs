// SPDX-FileCopyrightText: 2026 Eslam Hamed
//
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.Options;
using Proofsheet.Application.Models;
using Proofsheet.Domain.Enums;
using Proofsheet.Infrastructure.Persistence;
using Proofsheet.Tests.Unit.Chat;

namespace Proofsheet.Tests.Unit.Persistence;

public sealed class InMemoryResearchJobStoreTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Finished_Job_Expires_After_The_Retention_Period()
    {
        var clock = new FakeTimeProvider(Start);
        var store = CreateStore(clock, retentionMinutes: 60, maxStoredJobs: 10);
        var job = Job(JobStatus.Completed, completedAt: Start);
        Assert.True(store.TryAdd(job));

        clock.Advance(TimeSpan.FromMinutes(60));
        Assert.NotNull(store.Get(job.JobId));

        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Null(store.Get(job.JobId));
    }

    [Fact]
    public void Running_Job_Never_Expires()
    {
        var clock = new FakeTimeProvider(Start);
        var store = CreateStore(clock, retentionMinutes: 60, maxStoredJobs: 10);
        var job = Job(JobStatus.Running, completedAt: null);
        Assert.True(store.TryAdd(job));

        clock.Advance(TimeSpan.FromDays(3));

        Assert.NotNull(store.Get(job.JobId));
    }

    [Fact]
    public void Full_Store_Drops_The_Oldest_Finished_Job_And_Keeps_Running_Ones()
    {
        var clock = new FakeTimeProvider(Start);
        var store = CreateStore(clock, retentionMinutes: 600, maxStoredJobs: 3);
        var running = Job(JobStatus.Running, completedAt: null);
        var older = Job(JobStatus.Completed, completedAt: Start.AddMinutes(-20));
        var newer = Job(JobStatus.Failed, completedAt: Start.AddMinutes(-10));
        Assert.True(store.TryAdd(running));
        Assert.True(store.TryAdd(older));
        Assert.True(store.TryAdd(newer));

        var incoming = Job(JobStatus.Queued, completedAt: null);
        Assert.True(store.TryAdd(incoming));

        Assert.Null(store.Get(older.JobId));
        Assert.NotNull(store.Get(newer.JobId));
        Assert.NotNull(store.Get(running.JobId));
        Assert.NotNull(store.Get(incoming.JobId));
    }

    private static InMemoryResearchJobStore CreateStore(TimeProvider clock, int retentionMinutes, int maxStoredJobs) =>
        new(clock, Options.Create(new ResearchExecutionOptions { JobRetentionMinutes = retentionMinutes, MaxStoredJobs = maxStoredJobs }));

    private static ResearchJobRecord Job(JobStatus status, DateTimeOffset? completedAt) =>
        new(
            Guid.NewGuid(),
            "AAPL",
            status,
            Start.AddHours(-1),
            status == JobStatus.Queued ? null : Start.AddHours(-1),
            completedAt,
            "Completion",
            [],
            null,
            Guid.NewGuid().ToString("N"),
            [],
            null);
}
