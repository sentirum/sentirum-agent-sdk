using System;
using System.Collections.Generic;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Sentirum.Agent.Builder;

/// <summary>
/// Default <see cref="ISentirumAgentBuilder"/> implementation. Builders are
/// short-lived: they collect configuration during DI registration and the
/// resolved <see cref="ISentirumAgent"/> is materialized by the factory
/// registered into the container.
/// </summary>
public sealed class SentirumAgentBuilder : ISentirumAgentBuilder
{
    private readonly List<Action<ChatClientBuilder>> _chatClientLayers = [];
    private readonly List<Action<SentirumAgentOptions>> _optionsConfigurations = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="SentirumAgentBuilder"/> class.
    /// </summary>
    public SentirumAgentBuilder(string name, IServiceCollection services)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(services);

        Name = name;
        Services = services;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public IServiceCollection Services { get; }

    /// <summary>
    /// Gets the leaf chat-client factory, or <see langword="null"/> if no
    /// provider has been registered yet.
    /// </summary>
    public Func<IServiceProvider, IChatClient>? ChatClientFactory { get; private set; }

    /// <summary>
    /// Gets the registered chat-client pipeline layers in order.
    /// </summary>
    public IReadOnlyList<Action<ChatClientBuilder>> ChatClientLayers => _chatClientLayers;

    /// <summary>
    /// Gets the registered options configuration delegates in order.
    /// </summary>
    public IReadOnlyList<Action<SentirumAgentOptions>> OptionsConfigurations
        => _optionsConfigurations;

    /// <inheritdoc />
    public ISentirumAgentBuilder UseChatClient(Func<IServiceProvider, IChatClient> chatClientFactory)
    {
        ArgumentNullException.ThrowIfNull(chatClientFactory);
        ChatClientFactory = chatClientFactory;
        return this;
    }

    /// <inheritdoc />
    public ISentirumAgentBuilder ConfigureChatClient(Action<ChatClientBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _chatClientLayers.Add(configure);
        return this;
    }

    /// <inheritdoc />
    public ISentirumAgentBuilder Configure(Action<SentirumAgentOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _optionsConfigurations.Add(configure);
        return this;
    }
}
