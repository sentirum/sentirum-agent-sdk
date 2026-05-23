using System;
using Microsoft.Extensions.AI;

namespace Sentirum.Agent;

/// <summary>
/// Convenience extensions over <see cref="ISentirumAgentBuilder"/> that wrap
/// the lower-level <see cref="ISentirumAgentBuilder.Configure"/> hook.
/// </summary>
public static class SentirumAgentBuilderExtensions
{
    /// <summary>
    /// Sets the system instructions / persona for the agent.
    /// </summary>
    public static ISentirumAgentBuilder WithInstructions(
        this ISentirumAgentBuilder builder,
        string instructions)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(instructions);

        return builder.Configure(o => o.Instructions = instructions);
    }

    /// <summary>
    /// Sets a human-readable description for the agent.
    /// </summary>
    public static ISentirumAgentBuilder WithDescription(
        this ISentirumAgentBuilder builder,
        string description)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        return builder.Configure(o => o.Description = description);
    }

    /// <summary>
    /// Sets the model identifier carried on the agent's options.
    /// </summary>
    /// <remarks>
    /// Most providers consume the model from their own configuration
    /// extension (e.g. <c>UseOpenAI(model)</c>) and ignore this value;
    /// it is stored on the options bag for diagnostics and metadata only.
    /// </remarks>
    public static ISentirumAgentBuilder WithModel(
        this ISentirumAgentBuilder builder,
        string model)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        return builder.Configure(o => o.Model = model);
    }

    /// <summary>
    /// Adds an <see cref="AIFunction"/> (tool) to the agent.
    /// </summary>
    public static ISentirumAgentBuilder WithTool(
        this ISentirumAgentBuilder builder,
        AIFunction tool)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(tool);

        return builder.Configure(o => o.Tools.Add(tool));
    }
}
