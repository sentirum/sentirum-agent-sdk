using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sentirum.Agent.Memory.EntityFrameworkCore;

namespace Sentirum.Agent.Memory;

/// <summary>
/// DI registration helpers for the EF Core memory store.
/// </summary>
public static class EfCoreMemoryServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="EfCoreMemoryStore{TContext}"/> as
    /// <see cref="ISentirumMemoryStore"/>. The host is responsible for
    /// registering <typeparamref name="TContext"/> (typically via
    /// <c>services.AddDbContext&lt;TContext&gt;(...)</c>).
    /// </summary>
    public static IServiceCollection AddSentirumEfCoreMemory<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<ISentirumMemoryStore, EfCoreMemoryStore<TContext>>();
        return services;
    }

    /// <summary>
    /// Convenience overload: registers a fresh <see cref="SentirumMemoryDbContext"/>
    /// (configured by <paramref name="configureDbContext"/>) plus the store.
    /// </summary>
    public static IServiceCollection AddSentirumEfCoreMemory(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDbContext)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureDbContext);

        services.AddDbContext<SentirumMemoryDbContext>(configureDbContext);
        return services.AddSentirumEfCoreMemory<SentirumMemoryDbContext>();
    }
}
