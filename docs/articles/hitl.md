# Human-in-the-Loop

The `Sentirum.Agent.Workflows.HumanInTheLoop` package adds typed approval gates
to Sentirum workflows. A gate suspends execution, ships the pending request to a
reviewer via a channel abstraction, and resumes once the verdict arrives.

## Setup

```bash
dotnet add package Sentirum.Agent.Workflows.HumanInTheLoop
```

## Core concepts

### ApprovalGate

An `ApprovalGate` wraps a MAF `RequestPort<ApprovalRequest, ApprovalResponse>`.
It is the opaque correlator that ties a pending approval request to its eventual
verdict.

```csharp
var gate = ApprovalGate.Create("refund-approval");
```

Each gate carries a stable `Id` that is surfaced on every `ApprovalRequest`
emitted from it, making it easy to route requests to the right reviewer queue.

### ApprovalDispatcher

The dispatcher drives a workflow that contains one or more `ApprovalGate`
instances against an `IApprovalChannel`. It:

1. Streams the workflow run.
2. Intercepts every `RequestInfoEvent` whose payload is an `ApprovalRequest`.
3. Forwards it to the channel.
4. Pumps matching `ApprovalResponse` envelopes back into the workflow.
5. Returns an `ApprovalRunResult` with all outputs, events, and forwarded
   requests.

```csharp
await using var channel = new InMemoryApprovalChannel();
var result = await ApprovalDispatcher.RunAsync(workflow, request, channel);
```

The dispatcher is intentionally agnostic about the channel implementation.
`InMemoryApprovalChannel` is fine for tests; production hosts plug their own
transport (Slack, Teams, a custom approval UI).

## Wiring an approval gate into a workflow

Use `WithApprovalGate` to insert a gate between a source executor and a
downstream step:

```csharp
using Sentirum.Agent.Workflows.HumanInTheLoop;

var gate = ApprovalGate.Create("refund-approval");

var (builder, downstream) = new WorkflowBuilder(intake)
    .AddEdge(intake, recommender)
    .WithApprovalGate<RefundRecommendation, string>(
        recommender,
        gate,
        requestComposer: rec => new ApprovalRequest(
            gate.Id,
            "Refund approval",
            rec.Summary,
            new Dictionary<string, string> { ["amount"] = rec.Amount.ToString() },
            CorrelationId: string.Empty,
            RequestId: string.Empty),
        onApproved: outcome => $"Approved by {outcome.Reviewer}",
        onRejected: outcome => $"Rejected by {outcome.Reviewer}: {outcome.Comment}");
```

The pattern builds this topology:

```
source ▶ requestComposer ▶ port ◀▶ outcomeProjector ▶ downstream
```

- **requestComposer** — projects the source payload onto an `ApprovalRequest`.
- **port** — the gate's `RequestPort`; suspends the run until a reviewer
  responds.
- **outcomeProjector** — translates the `ApprovalOutcome` (approved or rejected)
  back into the downstream message type.

## IApprovalChannel

Implement `IApprovalChannel` to connect the dispatcher to your review transport:

```csharp
public interface IApprovalChannel
{
    IAsyncEnumerable<ApprovalResponse> GetResponsesAsync(
        CancellationToken ct);

    Task SubmitRequestAsync(
        ApprovalRequest request,
        CancellationToken ct);
}
```

`InMemoryApprovalChannel` ships in the box for single-process scenarios and
unit tests. Implement your own for Slack, Microsoft Teams, email, or a custom
web UI.

## End-to-end example

```csharp
// 1. Create the gate.
var gate = ApprovalGate.Create("order-review");

// 2. Build a workflow with the gate.
var workflow = SentirumWorkflowBuilder
    .Create("order-flow")
    .Sequential(classifier, approver, responder)
    // ... wire WithApprovalGate on the MAF WorkflowBuilder ...
    .Build();

// 3. Run against a channel.
await using var channel = new InMemoryApprovalChannel();
var runTask = ApprovalDispatcher.RunAsync(workflow, input, channel);

// 4. Reviewer approves (or rejects) on the same channel.
await channel.RespondAsync(new ApprovalResponse(
    gate.Id, ApprovalOutcome.Approved, "ops@sentirum", "Looks good"));

var result = await runTask;
```

## ApprovalRequest and ApprovalResponse

| Type | Key fields |
| --- | --- |
| `ApprovalRequest` | `GateId`, `Title`, `Summary`, `Context` (dictionary), `CorrelationId` |
| `ApprovalResponse` | `GateId`, `Outcome` (Approved/Rejected), `Reviewer`, `Comment` |
| `ApprovalOutcome` | `Approved`, `Rejected`, plus `Reviewer` and `Comment` |

The `ApprovalRunResult` record returned by the dispatcher carries:

- `Outputs` — all workflow outputs in emission order.
- `Events` — complete workflow event log.
- `Status` — final `RunStatus`.
- `ApprovalRequests` — every `ApprovalRequest` forwarded to the channel (useful
  for assertions and audit trails).
