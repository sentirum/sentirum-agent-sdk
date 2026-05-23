using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sentirum.Agent.Memory.InMemory;

namespace Sentirum.Agent.Memory;

/// <summary>
/// DI registration helpers for the in-memory store.
/// </summary>
public static class InMemoryMemoryServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="InMemoryMemoryStore"/> as a singleton
    /// <see cref="ISentirumMemoryStore"/>. Safe to call multiple times.
    /// </summary>
    public static IServiceCollection AddSentirumInMemoryMemory(this IServiceCollection services)
    {
        services.TryAddSingleton<ISentirumMemoryStore, InMemoryMemoryStore>();
        return services;
    }
}
