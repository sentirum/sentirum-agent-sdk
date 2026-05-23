using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Sentirum.Agent.Sessions.Tree;
using Xunit;

namespace Sentirum.Agent.Sessions.Tree.Tests;

public sealed class InMemoryTreeSessionStoreTests
{
    private static (ITreeSessionStore Store, ISentirumAgent Agent) BuildHost()
    {
        var services = new ServiceCollection();
        services.AddSentirumTreeSessions();
        services.AddSentirumAgent("test", b => b
            .UseChatClient(_ => new FakeChatClient()));

        var sp = services.BuildServiceProvider();
        var store = sp.GetRequiredService<ITreeSessionStore>();
        var agent = sp.GetRequiredService<ISentirumAgentRegistry>().Find("test")!;
        return (store, agent);
    }

    [Fact]
    public async Task ForkAsync_ProducesIsolatedDeepCopy()
    {
        var (store, agent) = BuildHost();

        var parent = await store.CreateAsync(agent.Id);
        await agent.RunAsync(parent, new ChatMessage(ChatRole.User, "first"));

        var fork = await store.ForkAsync(parent);

        fork.ParentId.Should().Be(parent.Id);
        fork.Id.Should().NotBe(parent.Id);
        ReferenceEquals(fork.InnerSession, parent.InnerSession).Should().BeFalse(
            "ForkAsync must deep-copy the inner AgentSession.");

        // Mutating the fork must not bleed into the parent.
        await agent.RunAsync(fork, new ChatMessage(ChatRole.User, "second-on-fork"));

        var diff = await store.CompareAsync(parent, fork);
        diff.LeftMessageCount.Should().BeLessThan(diff.RightMessageCount);
    }

    [Fact]
    public async Task GetTreeAsync_RebuildsHierarchyAndAllowsAsciiRender()
    {
        var (store, agent) = BuildHost();

        var root = await store.CreateAsync(agent.Id);
        var a = await store.ForkAsync(root);
        var b = await store.ForkAsync(root);
        var c = await store.ForkAsync(a);

        var tree = await store.GetTreeAsync(c.Id);   // any node works

        tree.Root.Session.Id.Should().Be(root.Id);
        tree.Root.Children.Should().HaveCount(2);
        tree.WalkBreadthFirst().Should().HaveCount(4);

        var ascii = tree.ToAsciiTree();
        ascii.Should().Contain(root.Id).And.Contain(a.Id).And.Contain(b.Id).And.Contain(c.Id);
    }

    [Fact]
    public async Task MergeAsync_TransfersNewMessagesFromSourceToTarget()
    {
        var (store, agent) = BuildHost();

        var parent = await store.CreateAsync(agent.Id);
        await agent.RunAsync(parent, new ChatMessage(ChatRole.User, "shared"));

        var fork = await store.ForkAsync(parent);
        await agent.RunAsync(fork, new ChatMessage(ChatRole.User, "fork-only"));

        var before = await store.CompareAsync(parent, fork);
        before.MessageCountDelta.Should().BeNegative("fork has extra messages before merge.");

        await store.MergeAsync(source: fork, target: parent);

        var after = await store.CompareAsync(parent, fork);
        after.MessageCountDelta.Should().Be(0, "after merge the timelines should align.");
    }

    [Fact]
    public async Task ForkAsync_Concurrent_AllChildrenAppearInTree()
    {
        var (store, agent) = BuildHost();
        var parent = await store.CreateAsync(agent.Id);
        await agent.RunAsync(parent, new ChatMessage(ChatRole.User, "seed"));

        const int ForkCount = 32;
        var tasks = new Task<ISentirumSession>[ForkCount];
        for (var i = 0; i < ForkCount; i++)
        {
            tasks[i] = Task.Run(() => store.ForkAsync(parent));
        }
        await Task.WhenAll(tasks);

        var tree = await store.GetTreeAsync(parent.Id);
        tree.Root.Children.Should().HaveCount(
            ForkCount,
            "concurrent ForkAsync calls on the same parent must never lose children.");
    }

    [Fact]
    public async Task MergeAsync_WrongDirection_Throws()
    {
        var (store, agent) = BuildHost();
        var root = await store.CreateAsync(agent.Id);
        await agent.RunAsync(root, new ChatMessage(ChatRole.User, "shared"));
        var fork = await store.ForkAsync(root);

        var act = async () => await store.MergeAsync(source: root, target: fork);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*ancestor*");
    }

    [Fact]
    public async Task MergeAsync_UnrelatedSessions_Throws()
    {
        var (store, agent) = BuildHost();
        var a = await store.CreateAsync(agent.Id);
        var b = await store.CreateAsync(agent.Id);

        var act = async () => await store.MergeAsync(source: a, target: b);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*ancestor*");
    }

    [Fact]
    public async Task MergeAsync_ClonesMessagesSoSourceMutationsDoNotLeak()
    {
        var (store, agent) = BuildHost();
        var root = await store.CreateAsync(agent.Id);
        await agent.RunAsync(root, new ChatMessage(ChatRole.User, "shared"));

        var fork = await store.ForkAsync(root);
        await agent.RunAsync(fork, new ChatMessage(ChatRole.User, "only-on-fork"));

        await store.MergeAsync(source: fork, target: root);

        // Find the merged user message on the source and mutate its props.
        fork.InnerSession!.TryGetInMemoryChatHistory(out var srcHistory);
        var srcMsg = srcHistory![^2]; // user msg, not assistant reply
        srcMsg.AdditionalProperties ??= new AdditionalPropertiesDictionary();
        srcMsg.AdditionalProperties["injected"] = "value";

        root.InnerSession!.TryGetInMemoryChatHistory(out var rootHistory);
        var rootCopy = rootHistory![^2]; // the just-merged user msg on root
        var leaked = rootCopy.AdditionalProperties is not null
            && rootCopy.AdditionalProperties.ContainsKey("injected");

        leaked.Should().BeFalse(
            "merge must deep-clone messages so post-merge mutation cannot cross branches.");
    }

    [Fact]
    public async Task ForkAsync_OnUnboundSession_Throws()
    {
        var (store, _) = BuildHost();

        var phantom = new SentirumSession(
            id: Guid.NewGuid().ToString("n"),
            agentId: "test",
            innerSession: null);

        var act = async () => await store.ForkAsync(phantom);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private sealed class FakeChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, "ok");
            await Task.CompletedTask;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
