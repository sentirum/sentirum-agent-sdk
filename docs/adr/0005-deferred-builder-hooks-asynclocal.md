# ADR-0005: Deferred builder hooks via AsyncLocal service-provider accessor

- **Status:** Accepted (with planned revisit before v1.0)
- **Date:** 2026-05-23
- **Applies to:** Sentirum 0.1.0-preview onward

## Context

Builder extensions sometimes need to resolve a DI service while the
options pipeline runs. The driving case is `WithTools<TToolset>()`
in `Sentirum.Agent.Tools.Core`: the toolset instance lives in DI so
it can take constructor dependencies, but `ToolDiscovery` has to
produce `AIFunction` instances against that instance during the
options pipeline — which runs inside `SentirumAgentFactory.Create`,
long after the `ServiceCollection` is sealed.

`SentirumAgentBuilder` cannot pass the `IServiceProvider` to the
deferred `Action<SentirumAgentOptions>` callbacks without changing
the public interface signature.

## Decision

Introduce `SentirumServiceProviderAccessor` (an `AsyncLocal`
carrier) in `Sentirum.Agent.Builder` (Core package).
`SentirumAgentFactory.Create` pushes the active service provider
onto the accessor for the duration of the options pipeline, then
restores the previous value. Builder extensions that need DI read
`SentirumServiceProviderAccessor.Current`.

This is an explicit, controlled use of `AsyncLocal`:

- Only `SentirumAgentFactory.Create` calls `Push`.
- The returned `IDisposable` always restores the previous value, so
  nested factory calls don't bleed into siblings.
- Extensions that run outside the factory observe `null` and must
  fall back to direct construction.

## Consequences

- `WithTools<T>()`, future `WithMemoryStore<T>()`,
  `WithKnowledgeBase(...)`, etc. can take DI dependencies without
  forcing users to register an `IServiceProvider` themselves.
- Builder API stays stable: no signature changes to
  `ISentirumAgentBuilder.Configure`.
- We accept the well-known smell of an AsyncLocal service-locator
  in exchange for a much smaller surface than the alternative
  (passing `(options, sp)` everywhere and rewriting every
  extension method).

### Planned revisit before v1.0

If we see issues with circular resolution, threading surprises, or
the surface needs to grow (e.g. async builders), revisit by adding
an internal `SentirumAgentBuildContext` type that is passed to
deferred callbacks instead of using `AsyncLocal`. The `WithXxx<T>()`
extensions can switch over without breaking user code.

## Alternatives considered

- **Force users to capture services in the lambda:**
  `WithTools<T>(sp.GetRequiredService<T>())`. Awkward and breaks
  the natural usage pattern.
- **Add a second `Configure((options, sp) => ...)` overload.**
  Doubles the surface and every provider extension must pick an
  overload.
- **Service-locate via a global `ServiceProvider.Current`.** Worse
  than scoped AsyncLocal because it leaks across requests.
