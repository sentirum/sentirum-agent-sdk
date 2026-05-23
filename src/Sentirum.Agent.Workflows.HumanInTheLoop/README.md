# Sentirum.Agent.Workflows.HumanInTheLoop

Approval gates for `Sentirum.Agent.Workflows`. Pause a workflow on a
typed `RequestPort<ApprovalRequest, ApprovalResponse>`, ship the
pending request to a reviewer transport, and resume once the verdict
arrives.

```csharp
var gate = ApprovalGate.Create("refund-approval");

var (builder, downstream) = new WorkflowBuilder(intake)
    .AddEdge(intake, recommender)
    .WithApprovalGate<RefundRecommendation, string>(
        recommender,
        gate,
        requestComposer: rec => new ApprovalRequest(
            gate.Id, "İade onayı", rec.Summary,
            new Dictionary<string, string> { ["amount"] = rec.Amount.ToString() },
            CorrelationId: string.Empty, RequestId: string.Empty),
        onApproved: outcome => $"✅ {outcome.Reviewer} onayladı",
        onRejected: outcome => $"❌ {outcome.Reviewer} reddetti: {outcome.Comment}");

// Run against any IApprovalChannel.
await using var channel = new InMemoryApprovalChannel();
var result = await ApprovalDispatcher.RunAsync(workflow, request, channel);
```

`InMemoryApprovalChannel` covers tests and single-process samples;
plug your own `IApprovalChannel` for Slack / Teams / a custom approval
UI. See ADR-0011 for the design rationale and the `08-HITL` sample
for an end-to-end policy-bot reviewer.
