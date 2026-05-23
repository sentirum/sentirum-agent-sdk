using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI;

namespace Sentirum.Agent;

/// <summary>
/// Default <see cref="ISentirumSessionStore"/> implementation that keeps
/// sessions in process memory.
/// </summary>
/// <remarks>
/// Intended for single-process scenarios, samples, and tests. Use
/// <c>Sentirum.Agent.Memory.Redis</c> (M4) for distributed deployments.
/// </remarks>
public sealed class InMemorySessionStore : ISentirumSessionStore
{
    private readonly ConcurrentDictionary<string, ISentirumSession> _sessions = new(StringComparer.Ordinal);
    private readonly ISentirumAgentRegistry _registry;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemorySessionStore"/> class.
    /// </summary>
    public InMemorySessionStore(ISentirumAgentRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
    }

    /// <inheritdoc />
    public async Task<ISentirumSession> CreateAsync(
        string agentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);

        var agent = _registry.Find(agentId)
            ?? throw new InvalidOperationException(
                $"No Sentirum agent is registered with id '{agentId}'.");

        var innerSession = await agent.InnerAgent
            .CreateSessionAsync(cancellationToken)
            .ConfigureAwait(false);

        var session = new SentirumSession(
            id: Guid.NewGuid().ToString("n"),
            agentId: agentId,
            innerSession: innerSession);

        _sessions[session.Id] = session;
        return session;
    }

    /// <inheritdoc />
    public Task<ISentirumSession> ForkAsync(
        ISentirumSession parent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parent);

        // M3 ships true tree-fork semantics on top of AgentSession serialization.
        // For M1 we model the relationship but share the same inner session.
        var fork = new SentirumSession(
            id: Guid.NewGuid().ToString("n"),
            agentId: parent.AgentId,
            innerSession: parent.InnerSession,
            parentId: parent.Id);

        _sessions[fork.Id] = fork;
        return Task.FromResult<ISentirumSession>(fork);
    }

    /// <inheritdoc />
    public Task<ISentirumSession?> GetAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        _sessions.TryGetValue(sessionId, out var session);
        return Task.FromResult(session);
    }

    /// <inheritdoc />
    public Task SaveAsync(
        ISentirumSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        _sessions[session.Id] = session;
        return Task.CompletedTask;
    }
}
