# Sentirum.Agent.Workflows

Ergonomic wrapper around Microsoft Agent Framework's
`Microsoft.Agents.AI.Workflows` engine. Compose multi-agent
orchestrations as one-liners over `ISentirumAgent`.

```csharp
var triage = SentirumWorkflowBuilder.Create("triage")
    .Sequential(classifier, responder)
    .Build();

var result = await triage.RunAsync(new List<ChatMessage>
{
    new(ChatRole.User, "Sipariş hasarlı geldi"),
});
```

Three shapes are first-class — `Sequential`, `Concurrent` /
`ConcurrentJoin`, and `Handoff` — built on top of MAF's
`AgentWorkflowBuilder`. Anything more exotic (switches, fan-in
barriers, custom executors, sub-workflows) drops through
`UseWorkflow(maf)` without leaving the Sentirum surface.

The wrapper sends MAF's `TurnToken` automatically for agent workflows
so callers do not have to track when to wake the cached-message
pipeline; custom workflows are left untouched. See ADR-0011 for the
design rationale.

Pair with `Sentirum.Agent.Workflows.HumanInTheLoop` to add approval
gates that pause the run on a typed `RequestPort` until a reviewer
verdicts.
