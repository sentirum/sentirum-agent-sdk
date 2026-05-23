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
    /// Maximum number of sessions before proactive eviction kicks in.
    /// When exceeded, the oldest sessions are removed. Set to
    /// <c>0</c> to disable the limit.
    /// </summary>
    public int MaxSessionCount { get; init; } = 1_000;

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
    public async Task<ISentirumSession> ForkAsync(
        ISentirumSession parent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parent);

        var agent = _registry.Find(parent.AgentId)
            ?? throw new InvalidOperationException(
                $"Agent '{parent.AgentId}' not found in registry during fork.");

        // Create a fresh AgentSession for the fork so mutations in the
        // child branch do not bleed back into the parent. This gives
        // true tree semantics rather than a shallow reference share.
        var freshInnerSession = await agent.InnerAgent
            .CreateSessionAsync(cancellationToken)
            .ConfigureAwait(false);

        var fork = new SentirumSession(
            id: Guid.NewGuid().ToString("n"),
            agentId: parent.AgentId,
            innerSession: freshInnerSession,
            parentId: parent.Id);

        _sessions[fork.Id] = fork;
        return fork;
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

        if (MaxSessionCount > 0 && _sessions.Count > MaxSessionCount)
        {
            EvictOldest();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Removes the oldest sessions until the count is back under
    /// <see cref="MaxSessionCount"/>. The "oldest" heuristic is the
    /// first entries returned by the dictionary enumerator — sufficient
    /// for a best-effort pressure valve, not a strict LRU.
    /// </summary>
    public void EvictOldest()
    {
        var excess = _sessions.Count - MaxSessionCount;
        if (excess <= 0)
        {
            return;
        }

        foreach (var key in _sessions.Keys.Take(excess))
        {
            _sessions.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Total sessions currently held.
    /// </summary>
    public int SessionCount => _sessions.Count;
}
