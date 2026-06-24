using Microsoft.Extensions.Logging;

namespace Sentirum.Agent.Observability;

/// <summary>
/// Source-generated <see cref="LoggerMessage"/> delegates for the
/// Observability package.
/// </summary>
internal static partial class SentirumObservabilityLogging
{
    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Debug,
        Message = "Agent {AgentId} starting chat request")]
    public static partial void AgentRequestStarting(ILogger logger, string agentId);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Debug,
        Message = "Agent {AgentId} cost updated. Model: {Model}, Tokens: {Tokens}")]
    public static partial void AgentCostUpdated(ILogger logger, string agentId, string? model, long tokens);
}
