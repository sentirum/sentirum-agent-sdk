using System.Collections.Generic;

namespace Sentirum.Agent;

/// <summary>
/// Provides lookup access to all <see cref="ISentirumAgent"/> instances
/// registered with the host's dependency injection container.
/// </summary>
public interface ISentirumAgentRegistry
{
    /// <summary>
    /// Gets the registered agents.
    /// </summary>
    IReadOnlyCollection<ISentirumAgent> Agents { get; }

    /// <summary>
    /// Attempts to resolve the agent with the supplied identifier.
    /// </summary>
    /// <returns>
    /// The agent, or <see langword="null"/> when no agent is registered with
    /// the given identifier.
    /// </returns>
    ISentirumAgent? Find(string agentId);
}
