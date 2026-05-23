# ADR-0003: Builder pipeline ordering and naming

- **Status:** Accepted
- **Date:** 2026-05-23
- **Applies to:** Sentirum 0.1.0-preview onward

## Context

`ISentirumAgentBuilder` exposes three composition primitives:

- `UseChatClient(Func<IServiceProvider, IChatClient>)` — sets the
  leaf provider client. Last call wins.
- `ConfigureChatClient(Action<ChatClientBuilder>)` — adds a
  delegating layer (function invocation, telemetry, retries, cache,
  custom middleware). Multiple calls are allowed; order matters.
- `Configure(Action<SentirumAgentOptions>)` — mutates agent-level
  options (instructions, description, model, tools).

Two questions came up during the M3 review:

1. **What is the layering order?** If a user calls
   `ConfigureChatClient(A)` then `ConfigureChatClient(B)`, is A or B
   the outer-most client?
2. **`Configure` is a generic name** and risks colliding with
   `IServiceCollection.Configure`, `ChatClientBuilder.Configure`,
   and any third-party extension that also defines `Configure`.

## Decision

1. **The first `ConfigureChatClient` call is the outermost layer.**
   That is, when a request flows out, it traverses layers in
   registration order; when a response flows back, it traverses them
   in reverse. This matches `HttpClientFactory` semantics and is now
   documented + covered by a regression test
   (`ConfigureChatClient_FirstRegistered_IsOutermost`).
2. **Keep `Configure` as the primitive on the builder interface**
   (it is what provider extensions call), but ship a discoverable
   alias `ConfigureOptions(Action<SentirumAgentOptions>)` in
   `Sentirum.Agent.Abstractions`. Documentation pushes users toward
   the alias for clarity; built-in extensions
   (`WithInstructions`, `WithDescription`, `WithModel`, `WithTool`,
   `WithTools<T>`) continue to wrap the primitive.

## Consequences

- The ordering contract becomes part of the public API and any
  future change is a breaking change documented in `RELEASING.md`.
- IntelliSense in user code shows `ConfigureChatClient` and
  `ConfigureOptions` side-by-side, which makes the two pipelines
  obvious without reading docs.
- Provider extension authors still call `.Configure(...)`
  internally; we don't break source compatibility with anything
  built against M1-M3.

## Alternatives considered

- **Reverse the order (last registered is outer).** Less intuitive
  for users familiar with HTTP middleware.
- **Rename `Configure` to `ConfigureOptions` outright.** Source-
  breaking for the provider extensions already shipped in
  0.0.0-preview.
