using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Sentirum.Agent;
using Sentirum.Agent.Observability;
using Sentirum.Agent.Security;
using Xunit;

namespace Sentirum.Agent.Testing.Tests;

/// <summary>
/// Integration tests that compose multiple Sentirum packages end-to-end:
/// Core + Testing + Security + Observability.
/// </summary>
public sealed class IntegrationTests
{
    [Fact]
    public async Task FullPipeline_PiiRedaction_TokenBudget_Recording_ComposeCorrectly()
    {
        // Arrange: build a chat-client pipeline with Security + Observability + Testing layers
        var inner = new FakeChatClient("The answer is 42.");

        // Layer 1 (outermost): Recording
        var recorder = new RecordingChatClient(inner);

        // Layer 2: Token budget
        var budget = new TokenBudgetChatClient(recorder, maxTokens: 10_000);

        // Layer 3 (innermost, closest to messages): PII redaction
        var redaction = new PiiRedactionChatClient(budget);

        // Build agent around the composed pipeline
        var services = new ServiceCollection()
            .AddSentirumCore()
            .AddSentirumAgent("integration", b => b.UseChatClient(_ => redaction))
            .BuildServiceProvider();

        var agent = services.GetAgent("integration");
        var session = new SentirumSession(Guid.NewGuid().ToString("n"), agent.Id, null!);

        var message = new ChatMessage(
            ChatRole.User,
            "My email is alice@example.com and my SSN is 123-45-6789.");

        // Act
        var response = await agent.RunAsync(session, message);

        // Assert: response flows through all layers
        response.Messages.Should().ContainSingle()
            .Which.Text.Should().Be("The answer is 42.");

        // Assert: PII was redacted before reaching the fake client
        inner.ReceivedRequests.Should().HaveCount(1);
        var receivedText = inner.ReceivedRequests[0][0].Text;
        receivedText.Should().Contain("[REDACTED]");
        receivedText.Should().NotContain("alice@example.com");
        receivedText.Should().NotContain("123-45-6789");

        // Assert: recording captured the redacted request
        recorder.Interactions.Should().HaveCount(1);
        recorder.Interactions[0].RequestMessages[0].Text.Should().Contain("[REDACTED]");
    }

    [Fact]
    public async Task FullPipeline_TokenBudget_ExceedsBudget_Throws()
    {
        var inner = new FakeChatClient(cannedReply: "ok", usageTokens: 6);
        var budget = new TokenBudgetChatClient(inner, maxTokens: 10);
        var redaction = new PiiRedactionChatClient(budget);

        var services = new ServiceCollection()
            .AddSentirumCore()
            .AddSentirumAgent("budget-test", b => b.UseChatClient(_ => redaction))
            .BuildServiceProvider();

        var agent = services.GetAgent("budget-test");
        var session = new SentirumSession(Guid.NewGuid().ToString("n"), agent.Id, null!);

        // First request: 6 tokens ≤ 10 budget → succeeds
        await agent.RunAsync(session, new ChatMessage(ChatRole.User, "first"));

        // Second request: 6 + 6 = 12 > 10 budget → throws
        Func<Task> act = () => agent.RunAsync(session, new ChatMessage(ChatRole.User, "second"));
        await act.Should().ThrowAsync<TokenBudgetExceededException>();
    }

    [Fact]
    public async Task FullPipeline_FixtureRoundTrip_ReplaysCorrectly()
    {
        // Record a conversation through the full pipeline
        var inner = new FakeChatClient("Round-trip response");
        var recorder = new RecordingChatClient(inner);
        var redaction = new PiiRedactionChatClient(recorder);

        var services = new ServiceCollection()
            .AddSentirumCore()
            .AddSentirumAgent("roundtrip", b => b.UseChatClient(_ => redaction))
            .BuildServiceProvider();

        var agent = services.GetAgent("roundtrip");
        var session = new SentirumSession(Guid.NewGuid().ToString("n"), agent.Id, null!);

        await agent.RunAsync(session, new ChatMessage(ChatRole.User, "ping"));

        // Export fixture
        var fixture = recorder.SaveToString();
        fixture.Should().Contain("Round-trip response");

        // Replay in a fresh agent
        var replayServices = new ServiceCollection()
            .AddSentirumCore()
            .AddReplayAgent("replay", fixture)
            .BuildServiceProvider();

        var replayAgent = replayServices.GetAgent("replay");
        var replaySession = new SentirumSession(Guid.NewGuid().ToString("n"), replayAgent.Id, null!);

        var replayResponse = await replayAgent.RunAsync(replaySession, new ChatMessage(ChatRole.User, "ping"));
        replayResponse.Messages[0].Text.Should().Be("Round-trip response");
    }
}
