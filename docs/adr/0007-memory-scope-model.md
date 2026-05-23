# ADR-0007: Memory scope model — opaque string store, strict partitions

- **Status:** Accepted
- **Date:** 2026-05-23
- **Applies to:** `Sentirum.Agent.Memory.*` 0.1.0-preview onward

## Context

The M4 memory layer ships three back-ends (InMemory, Redis, EF Core)
behind a single `ISentirumMemoryStore` interface. Two design choices
shape the entire surface and are hard to reverse once customers depend
on them:

1. **Opaque string values vs. typed payloads.** Should the store be
   `Get/SetAsync<T>` (typed, generic) or `Get/SetAsync(string)`
   (opaque)?
2. **Partition addressing model.** What are the valid combinations of
   `(scope, agentId, userId, sessionId)` and how strictly are they
   enforced?

The M4 reviewer (`zai-anthropic/glm-5`) flagged the second question as
a critical correctness bug: `MemoryPartition.Validate()` allowed
over-specified partitions like
`new MemoryPartition(MemoryScope.Global, AgentId: "x")` which silently
mis-partitioned data across back-ends (Redis ignored the extra id, EF
Core indexed it).

## Decision

### Opaque strings

`ISentirumMemoryStore.SetAsync` and `GetAsync` take and return
`string`. Callers serialize their own payloads. `SetJsonAsync<T>` /
`GetJsonAsync<T>` ship as opt-in extensions over `System.Text.Json`
with `JsonSerializerDefaults.Web` defaults.

Rationale:

- The wire format stays neutral across InMemory, Redis, and EF Core
  back-ends. Switching back-ends never forces a re-serialization step.
- Callers who need MessagePack, protobuf, or CBOR plug their own
  serializer in without dragging the SDK along.
- A typed store would force a serializer choice into the abstraction
  layer (which `Sentirum.Agent.Memory.Abstractions` deliberately
  keeps tiny — no `Microsoft.Extensions.AI` dependency, no DI
  dependency by default).

### Strict partition validation

`MemoryPartition.Validate()` now rejects over-specified partitions in
addition to under-specified ones:

- `Global` — no agent / user / session id allowed.
- `Agent` — `AgentId` required, no user / session.
- `User` — `UserId` required, no agent / session.
- `Session` — `SessionId` required, no agent / user.

Composite scopes (`agent+user`, `user+session`, etc.) are intentionally
**not** supported in v0.1. Adding a new `MemoryScope` value plus the
back-end key-building logic is a non-breaking change; allowing
arbitrary id combinations and then narrowing them later is breaking.

### Expired-row cleanup

`GetAsync` is side-effect free across all back-ends — expired entries
are filtered out but never deleted from a read path. Lazy cleanup on
read mutates the change tracker in EF Core and creates surprising
behavior inside ambient transactions. A background sweeper job is out
of scope for v0.1; consumers who care about disk reclamation either
schedule one against the underlying store or call `ClearAsync` /
`DeleteAsync` explicitly.

## Consequences

- The memory abstractions package has zero dependencies on the rest of
  Sentirum (the M4 reviewer also flagged an unused
  `Sentirum.Agent.Abstractions` reference; removed in this milestone).
- Adding new scopes (`AgentUser`, `UserSession`) is a deliberate
  decision tracked by a future ADR; today's API stays minimal.
- Distributed implementations must enforce the same `Validate()`
  contract or risk diverging from in-process behavior.

## Alternatives considered

- **Typed `Get/SetAsync<T>`.** Rejected because it bakes a serializer
  choice into every back-end and breaks wire compatibility across
  store implementations.
- **Discriminated partition hierarchy
  (`GlobalPartition` / `AgentPartition` / ...).** Marginally safer at
  the type level but doubles the API surface and forces every store
  to pattern-match on partition kind. The flat record with `Validate()`
  is the cheaper safe-by-construction option.
- **Lazy delete-on-read.** Implemented in the InMemory back-end where
  it is harmless; rejected for EF Core where it pollutes the change
  tracker and breaks ambient transactions.
