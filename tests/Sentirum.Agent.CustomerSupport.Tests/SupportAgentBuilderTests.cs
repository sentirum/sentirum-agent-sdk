using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Sentirum.Agent.CustomerSupport.Sentiment;
using Sentirum.Agent.Tests.Fakes;
using Xunit;

namespace Sentirum.Agent.CustomerSupport.Tests;

public class SupportAgentBuilderTests : IDisposable
{
    private readonly System.Collections.Generic.List<IDisposable> _disposables = [];

    private SupportAgentFixture BuildSupportAgent(
        string name,
        System.Action<SupportAgentBuilder> configure)
    {
        var fake = new FakeChatClient("ok");
        var services = new ServiceCollection();
        services.AddSentirumSupportAgent(name, b =>
        {
            b.Inner.UseChatClient(_ => fake);
            configure(b);
        });
        var sp = services.BuildServiceProvider();
        _disposables.Add(sp);
        var agent = sp.GetRequiredService<ISentirumAgentRegistry>().Find(name)!;
        var store = sp.GetRequiredService<ISentirumSessionStore>();
        return new SupportAgentFixture(agent, fake, store);
    }

    [Fact]
    public async Task WithTier1_AppliesTier1Instructions()
    {
        var fx = BuildSupportAgent("tier1", b => b.WithTier1());
        var session = await fx.CreateSessionAsync();

        await fx.Agent.RunAsync(session, new ChatMessage(ChatRole.User, "hello"));

        var instructions = fx.Fake.ReceivedOptions.Last()?.Instructions ?? string.Empty;
        instructions.Should().Contain("Tier-1");
    }

    [Fact]
    public async Task WithEscalation_AppliesEscalationInstructions()
    {
        var fx = BuildSupportAgent("esc", b => b.WithEscalation());
        var session = await fx.CreateSessionAsync();

        await fx.Agent.RunAsync(session, new ChatMessage(ChatRole.User, "hello"));

        var instructions = fx.Fake.ReceivedOptions.Last()?.Instructions ?? string.Empty;
        instructions.Should().Contain("escalation specialist");
    }

    [Fact]
    public async Task WithSentimentBasedEscalation_NegativeMessage_InjectsEscalationInstruction()
    {
        var fx = BuildSupportAgent("sent", b => b
            .WithTier1()
            .WithSentimentBasedEscalation());
        var session = await fx.CreateSessionAsync();

        await fx.Agent.RunAsync(session, new ChatMessage(
            ChatRole.User,
            "This is absolutely terrible and broken, I am furious! Worst experience."));

        var combined = string.Join("\n", fx.Fake.ReceivedRequests.Last().Select(m => m.Text));
        combined.Should().Contain("ESCALATION TRIGGERED",
            "a strongly negative message should trigger the escalation instruction");
    }

    [Fact]
    public async Task WithSentimentBasedEscalation_PositiveMessage_DoesNotEscalate()
    {
        var fx = BuildSupportAgent("sent-pos", b => b
            .WithTier1()
            .WithSentimentBasedEscalation());
        var session = await fx.CreateSessionAsync();

        await fx.Agent.RunAsync(session, new ChatMessage(
            ChatRole.User,
            "This is wonderful, I'm so happy, thank you! Amazing service."));

        var combined = string.Join("\n", fx.Fake.ReceivedRequests.Last().Select(m => m.Text));
        combined.Should().NotContain("ESCALATION TRIGGERED",
            "a positive message must not trigger escalation");
    }

    [Fact]
    public async Task WithSentimentBasedEscalation_InvokesPerTurnCallback()
    {
        SentimentScore? captured = null;
        var fx = BuildSupportAgent("cb", b => b
            .WithSentimentBasedEscalation(o => o.OnAnalyzed = s => captured = s));
        var session = await fx.CreateSessionAsync();

        await fx.Agent.RunAsync(session, new ChatMessage(ChatRole.User, "this is terrible and broken"));

        captured.Should().NotBeNull("the per-turn callback must fire on a user message");
        captured!.Value.IsNegative.Should().BeTrue("the message is strongly negative");
    }

    [Fact]
    public async Task AsSupportAgent_WrapsInnerBuilderAndPreservesName()
    {
        var fake = new FakeChatClient("ok");
        var services = new ServiceCollection();
        services.AddSentirumAgent("classic", b => b
            .UseChatClient(_ => fake)
            .AsSupportAgent()
                .WithTier1());

        var sp = services.BuildServiceProvider();
        _disposables.Add(sp);
        var agent = sp.GetRequiredService<ISentirumAgentRegistry>().Find("classic")!;

        agent.Id.Should().Be("classic");

        var store = sp.GetRequiredService<ISentirumSessionStore>();
        var session = await store.CreateAsync(agent.Id);
        await agent.RunAsync(session, new ChatMessage(ChatRole.User, "hi"));

        var instructions = fake.ReceivedOptions.Last()?.Instructions ?? string.Empty;
        instructions.Should().Contain("Tier-1",
            "AsSupportAgent should let support methods run against the inner builder");
    }

    public void Dispose()
    {
        foreach (var d in _disposables)
        {
            d.Dispose();
        }
        GC.SuppressFinalize(this);
    }

    private sealed class SupportAgentFixture
    {
        public SupportAgentFixture(ISentirumAgent agent, FakeChatClient fake, ISentirumSessionStore store)
        {
            Agent = agent;
            Fake = fake;
            Store = store;
        }

        public ISentirumAgent Agent { get; }
        public FakeChatClient Fake { get; }
        private ISentirumSessionStore Store { get; }

        public Task<ISentirumSession> CreateSessionAsync() => Store.CreateAsync(Agent.Id);
    }
}
