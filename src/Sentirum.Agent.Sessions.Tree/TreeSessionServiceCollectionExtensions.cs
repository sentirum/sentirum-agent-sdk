using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sentirum.Agent.Sessions.Tree;

namespace Sentirum.Agent;

/// <summary>
/// <see cref="IServiceCollection"/> extensions for registering tree-based
/// session storage.
/// </summary>
public static class TreeSessionServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="InMemoryTreeSessionStore"/> as the
    /// <see cref="ISentirumSessionStore"/> and <see cref="ITreeSessionStore"/>
    /// implementation. Replaces any previously registered store.
    /// </summary>
    public static IServiceCollection AddSentirumTreeSessions(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<InMemoryTreeSessionStore>();
        services.AddSingleton<ITreeSessionStore>(sp => sp.GetRequiredService<InMemoryTreeSessionStore>());

        // Replace, not add, so this beats the default in-memory store registered
        // by AddSentirumCore().
        services.RemoveAll<ISentirumSessionStore>();
        services.AddSingleton<ISentirumSessionStore>(sp =>
            sp.GetRequiredService<InMemoryTreeSessionStore>());

        return services;
    }
}
