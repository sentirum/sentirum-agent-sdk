using System;
using Microsoft.Agents.AI;

namespace Sentirum.Agent;

/// <summary>
/// Default linear <see cref="ISentirumSession"/> implementation backed by a
/// Microsoft Agent Framework <see cref="AgentSession"/>.
/// </summary>
public sealed class SentirumSession : ISentirumSession
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SentirumSession"/> class.
    /// </summary>
    public SentirumSession(
        string id,
        string agentId,
        AgentSession? innerSession,
        string? parentId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);

        Id = id;
        AgentId = agentId;
        ParentId = parentId;
        InnerSession = innerSession;
    }

    /// <inheritdoc />
    public string Id { get; }

    /// <inheritdoc />
    public string? ParentId { get; }

    /// <inheritdoc />
    public string AgentId { get; }

    /// <inheritdoc />
    public AgentSession? InnerSession { get; }
}
