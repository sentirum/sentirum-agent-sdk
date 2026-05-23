using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Sentirum.Agent;

/// <summary>
/// The primary abstraction for an agent in the Sentirum SDK.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ISentirumAgent"/> is a thin, opinionated wrapper around
/// Microsoft Agent Framework's <see cref="AIAgent"/>. It exposes a stable,
/// runtime-agnostic surface that consumers can rely on while allowing the
/// SDK to layer in tree-based sessions, custom providers, observability,
/// and policy enforcement.
/// </para>
/// <para>
/// Implementations are expected to be thread-safe and to host the
/// underlying <see cref="AIAgent"/> for the lifetime of the agent.
/// </para>
/// </remarks>
public interface ISentirumAgent
{
    /// <summary>
    /// Gets a stable identifier for this agent (typically the registered name).
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets a human-friendly display name for this agent.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the underlying Microsoft Agent Framework agent instance.
    /// </summary>
    /// <remarks>
    /// Exposed for advanced scenarios (workflow registration, custom
    /// middleware, etc.). Most consumers should prefer the high-level
    /// <see cref="RunAsync"/> and <see cref="RunStreamingAsync"/> methods.
    /// </remarks>
    AIAgent InnerAgent { get; }

    /// <summary>
    /// Runs the agent against the provided session and input message,
    /// returning a single aggregated response.
    /// </summary>
    /// <param name="session">The session that owns this conversation turn.</param>
    /// <param name="input">The user input message.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task<AgentResponse> RunAsync(
        ISentirumSession session,
        ChatMessage input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the agent against the provided session and input message,
    /// streaming response updates as they are produced.
    /// </summary>
    /// <param name="session">The session that owns this conversation turn.</param>
    /// <param name="input">The user input message.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    IAsyncEnumerable<AgentResponseUpdate> RunStreamingAsync(
        ISentirumSession session,
        ChatMessage input,
        CancellationToken cancellationToken = default);
}
