using System;
using Microsoft.Extensions.DependencyInjection;

namespace Sentirum.Agent.Workflows;

/// <summary>
/// DI helpers for registering Sentirum workflows.
/// </summary>
public static class SentirumWorkflowServiceCollectionExtensions
{
    /// <summary>
    /// Registers a singleton <see cref="ISentirumWorkflow"/> built by the
    /// supplied <paramref name="configure"/> callback. The callback runs
    /// once at first resolution against the host's
    /// <see cref="IServiceProvider"/> so it can locate
    /// <see cref="ISentirumAgent"/> instances out of the agent registry.
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <param name="id">
    /// Stable workflow id; used as the default display name and the
    /// service registration key.
    /// </param>
    /// <param name="configure">
    /// Configuration callback. Receives the resolving
    /// <see cref="IServiceProvider"/> and a pre-named
    /// <see cref="SentirumWorkflowBuilder"/>.
    /// </param>
    public static IServiceCollection AddSentirumWorkflow(
        this IServiceCollection services,
        string id,
        Action<IServiceProvider, SentirumWorkflowBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddKeyedSingleton<ISentirumWorkflow>(id, (sp, key) =>
        {
            var builder = SentirumWorkflowBuilder.Create((string)key!);
            configure(sp, builder);
            return builder.Build();
        });

        return services;
    }
}
