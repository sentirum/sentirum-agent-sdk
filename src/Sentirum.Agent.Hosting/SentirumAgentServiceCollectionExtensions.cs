using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sentirum.Agent.Builder;

namespace Sentirum.Agent;

/// <summary>
/// <see cref="IServiceCollection"/> extensions for registering Sentirum agents.
/// </summary>
public static class SentirumAgentServiceCollectionExtensions
{
    /// <summary>
    /// Registers a named Sentirum agent with the container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">
    /// Logical name of the agent. Doubles as the keyed-service key and the
    /// agent's <see cref="ISentirumAgent.Id"/>.
    /// </param>
    /// <param name="configure">Builder callback that selects provider, tools, and options.</param>
    /// <returns>The supplied service collection, for chaining.</returns>
    public static IServiceCollection AddSentirumAgent(
        this IServiceCollection services,
        string name,
        Action<ISentirumAgentBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);

        // Make sure the shared infrastructure is registered exactly once.
        AddSentirumCore(services);

        var builder = new SentirumAgentBuilder(name, services);
        configure(builder);

        // Register the agent both as a keyed singleton (so callers can resolve
        // by name) and as an unkeyed enumerable contributor (so the registry
        // can enumerate every agent).
        services.AddKeyedSingleton<ISentirumAgent>(
            name,
            (sp, _) => SentirumAgentFactory.Create(builder, sp));

        services.AddSingleton<ISentirumAgent>(
            sp => sp.GetRequiredKeyedService<ISentirumAgent>(name));

        return services;
    }

    /// <summary>
    /// Registers Sentirum's core hosting services. Safe to call multiple times.
    /// </summary>
    public static IServiceCollection AddSentirumCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<ISentirumAgentRegistry, SentirumAgentRegistry>();
        services.TryAddSingleton<ISentirumSessionStore, InMemorySessionStore>();

        return services;
    }
}
