using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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
/// <para>
/// Intended for single-process scenarios, samples, and tests. A distributed
/// implementation (Redis / EF Core) lands in M4.
/// </para>
/// <para>
/// All mutating operations are safe for concurrent callers. Children lists
/// are stored as <see cref="ImmutableArray{T}"/> behind a lock-free
/// <see cref="ConcurrentDictionary{TKey, TValue}"/> using <c>AddOrUpdate</c>
/// so concurrent forks of the same parent never corrupt the list.
/// </para>
/// </remarks>
public sealed class InMemoryTreeSessionStore : ITreeSessionStore
{
    private readonly ConcurrentDictionary<string, ISentirumSession> _sessions =
        new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, ImmutableArray<string>> _childrenByParent =
        new(StringComparer.Ordinal);

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
        cancellationToken.ThrowIfCancellationRequested();

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
        cancellationToken.ThrowIfCancellationRequested();

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

        // ImmutableArray + AddOrUpdate makes concurrent forks of the same
        // parent safe — neither caller can observe a partially-mutated list.
        _childrenByParent.AddOrUpdate(
            parent.Id,
            _ => ImmutableArray.Create(fork.Id),
            (_, existing) => existing.Add(fork.Id));

        return fork;
    }

    /// <inheritdoc />
    public Task<ISentirumSession?> GetAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        cancellationToken.ThrowIfCancellationRequested();

        _sessions.TryGetValue(sessionId, out var session);
        return Task.FromResult(session);
    }

    /// <inheritdoc />
    public Task SaveAsync(
        ISentirumSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();

        _sessions[session.Id] = session;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<SessionTree> GetTreeAsync(
        string rootSessionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootSessionId);
        cancellationToken.ThrowIfCancellationRequested();

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
    /// <remarks>
    /// <para>
    /// Merge semantics (see ADR-0002):
    /// </para>
    /// <list type="bullet">
    ///   <item><description>
    ///     <paramref name="target"/> must be an ancestor of
    ///     <paramref name="source"/>. The conceptual model is "fold a branch
    ///     back into its trunk". A no-op direction (merging an ancestor into
    ///     a descendant) throws so callers cannot accidentally duplicate
    ///     shared history.
    ///   </description></item>
    ///   <item><description>
    ///     The two sessions must belong to the same agent.
    ///   </description></item>
    ///   <item><description>
    ///     Only messages added on <paramref name="source"/> after the fork
    ///     point are replayed onto <paramref name="target"/>; each is
    ///     deep-cloned so further mutation on either side cannot leak across
    ///     branches.
    ///   </description></item>
    /// </list>
    /// </remarks>
    public Task MergeAsync(
        ISentirumSession source,
        ISentirumSession target,
        int? lastMessageCount = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.Equals(source.Id, target.Id, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Cannot merge a session into itself.");
        }

        if (!string.Equals(source.AgentId, target.AgentId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Cannot merge sessions across different agents " +
                $"('{source.AgentId}' vs '{target.AgentId}').");
        }

        if (source is not SentirumSession concreteSource)
        {
            throw new InvalidOperationException(
                $"Merge requires the source session to carry Sentirum fork " +
                $"metadata; got '{source.GetType().FullName}'.");
        }

        if (!IsAncestor(candidateAncestorId: target.Id, descendantSession: concreteSource))
        {
            throw new InvalidOperationException(
                $"Cannot merge: target session '{target.Id}' is not an ancestor " +
                $"of source '{source.Id}'. Merges always fold a fork back into " +
                "its trunk.");
        }

        if (source.InnerSession is null || target.InnerSession is null)
        {
            throw new InvalidOperationException(
                "Both source and target sessions must be bound to an AgentSession before merging.");
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

        // Divergence index recorded at ForkAsync time. Clamp to current
        // source size because callers may truncate history out-of-band.
        var divergenceIndex = Math.Min(concreteSource.ForkPointMessageCount, sourceHistory.Count);
        var newOnSource = sourceHistory.Skip(divergenceIndex).ToList();

        if (lastMessageCount is int take && take >= 0 && take < newOnSource.Count)
        {
            newOnSource = newOnSource.Skip(newOnSource.Count - take).ToList();
        }

        foreach (var message in newOnSource)
        {
            // Deep-clone so post-merge mutation on the source branch can't
            // leak into target history (and vice-versa).
            targetHistory.Add(CloneMessage(message));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<SessionDiff> CompareAsync(
        ISentirumSession left,
        ISentirumSession right,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        cancellationToken.ThrowIfCancellationRequested();

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
            // childIds is an ImmutableArray — safe to iterate without lock.
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

    /// <summary>
    /// Walks the parent chain of <paramref name="descendantSession"/> and
    /// returns <see langword="true"/> when <paramref name="candidateAncestorId"/>
    /// is reached.
    /// </summary>
    private bool IsAncestor(string candidateAncestorId, SentirumSession descendantSession)
    {
        var current = descendantSession.ParentId;
        while (current is not null)
        {
            if (string.Equals(current, candidateAncestorId, StringComparison.Ordinal))
            {
                return true;
            }

            if (!_sessions.TryGetValue(current, out var parent))
            {
                break;
            }

            current = parent.ParentId;
        }

        return false;
    }

    /// <summary>
    /// Produces a shallow-deep clone of <paramref name="message"/> suitable
    /// for cross-branch transfer: contents and additional properties are
    /// copied into fresh containers so post-merge mutation on either branch
    /// cannot leak across.
    /// </summary>
    private static ChatMessage CloneMessage(ChatMessage message)
    {
        // ChatMessage exposes a copy constructor as `Contents` list; we
        // rebuild a new instance so the target list and properties dict
        // don't alias the source.
        var contents = message.Contents is { Count: > 0 }
            ? new List<AIContent>(message.Contents)
            : new List<AIContent>();

        var clone = new ChatMessage(message.Role, contents)
        {
            AuthorName = message.AuthorName,
            MessageId = message.MessageId,
            RawRepresentation = message.RawRepresentation,
        };

        if (message.AdditionalProperties is { Count: > 0 } props)
        {
            clone.AdditionalProperties = new AdditionalPropertiesDictionary(props);
        }

        return clone;
    }

    private ISentirumAgent ResolveAgent(string agentId)
    {
        return _registry.Find(agentId)
            ?? throw new InvalidOperationException(
                $"No Sentirum agent is registered with id '{agentId}'.");
    }
}
