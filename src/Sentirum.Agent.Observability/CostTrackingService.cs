using System;
using System.Collections.Concurrent;
using System.Threading;
using Microsoft.Extensions.AI;

namespace Sentirum.Agent.Observability;

/// <summary>
/// Accumulates cost and token usage across agents, users, and sessions.
/// Thread-safe; intended as a singleton in DI.
/// </summary>
public sealed class CostTrackingService
{
    private readonly ICostModel _costModel;
    private readonly ConcurrentDictionary<string, AgentMetrics> _byAgent = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, UserMetrics> _byUser = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance with the supplied cost model.
    /// </summary>
    public CostTrackingService(ICostModel costModel)
    {
        ArgumentNullException.ThrowIfNull(costModel);
        _costModel = costModel;
    }

    /// <summary>
    /// Records a single request/response pair.
    /// </summary>
    public void Record(
        string agentId,
        string? userId,
        string? modelId,
        UsageDetails? usage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);

        var cost = _costModel.CalculateCost(modelId, usage);

        _byAgent.AddOrUpdate(
            agentId,
            _ => new AgentMetrics(1, cost, usage),
            (_, existing) => existing.Add(1, cost, usage));

        if (!string.IsNullOrEmpty(userId))
        {
            _byUser.AddOrUpdate(
                userId,
                _ => new UserMetrics(1, cost, usage),
                (_, existing) => existing.Add(1, cost, usage));
        }
    }

    /// <summary>
    /// Gets cumulative metrics for an agent.
    /// </summary>
    public AgentMetrics GetAgentMetrics(string agentId) =>
        _byAgent.TryGetValue(agentId, out var metrics) ? metrics : AgentMetrics.Empty;

    /// <summary>
    /// Gets cumulative metrics for a user.
    /// </summary>
    public UserMetrics GetUserMetrics(string userId) =>
        _byUser.TryGetValue(userId, out var metrics) ? metrics : UserMetrics.Empty;

    /// <summary>
    /// Resets all accumulated metrics.
    /// </summary>
    public void Reset()
    {
        _byAgent.Clear();
        _byUser.Clear();
    }
}

/// <summary>
/// Immutable snapshot of metrics for an agent.
/// </summary>
public readonly record struct AgentMetrics(
    long RequestCount,
    decimal TotalCostUsd,
    long InputTokens,
    long OutputTokens,
    long TotalTokens)
{
    public static AgentMetrics Empty => new(0, 0m, 0, 0, 0);

    internal AgentMetrics Add(long requests, decimal cost, UsageDetails? usage)
    {
        return new AgentMetrics(
            RequestCount + requests,
            TotalCostUsd + cost,
            InputTokens + (usage?.InputTokenCount ?? 0),
            OutputTokens + (usage?.OutputTokenCount ?? 0),
            TotalTokens + (usage?.TotalTokenCount ?? 0));
    }

    internal AgentMetrics(long requests, decimal cost, UsageDetails? usage)
        : this(
            requests,
            cost,
            usage?.InputTokenCount ?? 0,
            usage?.OutputTokenCount ?? 0,
            usage?.TotalTokenCount ?? 0)
    {
    }
}

/// <summary>
/// Immutable snapshot of metrics for a user.
/// </summary>
public readonly record struct UserMetrics(
    long RequestCount,
    decimal TotalCostUsd,
    long InputTokens,
    long OutputTokens,
    long TotalTokens)
{
    public static UserMetrics Empty => new(0, 0m, 0, 0, 0);

    internal UserMetrics Add(long requests, decimal cost, UsageDetails? usage)
    {
        return new UserMetrics(
            RequestCount + requests,
            TotalCostUsd + cost,
            InputTokens + (usage?.InputTokenCount ?? 0),
            OutputTokens + (usage?.OutputTokenCount ?? 0),
            TotalTokens + (usage?.TotalTokenCount ?? 0));
    }

    internal UserMetrics(long requests, decimal cost, UsageDetails? usage)
        : this(
            requests,
            cost,
            usage?.InputTokenCount ?? 0,
            usage?.OutputTokenCount ?? 0,
            usage?.TotalTokenCount ?? 0)
    {
    }
}
