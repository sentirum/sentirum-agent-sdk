using System;

namespace Sentirum.Agent;

/// <summary>
/// Discoverable aliases over <see cref="ISentirumAgentBuilder.Configure"/>
/// that make IntelliSense self-explanatory: <c>ConfigureOptions</c> reads
/// agent-level options, separate from <c>ConfigureChatClient</c> which
/// touches the IChatClient pipeline. See ADR-0003.
/// </summary>
public static class SentirumAgentBuilderConfigureExtensions
{
    /// <summary>
    /// Adds a configuration delegate that mutates the resolved
    /// <see cref="SentirumAgentOptions"/> for this agent.
    /// </summary>
    /// <remarks>
    /// Functionally identical to <see cref="ISentirumAgentBuilder.Configure"/>;
    /// kept as a clearer-named alias because <c>Configure</c> overloads tend
    /// to collide with chat-client and DI patterns.
    /// </remarks>
    public static ISentirumAgentBuilder ConfigureOptions(
        this ISentirumAgentBuilder builder,
        Action<SentirumAgentOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        return builder.Configure(configure);
    }
}
