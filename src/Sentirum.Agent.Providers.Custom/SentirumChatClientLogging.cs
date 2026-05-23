using System;
using Microsoft.Extensions.Logging;

namespace Sentirum.Agent.Providers.Custom;

/// <summary>
/// Source-generated <see cref="LoggerMessage"/> delegates used by
/// <see cref="SentirumChatClientBase"/>. Keeps log message templates in one
/// place and avoids the boxing / allocation costs of the
/// <see cref="LoggerExtensions"/> helpers.
/// </summary>
internal static partial class SentirumChatClientLogging
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Sentirum custom provider '{Provider}' request: {MessageCount} messages.")]
    public static partial void LogRequest(this ILogger logger, string provider, int messageCount);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Sentirum custom provider '{Provider}' response: {ElapsedMs}ms.")]
    public static partial void LogResponse(this ILogger logger, string provider, long elapsedMs);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Information,
        Message = "Sentirum custom provider '{Provider}' streaming request: {MessageCount} messages.")]
    public static partial void LogStreamingRequest(this ILogger logger, string provider, int messageCount);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Error,
        Message = "Sentirum custom provider '{Provider}' failed after {ElapsedMs}ms.")]
    public static partial void LogFailure(this ILogger logger, Exception exception, string provider, long elapsedMs);
}
