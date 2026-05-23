# ADR-0008: Context-provider mounting point — chat-client innermost

- **Status:** Accepted
- **Date:** 2026-05-23
- **Applies to:** `Sentirum.Agent.Context` 0.1.0-preview onward

## Context

Microsoft Agent Framework exposes two places to register an
`AIContextProvider`:

1. **`AIAgentBuilder.UseAIContextProviders(...)`** — providers run as
   part of the MAF agent loop. Their `InvokingContext` sees the
   agent-level session and run-state.
2. **`ChatClientBuilder.UseAIContextProviders(...)`** — providers
   become a delegating chat client layer that sits in front of the
   leaf LLM client. Their `InvokingContext` sees the request messages
   that flow through the chat client.

Sentirum's context providers (`MemoryContextProvider`,
`InstructionsContextProvider`, `KnowledgeBaseContextProvider`) derive
from `MessageAIContextProvider`, which exposes
`InvokingContext.RequestMessages`. That property only carries the
incoming user turn when mounted at the chat-client level — the
agent-level invocation context does not surface request messages.

## Decision

`SentirumAgentFactory.Create` mounts context providers via
`ChatClientBuilder.UseAIContextProviders(...)`, appended **after** all
user-supplied `ConfigureChatClient` layers. Practically that means:

1. The chat-client pipeline composed by the factory is:
   `leaf chat client ← UseAIContextProviders ← user ConfigureChatClient layers (in registration order)`.
2. Context providers sit closest to the leaf, so enrichment happens
   immediately before the LLM call.
3. User middleware (logging, tracing, custom delegating clients) sees
   the **pre-enrichment** messages on the way in and the
   **post-LLM** response on the way out.
4. Provider order is preserved across `WithContextProvider(...)`
   calls so callers can predict which provider runs first.

## Consequences

- Context providers always observe the user's request messages,
  because `MessageAIContextProvider.InvokingContext.RequestMessages`
  is populated at the chat-client level.
- Telemetry / logging middleware that wants to observe the *enriched*
  prompt has to sit *between* `UseAIContextProviders` and the leaf —
  not currently possible through `ConfigureChatClient` because the
  factory appends context providers last. We accept this for v0.1; if
  a user needs post-enrichment logging they can write a custom
  context provider or wrap the leaf chat client directly via
  `UseChatClient`.
- The `ConfigureChatClient` ordering contract from ADR-0003 is
  preserved (first-registered = outermost among user layers); the
  factory's append happens after user layers and is *not* part of
  that contract.

## Alternatives considered

- **Mount at the agent level (`AIAgentBuilder.UseAIContextProviders`).**
  Rejected because the agent-level `InvokingContext` does not carry
  request messages, breaking `KnowledgeBaseContextProvider`'s
  "search keyed on latest user message" semantics. We would have to
  reach into the agent's session history each turn, which is racy
  with concurrent calls.
- **Append context providers as the outermost chat-client layer**
  (i.e. before any user middleware). Rejected because logging or
  rate-limiting middleware that runs against the **pre-enrichment**
  prompt is more common than the inverse; appending innermost makes
  the more common case work without configuration.
- **Mount per-provider configurable** (caller chooses chat-client vs.
  agent level). Considered for v1.0 once we see real demand; today
  every shipped provider derives from `MessageAIContextProvider` so
  the chat-client level is the only correct mount point.
