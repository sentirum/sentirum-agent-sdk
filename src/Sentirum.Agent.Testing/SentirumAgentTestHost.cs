using System;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Sentirum.Agent.Builder;
using Sentirum.Agent;

namespace Sentirum.Agent.Testing;

/// <summary>
/// Factory for creating a pre-configured <see cref="IServiceProvider"/> for
/// testing Sentirum agents. Provides convenience methods for registering
/// fake, recording, or replay chat clients without boilerplate.
/// </summary>
public static class SentirumAgentTestHost
{
    /// <summary>
    /// Creates a new service collection with Sentirum core services registered.
    /// </summary>
    public static IServiceCollection CreateServices() =>
        new ServiceCollection().AddSentirumCore();

    /// <summary>
    /// Registers an agent that uses a <see cref="FakeChatClient"/> returning
    /// <paramref name="cannedReply"/> for every request.
    /// </summary>
    public static IServiceCollection AddFakeAgent(
        this IServiceCollection services,
        string name,
        string cannedReply = "ok")
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddSentirumAgent(name, b => b.UseChatClient(_ => new FakeChatClient(cannedReply)));
    }

    /// <summary>
    /// Registers an agent that uses the supplied <see cref="RecordingChatClient"/>.
    /// The caller retains a reference to <paramref name="recorder"/> so recordings
    /// can be inspected or exported after the test runs.
    /// </summary>
    public static IServiceCollection AddRecordingAgent(
        this IServiceCollection services,
        string name,
        RecordingChatClient recorder)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(recorder);
        return services.AddSentirumAgent(name, b => b.UseChatClient(_ => recorder));
    }

    /// <summary>
    /// Registers an agent that uses a <see cref="ReplayChatClient"/> loaded from
    /// a JSON fixture string.
    /// </summary>
    public static IServiceCollection AddReplayAgent(
        this IServiceCollection services,
        string name,
        string fixtureJson,
        ReplayChatClient.MatchStrategy matchStrategy = ReplayChatClient.MatchStrategy.Exact)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(fixtureJson);

        var replay = ReplayChatClient.LoadFromString(fixtureJson, matchStrategy);
        return services.AddSentirumAgent(name, b => b.UseChatClient(_ => replay));
    }

    /// <summary>
    /// Builds the service provider and resolves the named agent.
    /// </summary>
    public static ISentirumAgent GetAgent(this IServiceProvider serviceProvider, string name) =>
        serviceProvider.GetRequiredKeyedService<ISentirumAgent>(name);
}

/// <summary>
/// Minimal in-memory <see cref="IChatClient"/> for tests. Returns a single
/// canned response and records the prompts it received.
/// </summary>
public sealed class FakeChatClient : IChatClient
{
    private readonly string _cannedReply;
    private readonly long _usageTokens;

    /// <summary>
    /// Initializes a new instance with the supplied canned reply.
    /// </summary>
    public FakeChatClient(string cannedReply = "ok", long usageTokens = 100)
    {
        _cannedReply = cannedReply;
        _usageTokens = usageTokens;
    }

    /// <summary>
    /// Every request message list received so far.
    /// </summary>
    public List<IList<ChatMessage>> ReceivedRequests { get; } = [];

    /// <summary>
    /// Every <see cref="ChatOptions"/> received so far.
    /// </summary>
    public List<ChatOptions?> ReceivedOptions { get; } = [];

    /// <inheritdoc />
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var list = new List<ChatMessage>(messages);
        ReceivedRequests.Add(list);
        ReceivedOptions.Add(options);

        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, _cannedReply))
        {
            Usage = new UsageDetails
            {
                InputTokenCount = list.Sum(m => (m.Text ?? string.Empty).Length / 4),
                OutputTokenCount = _cannedReply.Length / 4,
                TotalTokenCount = _usageTokens,
            },
        };
        return Task.FromResult(response);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var list = new List<ChatMessage>(messages);
        ReceivedRequests.Add(list);
        ReceivedOptions.Add(options);

        yield return new ChatResponseUpdate(ChatRole.Assistant, _cannedReply);
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
