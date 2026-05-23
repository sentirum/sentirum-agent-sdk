using System;

namespace Sentirum.Agent.Memory;

/// <summary>
/// Identifies the lifetime / partitioning boundary of a Sentirum memory entry.
/// </summary>
/// <remarks>
/// <para>
/// Memory scopes are the primary way Sentirum decides who can read or write a
/// memory entry. Concrete <see cref="ISentirumMemoryStore"/> implementations
/// translate the scope into a key-prefix or table partition:
/// </para>
/// <list type="bullet">
///   <item><description><see cref="Global"/> — visible to everyone using this store.</description></item>
///   <item><description><see cref="Agent"/> — partitioned by agent id; shared across users.</description></item>
///   <item><description><see cref="User"/> — partitioned by user id; survives across sessions.</description></item>
///   <item><description><see cref="Session"/> — bound to a single conversation; cleared when the session ends.</description></item>
/// </list>
/// </remarks>
public enum MemoryScope
{
    /// <summary>Visible to every agent and every user. Use sparingly.</summary>
    Global = 0,

    /// <summary>Scoped to a single agent across all users.</summary>
    Agent = 1,

    /// <summary>Scoped to a single user across all sessions.</summary>
    User = 2,

    /// <summary>Scoped to a single session (and therefore a single tree branch).</summary>
    Session = 3,
}

/// <summary>
/// Concrete partition descriptor passed to memory operations. Combines a
/// <see cref="MemoryScope"/> with the identifiers that resolve it to an
/// addressable partition in the underlying store.
/// </summary>
/// <param name="Scope">The lifetime/partitioning boundary.</param>
/// <param name="AgentId">Agent identifier. Required for <see cref="MemoryScope.Agent"/>; optional otherwise.</param>
/// <param name="UserId">User identifier. Required for <see cref="MemoryScope.User"/>; optional otherwise.</param>
/// <param name="SessionId">Session identifier. Required for <see cref="MemoryScope.Session"/>; optional otherwise.</param>
public readonly record struct MemoryPartition(
    MemoryScope Scope,
    string? AgentId = null,
    string? UserId = null,
    string? SessionId = null)
{
    /// <summary>Creates a global partition.</summary>
    public static MemoryPartition ForGlobal() => new(MemoryScope.Global);

    /// <summary>Creates a partition scoped to <paramref name="agentId"/>.</summary>
    public static MemoryPartition ForAgent(string agentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        return new MemoryPartition(MemoryScope.Agent, AgentId: agentId);
    }

    /// <summary>Creates a partition scoped to <paramref name="userId"/>.</summary>
    public static MemoryPartition ForUser(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return new MemoryPartition(MemoryScope.User, UserId: userId);
    }

    /// <summary>Creates a partition scoped to <paramref name="sessionId"/>.</summary>
    public static MemoryPartition ForSession(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        return new MemoryPartition(MemoryScope.Session, SessionId: sessionId);
    }

    /// <summary>
    /// Throws when the partition is missing the identifier required by its
    /// <see cref="Scope"/>, or when it carries identifiers that do not
    /// apply to its scope (e.g. a <see cref="MemoryScope.Global"/> partition
    /// with a <see cref="UserId"/> set).
    /// </summary>
    /// <remarks>
    /// Over-specification is rejected because different backends interpret
    /// the extra ids differently (some ignore them, EF Core indexes them)
    /// which would silently mis-partition data across stores.
    /// </remarks>
    public void Validate()
    {
        switch (Scope)
        {
            case MemoryScope.Global:
                RejectExtras(allowedAgent: false, allowedUser: false, allowedSession: false);
                break;
            case MemoryScope.Agent:
                if (string.IsNullOrWhiteSpace(AgentId))
                {
                    throw new InvalidOperationException("MemoryScope.Agent requires AgentId.");
                }
                RejectExtras(allowedAgent: true, allowedUser: false, allowedSession: false);
                break;
            case MemoryScope.User:
                if (string.IsNullOrWhiteSpace(UserId))
                {
                    throw new InvalidOperationException("MemoryScope.User requires UserId.");
                }
                RejectExtras(allowedAgent: false, allowedUser: true, allowedSession: false);
                break;
            case MemoryScope.Session:
                if (string.IsNullOrWhiteSpace(SessionId))
                {
                    throw new InvalidOperationException("MemoryScope.Session requires SessionId.");
                }
                RejectExtras(allowedAgent: false, allowedUser: false, allowedSession: true);
                break;
        }
    }

    private void RejectExtras(bool allowedAgent, bool allowedUser, bool allowedSession)
    {
        if (!allowedAgent && !string.IsNullOrWhiteSpace(AgentId))
        {
            throw new InvalidOperationException(
                $"MemoryScope.{Scope} must not specify AgentId.");
        }
        if (!allowedUser && !string.IsNullOrWhiteSpace(UserId))
        {
            throw new InvalidOperationException(
                $"MemoryScope.{Scope} must not specify UserId.");
        }
        if (!allowedSession && !string.IsNullOrWhiteSpace(SessionId))
        {
            throw new InvalidOperationException(
                $"MemoryScope.{Scope} must not specify SessionId.");
        }
    }
}
