using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Sentirum.Agent.Workflows.Tests.Fakes;
using Xunit;

namespace Sentirum.Agent.Workflows.Tests;

public class SentirumWorkflowBuilderTests
{
    [Fact]
    public void Build_WithoutTopology_Throws()
    {
        var act = () => SentirumWorkflowBuilder.Create("wf").Build();
        act.Should().Throw<InvalidOperationException>().WithMessage("*topology*");
    }

    [Fact]
    public void Sequential_WithEmptyList_Throws()
    {
        var act = () => SentirumWorkflowBuilder.Create("wf").Sequential(Array.Empty<ISentirumAgent>());
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Handoff_WithNullTarget_Throws()
    {
        var initial = new SentirumAgentWrapper(new EchoAgent("a", "a!"));
        var act = () => SentirumWorkflowBuilder.Create("wf").Handoff(initial, (ISentirumAgent)null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Handoff_WithNoTargets_Throws()
    {
        var initial = new SentirumAgentWrapper(new EchoAgent("a", "a!"));
        var act = () => SentirumWorkflowBuilder.Create("wf").Handoff(initial);
        act.Should().Throw<ArgumentException>().WithMessage("*at least one*");
    }

    [Fact]
    public async Task Sequential_PipelineRunsAgentsHeadToTail()
    {
        var a = new EchoAgent("a", "[A]");
        var b = new EchoAgent("b", "[B]");
        var workflow = SentirumWorkflowBuilder.Create("seq")
            .Sequential(new SentirumAgentWrapper(a), new SentirumAgentWrapper(b))
            .Build();

        var result = await workflow.RunAsync(new List<ChatMessage>
        {
            new(ChatRole.User, "hi"),
        });

        a.Invocations.Should().Be(1);
        b.Invocations.Should().Be(1);
        result.Status.Should().Be(RunStatus.Idle).And.NotBe(RunStatus.NotStarted);
        result.Outputs.Should().NotBeEmpty();
        // The final transcript should carry both suffixes.
        var transcriptOutput = result.Outputs
            .OfType<IEnumerable<ChatMessage>>()
            .SelectMany(x => x)
            .Select(m => m.Text)
            .Aggregate(string.Empty, (acc, t) => acc + " " + t)
            .Trim();
        transcriptOutput.Should().Contain("[A]").And.Contain("[B]");
    }

    [Fact]
    public async Task Concurrent_AggregatesEveryBranchOutput()
    {
        var a = new EchoAgent("a", "[A]");
        var b = new EchoAgent("b", "[B]");
        var workflow = SentirumWorkflowBuilder.Create("conc")
            .ConcurrentJoin(new[]
            {
                new SentirumAgentWrapper(a),
                new SentirumAgentWrapper(b),
            })
            .Build();

        var result = await workflow.RunAsync(new List<ChatMessage>
        {
            new(ChatRole.User, "hi"),
        });

        a.Invocations.Should().Be(1);
        b.Invocations.Should().Be(1);
        result.Outputs.Should().NotBeEmpty();
        var joined = result.Outputs
            .OfType<IEnumerable<ChatMessage>>()
            .SelectMany(x => x)
            .Select(m => m.Text)
            .Aggregate(string.Empty, (acc, t) => acc + " " + t);
        joined.Should().Contain("[A]").And.Contain("[B]");
    }

    [Fact]
    public void UseWorkflow_HandsExternalMafWorkflowThrough()
    {
        // Build a trivial MAF workflow externally and verify the
        // Sentirum wrapper exposes it untouched. We don't drive it
        // through RunAsync here because the wrapper appends a TurnToken
        // that is meaningful for agent workflows only; custom workflows
        // have their own dispatch story and are tested in MAF directly.
        var executor = ((Func<int, IWorkflowContext, ValueTask>)(async (i, ctx) =>
            await ctx.YieldOutputAsync(i, default)))
            .BindAsExecutor(id: "increment", options: null, threadsafe: true);

        var custom = new WorkflowBuilder(executor)
            .WithOutputFrom(executor)
            .Build(validateOrphans: true);

        var workflow = SentirumWorkflowBuilder.Create("custom")
            .UseWorkflow(custom)
            .Build();

        workflow.InnerWorkflow.Should().BeSameAs(custom);
    }

    [Fact]
    public async Task RunAsync_StreamCarriesAllEvents()
    {
        var a = new EchoAgent("a", "[A]");
        var workflow = SentirumWorkflowBuilder.Create("stream")
            .Sequential(new SentirumAgentWrapper(a))
            .Build();

        var events = new List<WorkflowEvent>();
        await foreach (var evt in workflow.StreamAsync(new List<ChatMessage>
        {
            new(ChatRole.User, "hi"),
        }))
        {
            events.Add(evt);
        }

        events.Should().NotBeEmpty();
        // Workflow drove the single agent executor to completion, so
        // its ExecutorCompletedEvent must surface in the stream.
        events.OfType<ExecutorCompletedEvent>().Should().NotBeEmpty();
    }

    [Fact]
    public void DiagnosticEventsExposedForAdvancedConsumers()
    {
        var a = new EchoAgent("a", "[A]");
        var workflow = (SentirumWorkflow)SentirumWorkflowBuilder.Create("diag")
            .Sequential(new SentirumAgentWrapper(a))
            .Build();

        workflow.InnerWorkflow.Should().NotBeNull();
        workflow.Id.Should().Be("diag");
        workflow.Name.Should().Be("diag");
    }
}
