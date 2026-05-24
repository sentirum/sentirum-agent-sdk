using System;
using Microsoft.Extensions.AI;
using Sentirum.Agent.Providers.MiniMax;

namespace Sentirum.Agent;

/// <summary>
/// <see cref="ISentirumAgentBuilder"/> extensions for the MiniMax API.
/// Uses a raw HTTP client with full MiniMax API support including
/// <c>reasoning_split</c>.
/// </summary>
public static class MiniMaxSentirumAgentBuilderExtensions
{
    /// <summary>
    /// Configures the agent to use the MiniMax API via raw HTTP client.
    /// </summary>
    /// <param name="builder">The agent builder.</param>
    /// <param name="model">Model identifier (e.g. <c>MiniMax-M2.7</c>).</param>
    /// <param name="apiKey">MiniMax API key (Token Plan Key).</param>
    /// <param name="baseUrl">
    /// Optional base URL override. Defaults to <see cref="MiniMaxDefaults.OpenAIBaseUrl"/>.
    /// </param>
    /// <param name="reasoningSplit">
    /// When <see langword="true"/> (the default), the MiniMax API separates
    /// reasoning into a dedicated field and keeps <c>content</c> clean.
    /// </param>
    /// <param name="configureFunctionInvocation">
    /// When <see langword="true"/> (the default), enables function-invocation
    /// middleware for tool calling support.
    /// </param>
    public static ISentirumAgentBuilder UseMiniMax(
        this ISentirumAgentBuilder builder,
        string model,
        string apiKey,
        string? baseUrl = null,
        bool reasoningSplit = true,
        bool configureFunctionInvocation = true)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        builder
            .Configure(o => o.Model = model)
            .UseChatClient(_ => new MiniMaxChatClient(
                apiKey: apiKey,
                model: model,
                baseUrl: baseUrl,
                reasoningSplit: reasoningSplit));

        if (configureFunctionInvocation)
        {
            builder.ConfigureChatClient(b => b.UseFunctionInvocation());
        }

        return builder;
    }
}
