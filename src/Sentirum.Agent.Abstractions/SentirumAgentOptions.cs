using System.Collections.Generic;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Sentirum.Agent;

/// <summary>
/// Options that describe how a single agent is configured.
/// </summary>
public sealed class SentirumAgentOptions
{
    /// <summary>
    /// Gets or sets the agent's logical name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a human-readable description of the agent.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the system instructions that prime the agent.
    /// </summary>
    public string? Instructions { get; set; }

    /// <summary>
    /// Gets or sets the model identifier used when resolving the underlying
    /// chat client (e.g. <c>"gpt-4o-mini"</c>). Optional; some providers
    /// embed the model in their own configuration.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Gets the AI functions (tools) registered on this agent.
    /// </summary>
    public IList<AIFunction> Tools { get; } = [];

    /// <summary>
    /// Gets the MAF <see cref="AIContextProvider"/> instances that enrich
    /// every chat request with extra messages, instructions, or tools.
    /// Populated by extensions such as
    /// <c>WithMemoryContext()</c>, <c>WithCustomerProfile&lt;T&gt;()</c>,
    /// and <c>WithKnowledgeBase(...)</c>. Order is preserved — providers
    /// run in registration order before the chat client and in reverse on
    /// the way back. See ADR-0003.
    /// </summary>
    public IList<AIContextProvider> ContextProviders { get; } = [];

    /// <summary>
    /// Gets a free-form metadata bag carried with the agent.
    /// </summary>
    public IDictionary<string, object?> Metadata { get; } =
        new Dictionary<string, object?>(System.StringComparer.Ordinal);
}
