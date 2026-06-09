using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Sentirum.Agent.CustomerSupport;

/// <summary>
/// DI registration extensions for the customer support vertical.
/// </summary>
public static class SupportServiceCollectionExtensions
{
    /// <summary>
    /// Adds the customer support ticket store and related services.
    /// </summary>
    public static IServiceCollection AddCustomerSupport(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<ISupportTicketStore, InMemorySupportTicketStore>();
        return services;
    }

    /// <summary>
    /// Adds the customer support ticket store using a custom implementation.
    /// </summary>
    public static IServiceCollection AddCustomerSupport<TStore>(this IServiceCollection services)
        where TStore : class, ISupportTicketStore
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<ISupportTicketStore, TStore>();
        return services;
    }
}
