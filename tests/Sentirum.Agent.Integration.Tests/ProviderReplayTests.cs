using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Sentirum.Agent;
using Sentirum.Agent.Testing;
using Xunit;

namespace Sentirum.Agent.Integration.Tests;

/// <summary>
/// Integration tests that replay recorded provider fixtures through a full
/// <see cref="ISentirumAgent"/> pipeline.
/// </summary>
public sealed class ProviderReplayTests
{
    [Theory]
    [InlineData("openai-chat.json", "The capital of France is Paris.")]
    [InlineData("anthropic-chat.json", "Quantum computing leverages quantum mechanical phenomena")]
    [InlineData("ollama-chat.json", "¡Hola!")]
    [InlineData("zai-chat.json", "2 + 2 = 4.")]
    [InlineData("azureopenai-chat.json", "Red, blue, and yellow.")]
    public async Task ProviderFixture_ReplaysThroughAgent(string fixtureName, string expectedSubstring)
    {
        var fixturePath = Path.Combine("Fixtures", fixtureName);
        var json = await File.ReadAllTextAsync(fixturePath);

        var services = SentirumAgentTestHost.CreateServices()
            .AddReplayAgent("test", json, ReplayChatClient.MatchStrategy.Sequential);

        var agent = services.BuildServiceProvider().GetAgent("test");
        var session = new SentirumSession(Guid.NewGuid().ToString("n"), agent.Id, null!);

        var response = await agent.RunAsync(session, new ChatMessage(ChatRole.User, "trigger"));

        response.Messages.Should().ContainSingle()
            .Which.Text.Should().Contain(expectedSubstring);
    }

    [Fact]
    public async Task ProviderFixture_SequentialMode_ReturnsInOrder()
    {
        var fixturePath = Path.Combine("Fixtures", "workflow-sequential.json");
        var json = await File.ReadAllTextAsync(fixturePath);

        var services = SentirumAgentTestHost.CreateServices()
            .AddReplayAgent("test", json, ReplayChatClient.MatchStrategy.Sequential);

        var agent = services.BuildServiceProvider().GetAgent("test");
        var session = new SentirumSession(Guid.NewGuid().ToString("n"), agent.Id, null!);

        var r1 = await agent.RunAsync(session, new ChatMessage(ChatRole.User, "any"));
        var r2 = await agent.RunAsync(session, new ChatMessage(ChatRole.User, "any"));

        r1.Messages[0].Text.Should().Be("Hello!");
        r2.Messages[0].Text.Should().Be("What is your name?");
    }
}
