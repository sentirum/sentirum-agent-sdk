using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sentirum.Agent.Memory.Redis;
using StackExchange.Redis;

namespace Sentirum.Agent.Memory;

/// <summary>
/// DI registration helpers for the Redis-backed memory store.
/// </summary>
public static class RedisMemoryServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="RedisMemoryStore"/> as <see cref="ISentirumMemoryStore"/>.
    /// Expects an <see cref="IConnectionMultiplexer"/> to already be in DI;
    /// register one via <c>services.AddSingleton&lt;IConnectionMultiplexer&gt;(_ =&gt; ConnectionMultiplexer.Connect(...))</c>.
    /// </summary>
    public static IServiceCollection AddSentirumRedisMemory(
        this IServiceCollection services,
        Action<RedisMemoryStoreOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            services.AddOptions<RedisMemoryStoreOptions>();
        }

        services.TryAddSingleton<ISentirumMemoryStore, RedisMemoryStore>();
        return services;
    }

    /// <summary>
    /// Convenience overload that wires the <see cref="IConnectionMultiplexer"/>
    /// from a Redis connection string.
    /// </summary>
    public static IServiceCollection AddSentirumRedisMemory(
        this IServiceCollection services,
        string connectionString,
        Action<RedisMemoryStoreOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        services.TryAddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(connectionString));
        return services.AddSentirumRedisMemory(configure);
    }
}
