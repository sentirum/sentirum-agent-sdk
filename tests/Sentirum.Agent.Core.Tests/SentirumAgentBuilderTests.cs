using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Sentirum.Agent.Tests.Fakes;
using Xunit;

namespace Sentirum.Agent.Tests;

public sealed class SentirumAgentBuilderTests
{
    [Fact]
    public void AddSentirumAgent_RegistersAgentInRegistry()
    {
        var services = new ServiceCollection();
        var fake = new FakeChatClient("hello");

        services.AddSentirumAgent("support", b => b
            .WithInstructions("You are a Sentirum support agent.")
            .UseChatClient(_ => fake));

        using var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<ISentirumAgentRegistry>();

        registry.Agents.Should().HaveCount(1);
        registry.Find("support").Should().NotBeNull();
        registry.Find("unknown").Should().BeNull();
    }

    [Fact]
    public async Task RunAsync_ForwardsToInnerChatClient()
    {
        var services = new ServiceCollection();
        var fake = new FakeChatClient("merhaba");

        services.AddSentirumAgent("support", b => b
            .WithInstructions("You are a Sentirum support agent.")
            .UseChatClient(_ => fake));

        using var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<ISentirumAgentRegistry>();
        var store = sp.GetRequiredService<ISentirumSessionStore>();

        var agent = registry.Find("support")!;
        var session = await store.CreateAsync(agent.Id);

        var response = await agent.RunAsync(
            session,
            new ChatMessage(ChatRole.User, "Selam"));

        response.Text.Should().Be("merhaba");
        fake.ReceivedRequests.Should().HaveCount(1);
    }

    [Fact]
    public void Build_WithoutChatClient_Throws()
    {
        var services = new ServiceCollection();

        services.AddSentirumAgent("broken", b => b
            .WithInstructions("missing provider"));

        using var sp = services.BuildServiceProvider();

        var act = () => sp.GetRequiredKeyedService<ISentirumAgent>("broken");
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*no chat-client*");
    }

    [Fact]
    public async Task ForkAsync_CreatesSessionWithParentId()
    {
        var services = new ServiceCollection();
        services.AddSentirumAgent("support", b => b
            .UseChatClient(_ => new FakeChatClient()));

        using var sp = services.BuildServiceProvider();
        var store = sp.GetRequiredService<ISentirumSessionStore>();

        var parent = await store.CreateAsync("support");
        var fork = await store.ForkAsync(parent);

        fork.ParentId.Should().Be(parent.Id);
        fork.AgentId.Should().Be(parent.AgentId);
        fork.Id.Should().NotBe(parent.Id);
    }
}
