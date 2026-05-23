# ADR-0011: Workflows + Human-in-the-Loop design

- **Status:** Accepted
- **Date:** 2026-05-23
- **Applies to:** `Sentirum.Agent.Workflows` and
  `Sentirum.Agent.Workflows.HumanInTheLoop` 0.1.0-preview onward

## Context

M5 introduces multi-agent orchestration and human-in-the-loop (HITL)
approval gates on top of Microsoft Agent Framework (MAF)
`Microsoft.Agents.AI.Workflows` 1.6.2. Three design questions had to be
answered before the public API hardened:

1. **How much do we wrap MAF?** MAF ships a rich set of builders
   (`WorkflowBuilder`, `AgentWorkflowBuilder`, `HandoffWorkflowBuilder`,
   `MagenticWorkflowBuilder`, ...). A thin wrapper risks leaking MAF
   types; a thick wrapper risks getting in the way of advanced users.
2. **Who owns the `TurnToken` dance?** MAF agent executors cache
   incoming messages and only fire when they see a `TurnToken`. Custom
   executors do not. Callers should not have to remember when to send
   a token, and we must not break custom workflows by sending one
   unconditionally.
3. **How does the HITL gate plug into the workflow?** MAF exposes
   `RequestPort<TReq, TResp>` for external pause-and-resume; the
   request/response payload model and the dispatcher contract are
   ours to design.

## Decision

### A. Wrapping strategy — escape-hatch first

`Sentirum.Agent.Workflows` ships two surfaces:

- **`SentirumWorkflowBuilder`** — fluent DSL covering the common
  shapes:
  - `Sequential(...)` over `AgentWorkflowBuilder.BuildSequential`.
  - `Concurrent(agents, aggregator)` + `ConcurrentJoin` convenience
    over `AgentWorkflowBuilder.BuildConcurrent`.
  - `Handoff(initial, ...targets)` over `AgentWorkflowBuilder.CreateHandoffBuilderWith`.
  - `UseWorkflow(maf)` escape hatch that accepts any pre-built
    `Workflow` (used for switches, fan-in barriers, sub-workflows,
    HITL gates, anything custom).
- **`ISentirumWorkflow`** — minimal run surface:
  - `RunAsync<TInput>(input, sessionId?, ct)` → aggregated result.
  - `StreamAsync<TInput>(...)` → raw `WorkflowEvent` stream.
  - `InnerWorkflow` getter for advanced consumers who need MAF-native
    APIs (visualization, custom hosting, checkpoints).

Rationale: wrap the 80% case; let the remaining 20% drop straight to
MAF without forking the SDK. We deliberately did **not** wrap
`GroupChatWorkflowBuilder` or `MagenticWorkflowBuilder` in v0.1;
they are reachable via `UseWorkflow` and earn their own DSL once we
have shipping samples that need them.

### B. `TurnToken` dispatch — typed mode flag

`SentirumWorkflowBuilder` tracks a `WorkflowDispatchMode`:

| Builder method                                             | Mode             |
| ---------------------------------------------------------- | ---------------- |
| `Sequential`, `Concurrent`, `ConcurrentJoin`, `Handoff`    | `AgentTurn`      |
| `UseWorkflow(maf)`                                         | `Direct`         |

`SentirumWorkflow.RunAsync` / `StreamAsync` send a single
`TurnToken(emitEvents: true)` after queuing the input **only** when the
mode is `AgentTurn`. The token wakes MAF's cached-message pipeline so
agent executors fire; custom workflows route their input directly and
must not see a stray token (it would land on an untyped executor and
either deadlock or fail validation).

For mixed workflows (custom topology that embeds an MAF agent stage),
`ApprovalDispatcher.RunAsync` exposes an opt-in
`sendAgentTurnToken: true` flag. Sample 08 shows the contract.

Rationale: making the token implicit for the common shapes keeps the
high-level DSL ergonomic; making it explicit for custom workflows
preserves correctness for the advanced ones.

### C. HITL gate — typed request port + dispatcher

The HITL package introduces four primitives:

- **`ApprovalRequest`** / **`ApprovalResponse`** / **`ApprovalOutcome`**
  — JSON-serializable payloads keyed on `GateId`. Keeping the payload
  free of MAF-specific types means the request can cross process
  boundaries (Slack, Teams, HTTP) without dragging the workflow
  runtime along.
- **`ApprovalGate`** — pairs a stable `Id` with a typed
  `RequestPort<ApprovalRequest, ApprovalResponse>`. `Create(id)` is
  the canonical factory.
- **`ApprovalGateWorkflowBuilderExtensions.WithApprovalGate<TSource, TDownstream>`**
  — wires the smallest meaningful topology around the gate:
  `source ▶ composer ▶ port ◀▶ projector ▶ downstream`, where
  `composer` turns the upstream payload into an `ApprovalRequest` and
  `projector` translates the response (split across `onApproved` /
  `onRejected` callbacks) into the downstream message type.
- **`IApprovalChannel`** + **`InMemoryApprovalChannel`** — pluggable
  reviewer transport. The dispatcher publishes pending requests to
  the channel and pumps matching responses back into the workflow.
  In-memory channel is for tests, samples, and single-process demos;
  production hosts swap in their own transport.

`ApprovalDispatcher.RunAsync` is the only place that knows how to
correlate a `RequestInfoEvent` → channel → `ExternalResponse`. It
maintains two indexes:

- `requestId → ExternalRequest` so the response envelope can call
  `externalRequest.CreateResponse(response)`.
- `gateId → queue of pending requestIds` so responses route to the
  right pending request when a gate fires more than once.

Rationale: keep the workflow definition pure (`WithApprovalGate` is a
single fluent call) and concentrate the side-effecty wiring in the
dispatcher. Custom reviewer transports plug in by implementing one
two-method interface.

## Consequences

- The 80% workflow shapes read as one-liners; the 20% advanced
  shapes drop to MAF without ceremony.
- HITL gates compose with sequential, concurrent, and custom
  workflows uniformly because they live at the `WorkflowBuilder`
  level, not inside `SentirumWorkflowBuilder`.
- The `TurnToken` rule (agent workflows yes, custom workflows no, hybrid
  workflows opt in) is documented on the XML of every relevant API.
- We accept a v0.1 limitation: no built-in checkpoint store wrapper
  yet. Consumers who want pause-across-process-restarts use MAF's
  `CheckpointManager` directly via `InnerWorkflow`. Adding a Sentirum
  wrapper is non-breaking when we have a shipping scenario.

## Alternatives considered

- **Pure MAF re-export.** Rejected because the `TurnToken` dance is a
  real foot-gun and the agent/custom dispatch boundary should be
  encoded in the type system, not in tribal knowledge.
- **HITL gate as a special Sentirum agent.** Considered briefly so
  HITL would compose with `Sequential`. Rejected because a gate is
  fundamentally a pause point, not an agent — folding it into the
  `AIAgent` interface would either lie about the surface
  (`InnerAgent` would have to be non-null but never used) or force
  every consumer to handle a gate-shaped `AIAgent` they did not ask
  for.
- **`RequestPort` keyed on `Guid`.** Easier to mint, but the gate id
  doubles as a stable correlation key visible to reviewers
  (Slack message id, audit log row); keeping it a human-supplied
  string is better DX for the 95% case.
- **Send `TurnToken` unconditionally.** Considered for ergonomics;
  rejected because it breaks every custom workflow that does not
  declare `TurnToken` as a valid input. The mode-flag approach pays
  one boolean and removes the foot-gun.
