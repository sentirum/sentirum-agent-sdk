using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Agents.AI.Workflows;
using Sentirum.Agent.Workflows;
using Sentirum.Agent.Workflows.HumanInTheLoop;
using Xunit;

namespace Sentirum.Agent.Workflows.HumanInTheLoop.Tests;

public class ApprovalDispatcherTests
{
    [Fact]
    public async Task ApprovedGate_FlowsThroughDownstreamAsApproved()
    {
        var gate = ApprovalGate.Create("refund-gate");
        var workflow = BuildRefundLikeWorkflow(gate);

        await using var channel = new InMemoryApprovalChannel();

        var resultTask = ApprovalDispatcher.RunAsync(workflow, 99.99m, channel);

        // Reviewer arrives shortly after the dispatcher publishes.
        _ = Task.Run(async () =>
        {
            await foreach (var req in channel.WatchRequestsAsync())
            {
                await channel.ApproveAsync(req.GateId, reviewer: "ops", comment: "lgtm", requestId: req.RequestId, context: req.Context);
                break;
            }
        });

        var result = await resultTask;

        result.Status.Should().BeOneOf(RunStatus.Idle, RunStatus.Ended);
        result.Events.OfType<WorkflowErrorEvent>().Should().BeEmpty();
        result.ApprovalRequests.Should().ContainSingle().Which.GateId.Should().Be("refund-gate");
        result.Outputs.Should().ContainSingle().Which.Should().Be("APPROVED:99.99");
    }

    [Fact]
    public async Task RejectedGate_FlowsThroughDownstreamAsRejected()
    {
        var gate = ApprovalGate.Create("refund-gate");
        var workflow = BuildRefundLikeWorkflow(gate);

        await using var channel = new InMemoryApprovalChannel();

        var resultTask = ApprovalDispatcher.RunAsync(workflow, 99.99m, channel);

        _ = Task.Run(async () =>
        {
            await foreach (var req in channel.WatchRequestsAsync())
            {
                await channel.RejectAsync(req.GateId, reviewer: "ops", comment: "too high", requestId: req.RequestId, context: req.Context);
                break;
            }
        });

        var result = await resultTask;

        result.Outputs.Should().ContainSingle().Which.Should().Be("REJECTED:99.99");
    }

    [Fact]
    public async Task ApprovalRequest_CarriesContextFromComposer()
    {
        var gate = ApprovalGate.Create("refund-gate");
        var workflow = BuildRefundLikeWorkflow(gate);

        await using var channel = new InMemoryApprovalChannel();
        var resultTask = ApprovalDispatcher.RunAsync(workflow, 250m, channel);

        _ = Task.Run(async () =>
        {
            await foreach (var req in channel.WatchRequestsAsync())
            {
                req.Title.Should().Be("Refund approval");
                req.Summary.Should().Contain("250");
                req.Context.Should().ContainKey("amount").WhoseValue.Should().Be("250");
                await channel.ApproveAsync(req.GateId, reviewer: "ops", requestId: req.RequestId, context: req.Context);
                break;
            }
        });

        await resultTask;
    }

    /// <summary>
    /// Builds the smallest meaningful HITL workflow:
    /// <c>decimal ▶ (compose+request) ▶ port ◀▶ (project to string)</c>.
    /// Mimics a refund pipeline where the source executor produces a
    /// decimal amount, the gate asks a human, and the projector emits a
    /// final string verdict.
    /// </summary>
    private static SentirumWorkflow BuildRefundLikeWorkflow(ApprovalGate gate)
    {
        // Source executor: passes the decimal amount through unchanged.
        var source = ((Func<decimal, IWorkflowContext, ValueTask<decimal>>)(
            (amount, _) => new ValueTask<decimal>(amount)))
            .BindAsExecutor(id: "source", options: null, threadsafe: true);

        var builder = new WorkflowBuilder(source);

        var (_, downstream) = builder.WithApprovalGate<decimal, string>(
            source,
            gate,
            requestComposer: amount => new ApprovalRequest(
                gate.Id,
                "Refund approval",
                $"Approve refund for ${amount}?",
                new Dictionary<string, string> { ["amount"] = amount.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                CorrelationId: string.Empty,
                RequestId: string.Empty),
            onApproved: outcome => $"APPROVED:{outcome.Request.Context["amount"]}",
            onRejected: outcome => $"REJECTED:{outcome.Request.Context["amount"]}");

        // Terminal executor: returns the projected string so the framework
        // can auto-yield it as a workflow output (default options enable
        // AutoYieldOutputHandlerResultObject for the TInput,TOutput
        // overload).
        var sink = ((Func<string, IWorkflowContext, ValueTask<string>>)((verdict, _) =>
            new ValueTask<string>(verdict)))
            .BindAsExecutor<string, string>(id: "sink", options: null, threadsafe: true);

        builder.AddEdge(downstream, sink);
        builder.WithOutputFrom(sink);

        var maf = builder.Build(validateOrphans: true);
        return new SentirumWorkflow("refund-flow", "refund-flow", maf);
    }
}
