# Workflows

The `Sentirum.Agent.Workflows` package provides ergonomic wrappers around
Microsoft Agent Framework's `Microsoft.Agents.AI.Workflows` engine. Compose
multi-agent orchestrations as one-liners over `ISentirumAgent`.

## Setup

```bash
dotnet add package Sentirum.Agent.Workflows
```

Register the workflow services:

```csharp
using Sentirum.Agent.Workflows;

services.AddSentirumWorkflows();
```

## Sequential

Agents run head-to-tail. Each agent receives the previous agent's output as its
input.

```csharp
var classifier = /* resolve an ISentirumAgent */;
var responder  = /* resolve an ISentirumAgent */;

var workflow = SentirumWorkflowBuilder
    .Create("triage")
    .Sequential(classifier, responder)
    .Build();

var result = await workflow.RunAsync(new List<ChatMessage>
{
    new(ChatRole.User, "My order arrived damaged"),
});
```

Use case: classify-then-respond pipelines, summarization chains, multi-step
data extraction.

## Concurrent

All agents run in parallel against the same input. An aggregator function
collapses each branch's output into a single merged conversation.

```csharp
var sentiment  = /* ISentirumAgent */;
var categorize = /* ISentirumAgent */;

var workflow = SentirumWorkflowBuilder
    .Create("parallel-analysis")
    .Concurrent(
        new[] { sentiment, categorize },
        branches =>
        {
            // Merge all branch outputs into one conversation.
            var merged = branches
                .SelectMany(b => b)
                .ToList();
            return merged;
        })
    .Build();
```

Use case: parallel analysis (sentiment + category + language detection),
independent specialist agents that each contribute a perspective.

## ConcurrentJoin

A convenience over `Concurrent` that joins all branch outputs into a single
assistant message, separated by a delimiter (default `\n\n`).

```csharp
var billing   = /* ISentirumAgent */;
var technical = /* ISentirumAgent */;

var workflow = SentirumWorkflowBuilder
    .Create("support-triage")
    .ConcurrentJoin(new[] { billing, technical }, separator: "\n---\n")
    .Build();

var result = await workflow.RunAsync(new List<ChatMessage>
{
    new(ChatRole.User, "I was charged twice and the app keeps crashing"),
});
```

Use case: triage scenarios where each branch produces independently useful output
and the caller wants the full set in one envelope.

## Handoff

An initial agent delegates the conversation to a specialist via tool calls. The
framework manages the routing transparently using MAF's handoff workflow.

```csharp
var triage      = /* ISentirumAgent — the front door */;
var billing     = /* ISentirumAgent — billing specialist */;
var technical   = /* ISentirumAgent — technical specialist */;

var workflow = SentirumWorkflowBuilder
    .Create("support-handoff")
    .Handoff(triage, billing, technical)
    .Build();
```

Use case: "triage + specialists" topologies where the triage agent inspects the
request and routes to the right specialist.

## Advanced: UseWorkflow

For topologies that don't fit the first-class shapes (switch routing, fan-in
barriers, sub-workflows, custom executors), build a MAF `Workflow` directly and
pass it to `UseWorkflow`:

```csharp
var mafWorkflow = new WorkflowBuilder(...)
    .AddEdge(...)
    .WithOutputFrom(...)
    .Build();

var workflow = SentirumWorkflowBuilder
    .Create("custom")
    .UseWorkflow(mafWorkflow)
    .Build();
```

The Sentirum wrapper still provides the `RunAsync` / `StreamAsync` surface but
does not auto-send `TurnToken` for custom workflows — you control dispatch
directly.

## Running a workflow

All topologies share the same execution surface:

```csharp
// Run to completion.
var result = await workflow.RunAsync(input);

// Stream events as they are produced.
await foreach (var evt in workflow.StreamAsync(input))
{
    Console.WriteLine(evt);
}
```

`ISentirumWorkflow.RunAsync` returns a `SentirumWorkflowResult` containing the
aggregated outputs, the full event log, and the final run status.
