# ADR-0009: Why context providers derive from `MessageAIContextProvider`

- **Status:** Accepted
- **Date:** 2026-05-23
- **Applies to:** `Sentirum.Agent.Context` 0.1.0-preview onward

## Context

Microsoft Agent Framework ships two abstract base classes for context
providers:

- **`AIContextProvider`** — the most general form. Override
  `ProvideAIContextAsync(InvokingContext)` returning an `AIContext`
  that can set `Instructions`, register tools, or attach extra
  message-shape data. `InvokingContext.Session` is available but
  `RequestMessages` is not.
- **`MessageAIContextProvider`** — convenience base that focuses on
  the message stream. Override
  `ProvideMessagesAsync(InvokingContext)` returning an
  `IEnumerable<ChatMessage>` to prepend to the request.
  `InvokingContext.RequestMessages` carries the user turn that
  triggered this run.

## Decision

All three shipped context providers
(`InstructionsContextProvider`, `MemoryContextProvider`,
`KnowledgeBaseContextProvider`) derive from
`MessageAIContextProvider` and implement `ProvideMessagesAsync`.

Rationale:

1. **Access to `RequestMessages`.**
   `KnowledgeBaseContextProvider` searches keyed on the latest user
   message in the request. Only `MessageAIContextProvider.InvokingContext`
   exposes that.
2. **Stable injection point.** Returning a `ChatMessage` of role
   `System` is consistent across MAF versions; the
   `AIContext.Instructions` path has changed shape between MAF
   previews and is more brittle.
3. **Composability.** Multiple message-providers stack predictably:
   each prepends its messages in registration order. Mixing
   `AIContext.Instructions` providers with message providers in the
   same agent leads to two parallel injection paths that callers have
   to reason about together.
4. **Empty result is well-defined.** `ProvideMessagesAsync` returns
   `Array.Empty<ChatMessage>()` for the no-op case; MAF treats this
   the same as "no contribution" so callers don't pay for empty
   providers.

## Consequences

- Anyone authoring a Sentirum-native context provider can pattern
  after the shipped ones with a simple `MessageAIContextProvider`
  subclass — no `AIContext.Instructions` knowledge required.
- Providers that need to register dynamic tools (`AIContext.Tools`)
  still need to derive from the base `AIContextProvider`; they are
  not blocked, just unusual. When we ship a tool-injecting provider
  it will be a separate base class in `Sentirum.Agent.Context.Tools`
  and this ADR will get an addendum.
- The `WithAmbientInstructions(...)` extension takes a delegate that
  returns `string?` and translates it into a system message — users
  never see the `AIContext.Instructions` path even though it would
  also work.

## Alternatives considered

- **Derive from `AIContextProvider` and return
  `AIContext { Instructions = ... }`.** First implementation went
  this route and the `KnowledgeBaseContextProvider` tests proved it
  flawed because the chat-client-level `InvokingContext` did not
  carry `RequestMessages`. Switched in M4 before commit.
- **Custom adapter that lets users pick their base class per
  provider.** Over-engineered for today; revisit if the tool-context
  case lands.
