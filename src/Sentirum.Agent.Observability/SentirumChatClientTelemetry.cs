using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Sentirum.Agent.Observability;

/// <summary>
/// A <see cref="DelegatingChatClient"/> that emits OpenTelemetry-compatible
/// <see cref="Activity"/> spans for every chat request. Captures model,
/// token usage, and agent identity as tags.
/// </summary>
public sealed class SentirumChatClientTelemetry : DelegatingChatClient
{
    private static readonly ActivitySource s_activitySource = new("Sentirum.Agent");

    private readonly string _agentId;
    private readonly string _agentName;
    private readonly ILogger<SentirumChatClientTelemetry> _logger;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    public SentirumChatClientTelemetry(
        IChatClient innerClient,
        string agentId,
        string agentName,
        ILogger<SentirumChatClientTelemetry> logger)
        : base(innerClient)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);
        ArgumentNullException.ThrowIfNull(logger);

        _agentId = agentId;
        _agentName = agentName;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = StartActivity(options?.ModelId);
        try
        {
            var response = await base.GetResponseAsync(messages, options, cancellationToken)
                .ConfigureAwait(false);

            RecordUsage(activity, response.Usage);
            return response;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddTag("exception.type", ex.GetType().FullName);
            activity?.AddTag("exception.message", ex.Message);
            throw;
        }
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var activity = StartActivity(options?.ModelId);
        var updates = new List<ChatResponseUpdate>();
        Exception? captured = null;

        try
        {
            await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken)
                .ConfigureAwait(false))
            {
                updates.Add(update);
            }
        }
        catch (Exception ex)
        {
            captured = ex;
        }
        finally
        {
            if (captured is not null)
            {
                activity?.SetStatus(ActivityStatusCode.Error, captured.Message);
                activity?.AddTag("exception.type", captured.GetType().FullName);
                activity?.AddTag("exception.message", captured.Message);
            }
            activity?.Dispose();
        }

        if (captured is not null)
        {
            throw captured;
        }

        foreach (var update in updates)
        {
            yield return update;
        }
    }

    private Activity? StartActivity(string? modelId)
    {
        var activity = s_activitySource.StartActivity("chat", ActivityKind.Client);
        if (activity is null)
        {
            return null;
        }

        activity.SetTag("sentirum.agent.id", _agentId);
        activity.SetTag("sentirum.agent.name", _agentName);
        activity.SetTag("gen_ai.system", "sentirum");

        if (!string.IsNullOrEmpty(modelId))
        {
            activity.SetTag("gen_ai.request.model", modelId);
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            SentirumObservabilityLogging.AgentRequestStarting(_logger, _agentId);
        }

        return activity;
    }

    private static void RecordUsage(Activity? activity, UsageDetails? usage)
    {
        if (activity is null || usage is null)
        {
            return;
        }

        activity.SetTag("gen_ai.usage.input_tokens", usage.InputTokenCount.GetValueOrDefault());
        activity.SetTag("gen_ai.usage.output_tokens", usage.OutputTokenCount.GetValueOrDefault());
        activity.SetTag("gen_ai.usage.total_tokens", usage.TotalTokenCount.GetValueOrDefault());
    }
}
