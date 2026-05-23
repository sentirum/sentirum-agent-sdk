using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Sentirum.Agent.Workflows.HumanInTheLoop;
using Xunit;

namespace Sentirum.Agent.Workflows.HumanInTheLoop.Tests;

public class InMemoryApprovalChannelTests
{
    [Fact]
    public async Task PublishedRequest_IsObservableByWatcher()
    {
        await using var channel = new InMemoryApprovalChannel();
        var request = new ApprovalRequest(
            "g1",
            "Refund",
            "Refund 99.99 USD",
            new Dictionary<string, string> { ["amount"] = "99.99" },
            CorrelationId: "corr-1",
            RequestId: "req-1");

        await channel.PublishAsync(request);

        var observed = new List<ApprovalRequest>();
        var tcs = new TaskCompletionSource();
        var consumer = Task.Run(async () =>
        {
            await foreach (var req in channel.WatchRequestsAsync())
            {
                observed.Add(req);
                tcs.TrySetResult();
                break;
            }
        });

        await Task.WhenAny(tcs.Task, Task.Delay(1000));
        observed.Should().ContainSingle().Which.Should().Be(request);
        await consumer; // ensure clean exit
    }

    [Fact]
    public async Task ApproveAsync_PostsResponseObservableByConsumer()
    {
        await using var channel = new InMemoryApprovalChannel();

        await channel.ApproveAsync("g1", reviewer: "ops@sentirum", comment: "lgtm");

        var observed = new List<ApprovalResponse>();
        var tcs = new TaskCompletionSource();
        var consumer = Task.Run(async () =>
        {
            await foreach (var resp in channel.ConsumeAsync())
            {
                observed.Add(resp);
                tcs.TrySetResult();
                break;
            }
        });

        await Task.WhenAny(tcs.Task, Task.Delay(1000));
        observed.Should().ContainSingle().Which.Should().BeEquivalentTo(new ApprovalResponse(
            "g1", Approved: true, Reviewer: "ops@sentirum", Comment: "lgtm"));
        await consumer;
    }

    [Fact]
    public async Task RejectAsync_ProducesNonApprovedResponse()
    {
        await using var channel = new InMemoryApprovalChannel();

        await channel.RejectAsync("g2", reviewer: "ops@sentirum", comment: "nope");

        await foreach (var resp in channel.ConsumeAsync())
        {
            resp.Approved.Should().BeFalse();
            resp.GateId.Should().Be("g2");
            resp.Comment.Should().Be("nope");
            break;
        }
    }

    [Fact]
    public void ApprovalGate_CreateMintsRequestPortKeyedOnId()
    {
        var gate = ApprovalGate.Create("refund-approval");
        gate.Id.Should().Be("refund-approval");
        gate.Port.Should().NotBeNull();
        gate.Port.Id.Should().Be("refund-approval");
    }
}
