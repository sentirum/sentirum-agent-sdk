using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Sentirum.Agent.Sessions.Tree;

/// <summary>
/// In-process implementation of <see cref="ITreeSessionStore"/>. Stores
/// sessions by id, tracks parent links, and performs forks via MAF's
/// <see cref="AIAgent.SerializeSessionAsync"/> /
/// <see cref="AIAgent.DeserializeSessionAsync"/> round-trip so each branch
/// is a true deep copy.
/// </summary>
/// <remarks>
/// Intended for single-process scenarios, samples, and tests. A distributed
/// implementation (Redis / EF Core) lands in M4.
/// </remarks>
public sealed class InMemoryTreeSessionStore : ITreeSessionStore
{
    private readonly ConcurrentDictionary<string, ISentirumSession> _sessions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, List<string>> _childrenByParent = new(StringComparer.Ordinal);
    private readonly ISentirumAgentRegistry _registry;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryTreeSessionStore"/> class.
    /// </summary>
    public InMemoryTreeSessionStore(ISentirumAgentRegistry registry)
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

        var agent = ResolveAgent(agentId);
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

        if (parent.InnerSession is null)
        {
            throw new InvalidOperationException(
                $"Session '{parent.Id}' has no inner AgentSession bound; cannot fork.");
        }

        var agent = ResolveAgent(parent.AgentId);

        // Round-trip through Serialize / Deserialize to get a true deep copy.
        // This is the only place where Sentirum's tree-fork semantics differ
        // materially from MAF's linear sessions.
        var serialized = await agent.InnerAgent
            .SerializeSessionAsync(parent.InnerSession, jsonSerializerOptions: null, cancellationToken)
            .ConfigureAwait(false);

        var clonedInner = await agent.InnerAgent
            .DeserializeSessionAsync(serialized, jsonSerializerOptions: null, cancellationToken)
            .ConfigureAwait(false);

        var fork = new SentirumSession(
            id: Guid.NewGuid().ToString("n"),
            agentId: parent.AgentId,
            innerSession: clonedInner,
            parentId: parent.Id,
            forkPointMessageCount: CountMessages(parent));

        _sessions[fork.Id] = fork;
        _childrenByParent
            .GetOrAdd(parent.Id, _ => new List<string>())
            .Add(fork.Id);

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
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<SessionTree> GetTreeAsync(
        string rootSessionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootSessionId);

        if (!_sessions.TryGetValue(rootSessionId, out var anySession))
        {
            throw new InvalidOperationException($"Session '{rootSessionId}' was not found.");
        }

        // Walk parent pointers up to the real root so the caller can pass
        // any node in the tree.
        var actualRoot = anySession;
        while (actualRoot.ParentId is not null
            && _sessions.TryGetValue(actualRoot.ParentId, out var parent))
        {
            actualRoot = parent;
        }

        var rootNode = BuildNode(actualRoot, depth: 0);
        return Task.FromResult(new SessionTree(rootNode));
    }

    /// <inheritdoc />
    public async Task MergeAsync(
        ISentirumSession source,
        ISentirumSession target,
        int? lastMessageCount = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        if (source.InnerSession is null || target.InnerSession is null)
        {
            throw new InvalidOperationException(
                "Both source and target sessions must be bound to an AgentSession before merging.");
        }

        if (!string.Equals(source.AgentId, target.AgentId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Cannot merge sessions across different agents " +
                $"('{source.AgentId}' vs '{target.AgentId}').");
        }

        if (!source.InnerSession.TryGetInMemoryChatHistory(out var sourceHistory)
            || sourceHistory is null)
        {
            throw new InvalidOperationException(
                $"Source session '{source.Id}' does not expose an in-memory chat history; " +
                "cannot merge against it.");
        }

        if (!target.InnerSession.TryGetInMemoryChatHistory(out var targetHistory)
            || targetHistory is null)
        {
            throw new InvalidOperationException(
                $"Target session '{target.Id}' does not expose an in-memory chat history; " +
                "cannot merge into it.");
        }

        // Determine which messages are "new" on the source compared to the
        // target. The divergence point is recorded on the source as
        // ForkPointMessageCount when ForkAsync ran — anything past that
        // index on the source is the work added on this branch.
        var divergenceIndex = source is SentirumSession concreteSource
            ? Math.Min(concreteSource.ForkPointMessageCount, sourceHistory.Count)
            : CommonPrefixLength(sourceHistory, targetHistory);

        var newOnSource = sourceHistory.Skip(divergenceIndex).ToList();

        if (lastMessageCount is int take && take >= 0 && take < newOnSource.Count)
        {
            newOnSource = newOnSource.Skip(newOnSource.Count - take).ToList();
        }

        foreach (var message in newOnSource)
        {
            targetHistory.Add(message);
        }

        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<SessionDiff> CompareAsync(
        ISentirumSession left,
        ISentirumSession right,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return Task.FromResult(new SessionDiff(
            leftSessionId: left.Id,
            rightSessionId: right.Id,
            leftMessageCount: CountMessages(left),
            rightMessageCount: CountMessages(right)));
    }

    private SessionTreeNode BuildNode(ISentirumSession session, int depth)
    {
        var node = new SessionTreeNode(
            session,
            depth,
            messageCount: CountMessages(session));

        if (_childrenByParent.TryGetValue(session.Id, out var childIds))
        {
            foreach (var childId in childIds)
            {
                if (_sessions.TryGetValue(childId, out var childSession))
                {
                    node.Children.Add(BuildNode(childSession, depth + 1));
                }
            }
        }

        return node;
    }

    private static int CountMessages(ISentirumSession session)
    {
        if (session.InnerSession is null)
        {
            return 0;
        }

        return session.InnerSession.TryGetInMemoryChatHistory(out var history) && history is not null
            ? history.Count
            : 0;
    }

    private static int CommonPrefixLength(List<ChatMessage> left, List<ChatMessage> right)
    {
        var max = Math.Min(left.Count, right.Count);
        var i = 0;
        while (i < max && ReferenceEquals(left[i], right[i]))
        {
            i++;
        }

        return i;
    }

    private ISentirumAgent ResolveAgent(string agentId)
    {
        return _registry.Find(agentId)
            ?? throw new InvalidOperationException(
                $"No Sentirum agent is registered with id '{agentId}'.");
    }
}
