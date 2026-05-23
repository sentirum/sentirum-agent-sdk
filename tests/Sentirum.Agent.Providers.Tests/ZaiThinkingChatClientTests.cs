using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Sentirum.Agent.Providers.ZAI;
using Xunit;

namespace Sentirum.Agent.Providers.Tests;

public sealed class ZaiThinkingChatClientTests
{
    [Fact]
    public async Task EnableZaiThinking_InjectsThinkingEnabledIntoChatOptions()
    {
        var spy = new SpyChatClient();
        var services = new ServiceCollection();

        services.AddSentirumAgent("zai", b => b
            .UseChatClient(_ => spy)
            .EnableZaiThinking());

        using var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<ISentirumAgentRegistry>();
        var store = sp.GetRequiredService<ISentirumSessionStore>();

        var agent = registry.Find("zai")!;
        var session = await store.CreateAsync(agent.Id);

        await agent.RunAsync(session, new ChatMessage(ChatRole.User, "hi"));

        spy.LastOptions.Should().NotBeNull();
        spy.LastOptions!.AdditionalProperties.Should().ContainKey("thinking");

        var thinking = spy.LastOptions.AdditionalProperties!["thinking"] as JsonNode;
        thinking.Should().NotBeNull();
        thinking!["type"]!.GetValue<string>().Should().Be("enabled");
    }

    [Fact]
    public async Task EnableZaiThinking_DoesNotMutateCallerSuppliedAdditionalProperties()
    {
        var spy = new SpyChatClient();
        var services = new ServiceCollection();

        services.AddSentirumAgent("zai", b => b
            .UseChatClient(_ => spy)
            .EnableZaiThinking());

        using var sp = services.BuildServiceProvider();
        var agent = sp.GetRequiredService<ISentirumAgentRegistry>().Find("zai")!;
        var session = await sp.GetRequiredService<ISentirumSessionStore>().CreateAsync(agent.Id);

        // Drive the run through the agent twice with a shared ChatOptions
        // proxy; the spy captures whatever leaves the EnableZaiThinking layer.
        await agent.RunAsync(session, new ChatMessage(ChatRole.User, "one"));
        var firstSeen = spy.LastOptions;
        await agent.RunAsync(session, new ChatMessage(ChatRole.User, "two"));
        var secondSeen = spy.LastOptions;

        // Each turn must hand a fresh ChatOptions to the leaf provider so
        // mutation across turns cannot leak.
        ReferenceEquals(firstSeen, secondSeen).Should().BeFalse(
            "ApplyThinking must clone ChatOptions per call.");
    }

    [Fact]
    public async Task EnableZaiThinking_DisabledFlag_EmitsDisabledType()
    {
        var spy = new SpyChatClient();
        var services = new ServiceCollection();

        services.AddSentirumAgent("zai", b => b
            .UseChatClient(_ => spy)
            .EnableZaiThinking(enabled: false));

        using var sp = services.BuildServiceProvider();
        var agent = sp.GetRequiredService<ISentirumAgentRegistry>().Find("zai")!;
        var session = await sp.GetRequiredService<ISentirumSessionStore>().CreateAsync(agent.Id);

        await agent.RunAsync(session, new ChatMessage(ChatRole.User, "hi"));

        var thinking = spy.LastOptions!.AdditionalProperties!["thinking"] as JsonNode;
        thinking!["type"]!.GetValue<string>().Should().Be("disabled");
    }

    private sealed class SpyChatClient : IChatClient
    {
        public ChatOptions? LastOptions { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            LastOptions = options;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            LastOptions = options;
            yield return new ChatResponseUpdate(ChatRole.Assistant, "ok");
            await Task.CompletedTask;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
