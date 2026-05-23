# ADR-0001: Chat-client ownership and agent disposal

- **Status:** Accepted
- **Date:** 2026-05-23
- **Applies to:** Sentirum 0.1.0-preview onward

## Context

Sentirum agents are composed at build time from a leaf `IChatClient`
produced by a provider extension (`UseOpenAI`, `UseAnthropic`,
`UseOllama`, `UseZAI`, etc.) plus zero or more delegating layers added
via `ConfigureChatClient`. The leaf client typically owns a transport
client (`OpenAIClient`, `OllamaApiClient`, an HTTP client, gRPC channel,
etc.) that holds OS-level resources.

Before this ADR the lifecycle was ambiguous:

- Provider closures captured the leaf SDK client in a lambda
  (`UseChatClient(_ => new OllamaApiClient(...))`), so nothing in DI
  saw it.
- `SentirumAgent` did not implement `IDisposable` or
  `IAsyncDisposable`, so even when an agent fell out of scope its
  chat-client pipeline could leak connections.
- The reviewer in M3 (`gpt-5.5`, ADR-0006) flagged this as a v1.0
  blocker because long-running hosts (web apps, workers) will rebuild
  agents on configuration reload and accumulate handles.

## Decision

1. **`ISentirumAgent` implements `IAsyncDisposable`** (primary path)
   plus `IDisposable` (so non-async DI scopes can tear down the
   container).
2. **`SentirumAgent` owns the inner `AIAgent`.** On dispose it checks
   whether the inner agent is `IAsyncDisposable` (preferred) or
   `IDisposable` and propagates the call.
3. **`ChatClientAgent` (the MAF inner) already disposes the composed
   `IChatClient` pipeline for us.** Sentirum does not double-dispose
   the chat client to avoid `ObjectDisposedException` cascades.
4. **Provider extensions stay closure-based** for now (a single shared
   leaf client per agent build). When a provider needs explicit DI
   registration (e.g. a custom Polly handler) it can register the
   transport on `Services` directly and resolve it inside the
   `UseChatClient` lambda.
5. **`ThrowIfDisposed`** is checked on `RunAsync` / `RunStreamingAsync`
   so misuse fails loudly instead of dispatching to a torn-down client.

## Consequences

- Hosts that resolve agents from DI (`AddSentirumAgent`) get correct
  disposal for free because the container disposes the scope.
- Hosts that build agents manually must `await using` or `using` the
  agent — documented in the README and sample 01.
- Providers that bring their own transport (HTTP, gRPC, queue) can
  wrap it in `DelegatingChatClient` and rely on `ChatClientAgent` to
  dispose the chain.

## Alternatives considered

- **DI-owned transport clients (e.g. always register `OpenAIClient`
  as singleton).** Cleaner ownership but forces every provider
  extension to add three DI registrations and complicates options
  binding. Deferred — provider extensions remain closure-based until
  we have a real-world need.
- **Reference-counted shared chat clients.** Adds a runtime concept
  (a "shared client" wrapper) that users have to reason about. Not
  worth the surface-area cost for the current scenarios.
