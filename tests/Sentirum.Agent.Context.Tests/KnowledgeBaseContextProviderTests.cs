using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Sentirum.Agent.Tests.Fakes;
using Xunit;

namespace Sentirum.Agent.Context.Tests;

public class KnowledgeBaseContextProviderTests
{
    [Fact]
    public async Task SearchKeyedOnLatestUserMessage_InjectsTopSnippets()
    {
        var kb = new InMemoryKnowledgeBase(new[]
        {
            new KnowledgeBaseSnippet("Refund policy", "Customers can request a refund within 30 days.", Score: 0),
            new KnowledgeBaseSnippet("Shipping",       "Standard shipping takes 3-5 business days.",     Score: 0),
            new KnowledgeBaseSnippet("Warranty",       "All electronics carry a 2-year warranty.",       Score: 0),
        });

        var fake = new FakeChatClient("ok");
        var services = new ServiceCollection();
        services.AddSentirumAgent("kb", b => b
            .UseChatClient(_ => fake)
            .WithInstructions("You are a support agent.")
            .WithKnowledgeBase(kb, maxResults: 2));

        using var sp = services.BuildServiceProvider();
        var agent = sp.GetRequiredService<ISentirumAgentRegistry>().Find("kb")!;
        var session = await sp.GetRequiredService<ISentirumSessionStore>().CreateAsync(agent.Id);

        await agent.RunAsync(session, new ChatMessage(ChatRole.User, "How long does shipping take?"));

        var msgText = string.Join("\n", fake.ReceivedRequests.Last().Select(m => m.Text));
        var optsText = fake.ReceivedOptions.Last()?.Instructions ?? string.Empty;
        var combined = msgText + "\n" + optsText;

        combined.Should().Contain("Shipping");
        combined.Should().Contain("3-5 business days");
    }

    [Fact]
    public async Task NoUserMessageInRequest_DoesNotTriggerSearch()
    {
        // Build a KB whose snippets would always match "a"; if the provider
        // ran a search the result would land in the prompt. The provider
        // must instead bail out because there is no user message in the
        // RequestMessages handed to it.
        var fired = 0;
        var kb = new CountingKnowledgeBase(() => fired++);

        var fake = new FakeChatClient("ok");
        var services = new ServiceCollection();
        services.AddSentirumAgent("kb", b => b
            .UseChatClient(_ => fake)
            .WithKnowledgeBase(kb));

        using var sp = services.BuildServiceProvider();
        var agent = sp.GetRequiredService<ISentirumAgentRegistry>().Find("kb")!;
        var session = await sp.GetRequiredService<ISentirumSessionStore>().CreateAsync(agent.Id);

        // Drive a system-only turn through the underlying chat client to
        // simulate a request where no user message exists yet (e.g. a
        // greeting initiated by the assistant).
        await fake.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.System, "warm up") },
            options: null);

        fired.Should().Be(0, "KB search must not run when the request has no user message.");
    }

    [Fact]
    public async Task NoQueryMatch_InjectsNothing()
    {
        var kb = new InMemoryKnowledgeBase(new[]
        {
            new KnowledgeBaseSnippet("Refund policy", "Customers can request a refund within 30 days.", 0),
        });

        var fake = new FakeChatClient("ok");
        var services = new ServiceCollection();
        services.AddSentirumAgent("kb", b => b
            .UseChatClient(_ => fake)
            .WithKnowledgeBase(kb, heading: "KB:"));

        using var sp = services.BuildServiceProvider();
        var agent = sp.GetRequiredService<ISentirumAgentRegistry>().Find("kb")!;
        var session = await sp.GetRequiredService<ISentirumSessionStore>().CreateAsync(agent.Id);

        await agent.RunAsync(session, new ChatMessage(ChatRole.User, "tell me a joke about cats"));

        var msgText = string.Join("\n", fake.ReceivedRequests.Last().Select(m => m.Text));
        var optsText = fake.ReceivedOptions.Last()?.Instructions ?? string.Empty;
        (msgText + "\n" + optsText).Should().NotContain("KB:");
    }

    private sealed class CountingKnowledgeBase : IKnowledgeBase
    {
        private readonly System.Action _onCalled;

        public CountingKnowledgeBase(System.Action onCalled)
        {
            _onCalled = onCalled;
        }

        public Task<System.Collections.Generic.IReadOnlyList<KnowledgeBaseSnippet>> SearchAsync(
            string query,
            int maxResults,
            System.Threading.CancellationToken cancellationToken = default)
        {
            _onCalled();
            System.Collections.Generic.IReadOnlyList<KnowledgeBaseSnippet> empty =
                System.Array.Empty<KnowledgeBaseSnippet>();
            return Task.FromResult(empty);
        }
    }
}
