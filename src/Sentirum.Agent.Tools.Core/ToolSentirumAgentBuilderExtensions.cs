using System;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sentirum.Agent.Builder;
using Sentirum.Agent.Tools;

namespace Sentirum.Agent;

/// <summary>
/// <see cref="ISentirumAgentBuilder"/> extensions for tool registration.
/// </summary>
public static class ToolSentirumAgentBuilderExtensions
{
    /// <summary>
    /// Discovers every <see cref="ToolAttribute"/>-decorated method on
    /// <typeparamref name="TToolset"/> and registers each as an
    /// <see cref="AIFunction"/> on the agent.
    /// </summary>
    /// <remarks>
    /// The toolset instance is resolved from DI when the agent is built, so
    /// tool methods may take constructor dependencies. Register the toolset
    /// yourself (typically via
    /// <c>services.AddSingleton&lt;TToolset&gt;()</c>) before resolving the
    /// agent.
    /// </remarks>
    public static ISentirumAgentBuilder WithTools<TToolset>(
        this ISentirumAgentBuilder builder)
        where TToolset : class
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.TryAddSingleton<TToolset>();

        return builder.Configure(options =>
        {
            // Deferred so DI is fully built. Captured via service provider
            // accessor in SentirumAgentFactory.Create(...) — the options
            // delegate runs inside that scope.
            var serviceProvider = SentirumServiceProviderAccessor.Current
                ?? throw new InvalidOperationException(
                    "WithTools requires a service provider scope. " +
                    "This usually means the builder ran outside the Sentirum factory.");

            var toolset = serviceProvider.GetRequiredService<TToolset>();
            foreach (var function in ToolDiscovery.Discover(toolset))
            {
                options.Tools.Add(function);
            }
        });
    }

    /// <summary>
    /// Registers the supplied delegate as a tool on the agent. Convenient
    /// for one-off lambdas; for richer scenarios prefer
    /// <see cref="WithTools{TToolset}"/>.
    /// </summary>
    public static ISentirumAgentBuilder WithToolDelegate(
        this ISentirumAgentBuilder builder,
        Delegate toolDelegate,
        string? name = null,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(toolDelegate);

        var function = AIFunctionFactory.Create(
            toolDelegate,
            new AIFunctionFactoryOptions
            {
                Name = name,
                Description = description,
            });

        return builder.WithTool(function);
    }
}
