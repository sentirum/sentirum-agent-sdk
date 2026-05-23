using System;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Sentirum.Agent;

/// <summary>
/// Fluent builder for constructing a <see cref="ISentirumAgent"/>.
/// </summary>
/// <remarks>
/// Provider, tools, middleware, memory, and observability concerns are all
/// expressed as extension methods over this builder. The builder defers all
/// resolution to the underlying <see cref="IServiceCollection"/> so that
/// agents compose cleanly with dependency injection.
/// </remarks>
public interface ISentirumAgentBuilder
{
    /// <summary>
    /// Gets the logical name of the agent being built.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the underlying service collection. Use this to register
    /// dependencies (providers, tools, options) needed by the agent.
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// Configures the <see cref="IChatClient"/> used by this agent.
    /// </summary>
    /// <param name="configure">
    /// Callback that receives a <see cref="ChatClientBuilder"/> for layering
    /// behaviors (function invocation, OpenTelemetry, caching, rate limiting,
    /// custom delegating clients, etc.).
    /// </param>
    ISentirumAgentBuilder UseChatClient(Action<ChatClientBuilder> configure);

    /// <summary>
    /// Adds a configuration delegate that runs against the resolved agent
    /// options after all builder extensions have contributed.
    /// </summary>
    ISentirumAgentBuilder Configure(Action<SentirumAgentOptions> configure);
}
