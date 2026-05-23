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
    /// Configures the inner-most <see cref="IChatClient"/> for this agent.
    /// </summary>
    /// <param name="chatClientFactory">
    /// Factory that resolves the leaf <see cref="IChatClient"/> from the
    /// service provider. Provider packages (OpenAI, Anthropic, Ollama, etc.)
    /// register the leaf client through this hook.
    /// </param>
    /// <remarks>
    /// Only one inner chat client may be set per agent. The most recently
    /// supplied factory wins.
    /// </remarks>
    ISentirumAgentBuilder UseChatClient(Func<IServiceProvider, IChatClient> chatClientFactory);

    /// <summary>
    /// Adds a delegating layer to the chat-client pipeline (e.g. function
    /// invocation, telemetry, retries, caching, custom middleware).
    /// </summary>
    /// <param name="configure">
    /// Callback that receives the in-flight <see cref="ChatClientBuilder"/>.
    /// Layers are applied in the order they are registered, so the first
    /// configured layer is the outermost client.
    /// </param>
    ISentirumAgentBuilder ConfigureChatClient(Action<ChatClientBuilder> configure);

    /// <summary>
    /// Adds a configuration delegate that runs against the resolved agent
    /// options after all builder extensions have contributed.
    /// </summary>
    ISentirumAgentBuilder Configure(Action<SentirumAgentOptions> configure);
}
