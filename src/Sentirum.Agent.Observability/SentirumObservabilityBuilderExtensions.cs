using System;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Sentirum.Agent.Observability;

/// <summary>
/// Builder extensions for adding observability layers to a Sentirum agent.
/// </summary>
public static class SentirumObservabilityBuilderExtensions
{
    /// <summary>
    /// Adds OpenTelemetry-compatible telemetry to the agent's chat-client
    /// pipeline. Emits <c>Activity</c> spans with tags for model, tokens,
    /// agent id, and agent name.
    /// </summary>
    public static ISentirumAgentBuilder WithTelemetry(this ISentirumAgentBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureChatClient(b => b.Use((client, services) =>
        {
            var logger = services.GetRequiredService<ILogger<SentirumChatClientTelemetry>>();
            return new SentirumChatClientTelemetry(
                client,
                builder.Name,
                builder.Name,
                logger);
        }));

        return builder;
    }

    /// <summary>
    /// Adds cost tracking to the agent. Requires an <see cref="ICostModel"/>
    /// to be registered in DI.
    /// </summary>
    public static ISentirumAgentBuilder WithCostTracking(this ISentirumAgentBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.TryAddSingleton<CostTrackingService>();

        builder.ConfigureChatClient(b => b.Use((client, services) =>
        {
            var tracker = services.GetRequiredService<CostTrackingService>();
            var logger = services.GetRequiredService<ILogger<CostTrackingChatClient>>();
            return new CostTrackingChatClient(client, builder.Name, tracker, logger);
        }));

        return builder;
    }

    /// <summary>
    /// Adds a hard token budget to the agent. When the accumulated tokens
    /// exceed <paramref name="maxTokens"/>, subsequent requests throw
    /// <see cref="TokenBudgetExceededException"/>.
    /// </summary>
    public static ISentirumAgentBuilder WithTokenBudget(
        this ISentirumAgentBuilder builder,
        long maxTokens)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureChatClient(b => b.Use(client =>
            new TokenBudgetChatClient(client, maxTokens)));

        return builder;
    }
}

/// <summary>
/// Internal decorator that pumps usage data into <see cref="CostTrackingService"/>.
/// </summary>
internal sealed class CostTrackingChatClient : DelegatingChatClient
{
    private readonly string _agentId;
    private readonly CostTrackingService _tracker;
    private readonly ILogger<CostTrackingChatClient> _logger;

    public CostTrackingChatClient(
        IChatClient innerClient,
        string agentId,
        CostTrackingService tracker,
        ILogger<CostTrackingChatClient> logger)
        : base(innerClient)
    {
        _agentId = agentId;
        _tracker = tracker;
        _logger = logger;
    }

    public override async Task<ChatResponse> GetResponseAsync(
        System.Collections.Generic.IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        System.Threading.CancellationToken cancellationToken = default)
    {
        var response = await base.GetResponseAsync(messages, options, cancellationToken)
            .ConfigureAwait(false);

        _tracker.Record(_agentId, null, options?.ModelId, response.Usage);
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            var tokens = response.Usage?.TotalTokenCount.GetValueOrDefault() ?? 0;
            SentirumObservabilityLogging.AgentCostUpdated(
                _logger,
                _agentId,
                options?.ModelId,
                tokens);
        }
        return response;
    }
}
