using Microsoft.Agents.AI;

namespace Sentirum.Agent;

/// <summary>
/// Represents a single conversational session with a Sentirum agent.
/// </summary>
/// <remarks>
/// <para>
/// A Sentirum session is the unit of conversational state. It tracks the
/// thread of messages, attached context providers, and any session-scoped
/// state.
/// </para>
/// <para>
/// In contrast to a flat <see cref="AgentSession"/>, the Sentirum SDK
/// supports tree-based sessions where each session may have a parent and
/// multiple branches (see <see cref="ParentId"/> and forking APIs in the
/// session store). The default implementation is linear.
/// </para>
/// </remarks>
public interface ISentirumSession
{
    /// <summary>
    /// Gets a unique identifier for this session.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the identifier of the parent session if this session was forked
    /// from another; otherwise <see langword="null"/>.
    /// </summary>
    string? ParentId { get; }

    /// <summary>
    /// Gets the identifier of the agent that owns this session.
    /// </summary>
    string AgentId { get; }

    /// <summary>
    /// Gets the underlying Microsoft Agent Framework session.
    /// </summary>
    /// <remarks>
    /// Exposed so providers and middleware can interact with the same
    /// state object as the underlying <see cref="AIAgent"/>. May be
    /// <see langword="null"/> for sessions that have not yet been bound
    /// to a concrete agent.
    /// </remarks>
    AgentSession? InnerSession { get; }
}
