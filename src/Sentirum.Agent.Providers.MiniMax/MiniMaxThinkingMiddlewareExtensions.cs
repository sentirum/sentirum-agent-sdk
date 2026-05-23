using System;
using Microsoft.Extensions.AI;

namespace Sentirum.Agent.Providers.MiniMax;

/// <summary>
/// <see cref="ChatClientBuilder"/> extension to add MiniMax thinking
/// separation middleware to the pipeline.
/// </summary>
public static class MiniMaxThinkingMiddlewareExtensions
{
    /// <summary>
    /// Adds the <see cref="MiniMaxThinkingMiddleware"/> to the chat client
    /// pipeline. Thinking content is extracted from ႒...yec süslü parantez tags
    /// and stored in <see cref="ChatMessage.AdditionalProperties"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.ConfigureChatClient(c => c.UseMiniMaxThinking());
    /// </code>
    /// </example>
    public static ChatClientBuilder UseMiniMaxThinking(this ChatClientBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.Use(inner => new MiniMaxThinkingMiddleware(inner));
    }
}
