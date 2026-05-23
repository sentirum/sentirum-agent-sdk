using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Sentirum.Agent.Workflows.Tests.Fakes;

/// <summary>
/// Adapter that hands a raw <see cref="AIAgent"/> to the workflow
/// builder without spinning up the full <c>SentirumAgentBuilder</c> +
/// chat-client stack.
/// </summary>
internal sealed class SentirumAgentWrapper : ISentirumAgent
{
    public SentirumAgentWrapper(AIAgent inner, string? id = null, string? name = null)
    {
        InnerAgent = inner;
        Id = id ?? inner.Id;
        Name = name ?? inner.Name ?? Id;
    }

    public string Id { get; }
    public string Name { get; }
    public AIAgent InnerAgent { get; }

    public Task<AgentResponse> RunAsync(
        ISentirumSession session,
        ChatMessage input,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Test fake bypasses the SentirumAgent surface.");

    public IAsyncEnumerable<AgentResponseUpdate> RunStreamingAsync(
        ISentirumSession session,
        ChatMessage input,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Test fake bypasses the SentirumAgent surface.");

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
