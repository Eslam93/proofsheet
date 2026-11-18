# 0006. Raw HTTP clients

Written on 2026-11-18, after the decision, from the code and the commit history. The decision dates from the first commit, `5614c0a`, on 2026-03-18.

## Context

Proofsheet calls four services: SEC EDGAR, Twelve Data, Alpha Vantage and the OpenAI Responses API. SEC has no official
.NET client. The Responses API's streaming events and tool calls had to be handled one by one for the chat.

## Decision

Each service is called through a typed `HttpClient` with hand-written requests and JSON parsing, not through an SDK.

## Consequences

- The app depends on no vendor SDK, and every request and response shape is in this repository.
- Swapping the HTTP handler changes where the data comes from without touching the client: the tests use fake handlers,
  and the recorded demo replays SEC data from disk through the real client (commit `5894b4a`).
- The parsing is this project's to maintain. For example, since .NET 9 a JSON `null` body no longer deserializes to null,
  and the clients had to check for it; retries, timeouts and error mapping are hand-written too.
- Upstream error text has to be kept out of user-facing messages by hand, which the chat did not do until commit
  `2c88f00`.

## Considered

- The official OpenAI .NET SDK: less code, but the streaming and tool loop would have been shaped by it.
- `Microsoft.Extensions.AI`: a later option, listed on the roadmap, not adopted.

## Evidence

`Proofsheet.Infrastructure/Clients`, `Proofsheet.Infrastructure/Chat/ResponsesApi.cs`, `ResponsesApiTests`, and
`HybridLiveProviderTests`, including the JSON `null` body cases.
