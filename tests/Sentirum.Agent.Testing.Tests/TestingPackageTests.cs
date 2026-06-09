using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Sentirum.Agent;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Sentirum.Agent.Testing.Tests;

public sealed class TestingPackageTests
{
    [Fact]
    public async Task FakeChatClient_ReturnsCannedReply()
    {
        var fake = new FakeChatClient("42");

        var response = await fake.GetResponseAsync([new ChatMessage(ChatRole.User, "What is the answer?")]);

        response.Messages.Should().ContainSingle()
            .Which.Text.Should().Be("42");
    }

    [Fact]
    public async Task FakeChatClient_RecordsRequests()
    {
        var fake = new FakeChatClient();
        var messages = new[] { new ChatMessage(ChatRole.User, "Hello") };

        await fake.GetResponseAsync(messages);

        fake.ReceivedRequests.Should().HaveCount(1);
        fake.ReceivedRequests[0].Should().ContainSingle()
            .Which.Text.Should().Be("Hello");
    }

    [Fact]
    public async Task RecordingChatClient_RecordsInteraction()
    {
        var inner = new FakeChatClient("recorded");
        var recorder = new RecordingChatClient(inner);

        var response = await recorder.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "test")]);

        response.Messages[0].Text.Should().Be("recorded");
        recorder.Interactions.Should().HaveCount(1);
        recorder.Interactions[0].RequestMessages[0].Text.Should().Be("test");
        recorder.Interactions[0].ResponseMessages[0].Text.Should().Be("recorded");
    }

    [Fact]
    public async Task RecordingChatClient_SaveAndReplayRoundTrip()
    {
        var inner = new FakeChatClient("round-trip");
        var recorder = new RecordingChatClient(inner);
        await recorder.GetResponseAsync([new ChatMessage(ChatRole.User, "ping")]);

        var json = recorder.SaveToString();
        json.Should().Contain("round-trip");

        var replay = ReplayChatClient.LoadFromString(json);
        var response = await replay.GetResponseAsync([new ChatMessage(ChatRole.User, "ping")]);

        response.Messages[0].Text.Should().Be("round-trip");
    }

    [Fact]
    public async Task ReplayChatClient_SupportsSequentialMode()
    {
        var interactions = new List<ChatInteraction>
        {
            new()
            {
                RequestMessages = [new RecordedMessage { Role = "user", Text = "a" }],
                ResponseMessages = [new RecordedMessage { Role = "assistant", Text = "1" }],
            },
            new()
            {
                RequestMessages = [new RecordedMessage { Role = "user", Text = "b" }],
                ResponseMessages = [new RecordedMessage { Role = "assistant", Text = "2" }],
            },
        };

        var replay = new ReplayChatClient(interactions, ReplayChatClient.MatchStrategy.Sequential);

        var r1 = await replay.GetResponseAsync([new ChatMessage(ChatRole.User, "anything")]);
        var r2 = await replay.GetResponseAsync([new ChatMessage(ChatRole.User, "anything")]);

        r1.Messages[0].Text.Should().Be("1");
        r2.Messages[0].Text.Should().Be("2");
    }

    [Fact]
    public async Task ReplayChatClient_ThrowsWhenNoMatch()
    {
        var interactions = new List<ChatInteraction>
        {
            new()
            {
                RequestMessages = [new RecordedMessage { Role = "user", Text = "known" }],
                ResponseMessages = [new RecordedMessage { Role = "assistant", Text = "ok" }],
            },
        };

        var replay = new ReplayChatClient(interactions);

        Func<Task> act = () => replay.GetResponseAsync([new ChatMessage(ChatRole.User, "unknown")]);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void TestHost_AddFakeAgent_ResolvesAgent()
    {
        var services = SentirumAgentTestHost.CreateServices()
            .AddFakeAgent("test", cannedReply: "fake");

        var agent = services.BuildServiceProvider().GetAgent("test");

        agent.Name.Should().Be("test");
    }

    [Fact]
    public async Task TestHost_AddReplayAgent_RunsFromFixture()
    {
        var fixture = "[{\"requestMessages\":[{\"role\":\"user\",\"text\":\"hi\"}]," +
            "\"responseMessages\":[{\"role\":\"assistant\",\"text\":\"hello\"}]}]";

        var services = SentirumAgentTestHost.CreateServices()
            .AddReplayAgent("test", fixture);

        var agent = services.BuildServiceProvider().GetAgent("test");
        var session = new SentirumSession(Guid.NewGuid().ToString("n"), agent.Id, null!);
        var response = await agent.RunAsync(session, new ChatMessage(ChatRole.User, "hi"));

        response.Messages[0].Text.Should().Be("hello");
    }
}
