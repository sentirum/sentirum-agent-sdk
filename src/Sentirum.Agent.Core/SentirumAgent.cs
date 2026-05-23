using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Sentirum.Agent;

/// <summary>
/// Default <see cref="ISentirumAgent"/> implementation that wraps a
/// Microsoft Agent Framework <see cref="AIAgent"/>.
/// </summary>
public sealed class SentirumAgent : ISentirumAgent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SentirumAgent"/> class.
    /// </summary>
    public SentirumAgent(string id, string name, AIAgent innerAgent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(innerAgent);

        Id = id;
        Name = name;
        InnerAgent = innerAgent;
    }

    /// <inheritdoc />
    public string Id { get; }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public AIAgent InnerAgent { get; }

    /// <inheritdoc />
    public async Task<AgentResponse> RunAsync(
        ISentirumSession session,
        ChatMessage input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(input);

        return await InnerAgent.RunAsync(
            [input],
            session.InnerSession,
            options: null,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<AgentResponseUpdate> RunStreamingAsync(
        ISentirumSession session,
        ChatMessage input,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(input);

        var stream = InnerAgent.RunStreamingAsync(
            [input],
            session.InnerSession,
            options: null,
            cancellationToken);

        await foreach (var update in stream.ConfigureAwait(false))
        {
            yield return update;
        }
    }
}
