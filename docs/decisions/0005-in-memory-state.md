# 0005. In-memory state

Written on 2026-11-18, after the decision, from the code and the commit history. The decision dates from the first commit, `5614c0a`, on 2026-03-18; the limits on it from commit `d14b29c` on 2026-11-18.

## Context

The project began as a take-home for one process: jobs, reports, chat threads and autonomous sessions had to live
somewhere, and a database would have been one more thing to run and to explain.

## Decision

All state lives in memory, in one process, behind interfaces (`IResearchJobStore`, `IReportChatThreadStore`,
`IAutonomousJobSessionStore`). Since 2026-11-18 a finished job is kept for 24 hours and at most 500 jobs are kept, oldest
finished first; chat threads expire after an hour without use.

## Consequences

- Nothing survives a restart, and the app cannot run as more than one instance behind a load balancer.
- Memory is bounded however many jobs are submitted, and per-address rate limits bound how fast they arrive.
- The recorded demo needs no state between restarts: its data is on disk and its reports are rebuilt on request.
- Replacing a store means one new implementation of its interface.

## Considered

- SQLite or PostgreSQL: durable, but nothing here needs durability yet.
- Redis: the same, plus another service to run.

## Evidence

`Proofsheet.Infrastructure/Persistence`, and `InMemoryResearchJobStoreTests` for expiry and the cap.
