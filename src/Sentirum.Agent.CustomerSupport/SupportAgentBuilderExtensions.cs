using System;
using Microsoft.Extensions.DependencyInjection;
using Sentirum.Agent.CustomerSupport.Sentiment;

namespace Sentirum.Agent.CustomerSupport;

/// <summary>
/// Entry points for the <see cref="SupportAgentBuilder"/>.
/// </summary>
public static class SupportAgentBuilderExtensions
{
    /// <summary>
    /// Wraps an <see cref="ISentirumAgentBuilder"/> in an opinionated
    /// <see cref="SupportAgentBuilder"/> so support-specific methods
    /// (<c>WithTier1</c>, <c>WithEscalation</c>,
    /// <c>WithSentimentBasedEscalation</c>, …) become available. Select the
    /// provider on the inner builder first, then switch to the support
    /// surface:
    /// <code>
    /// services.AddSentirumAgent("tier1", b => b
    ///     .UseZAI("glm-4.6", apiKey: key)
    ///     .AsSupportAgent()
    ///         .WithTier1()
    ///         .WithPiiRedaction()
    ///         .WithSentimentBasedEscalation());
    /// </code>
    /// </summary>
    public static SupportAgentBuilder AsSupportAgent(this ISentirumAgentBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return new SupportAgentBuilder(builder);
    }

    /// <summary>
    /// Registers a named customer-support agent and exposes a
    /// <see cref="SupportAgentBuilder"/> for configuration. Use
    /// <see cref="SupportAgentBuilder.Inner"/> to select the chat provider.
    /// <code>
    /// services.AddSentirumSupportAgent("tier1", b => b
    ///     .Inner.UseOpenAI("gpt-4o-mini", apiKey: key)
    ///     .WithTier1()
    ///     .WithPiiRedaction()
    ///     .WithSentimentBasedEscalation());
    /// </code>
    /// </summary>
    public static IServiceCollection AddSentirumSupportAgent(
        this IServiceCollection services,
        string name,
        Action<SupportAgentBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);

        return services.AddSentirumAgent(name, inner =>
        {
            var support = new SupportAgentBuilder(inner);
            configure(support);
        });
    }
}
