using System;
using System.Runtime.CompilerServices;
using System.Text;
using Anthropic;
using Microsoft.Extensions.AI;
using Sentirum.Agent.Providers.MiniMax;

namespace Sentirum.Agent;

/// <summary>
/// <see cref="ISentirumAgentBuilder"/> extensions for the MiniMax API.
/// Supports both OpenAI-compatible and Anthropic-compatible wire protocols.
/// </summary>
public static class MiniMaxSentirumAgentBuilderExtensions
{
    /// <summary>
    /// Default base URL for the OpenAI-compatible MiniMax endpoint.
    /// </summary>
    public static readonly Uri DefaultOpenAIBaseUrl = new("https://api.minimax.io/v1");

    /// <summary>
    /// Default base URL for the Anthropic-compatible MiniMax endpoint.
    /// </summary>
    public static readonly Uri DefaultAnthropicBaseUrl = new("https://api.minimax.io/anthropic");

    /// <summary>
    /// Configures the agent to talk to the MiniMax API.
    /// </summary>
    /// <param name="builder">The agent builder.</param>
    /// <param name="model">
    /// Model identifier — for example <c>MiniMax-M2.7</c> or
    /// <c>MiniMax-M2.7-highspeed</c>.
    /// </param>
    /// <param name="apiKey">Your MiniMax API key (Token Plan Key).</param>
    /// <param name="protocol">
    /// Wire protocol to use. Defaults to <see cref="MiniMaxProtocol.OpenAI"/>.
    /// </param>
    /// <param name="configureFunctionInvocation">
    /// When <see langword="true"/> (the default), enables function-invocation
    /// middleware. Disable for providers that do not support tool calling.
    /// </param>
    /// <param name="separateThinking">
    /// When <see langword="true"/> (the default), adds the
    /// <see cref="MiniMaxThinkingMiddleware"/> to the pipeline to separate
    /// thinking content from the answer.
    /// </param>
    public static ISentirumAgentBuilder UseMiniMax(
        this ISentirumAgentBuilder builder,
        string model,
        string apiKey,
        MiniMaxProtocol protocol = MiniMaxProtocol.OpenAI,
        bool configureFunctionInvocation = true,
        bool separateThinking = true)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        return protocol switch
        {
            MiniMaxProtocol.OpenAI => UseOpenAIProtocol(builder, model, apiKey, configureFunctionInvocation, separateThinking),
            MiniMaxProtocol.Anthropic => UseAnthropicProtocol(builder, model, apiKey, configureFunctionInvocation, separateThinking),
            _ => throw new ArgumentOutOfRangeException(nameof(protocol), protocol, "Unsupported MiniMax protocol."),
        };
    }

    // ------------------------------------------------------------------
    // OpenAI-compatible protocol
    // ------------------------------------------------------------------

    private static ISentirumAgentBuilder UseOpenAIProtocol(
        ISentirumAgentBuilder builder,
        string model,
        string apiKey,
        bool configureFunctionInvocation,
        bool separateThinking)
    {
        // Delegate to the OpenAI provider with the MiniMax endpoint override.
        builder.UseOpenAI(
            model: model,
            apiKey: apiKey,
            endpoint: DefaultOpenAIBaseUrl,
            configureFunctionInvocation: configureFunctionInvocation);

        if (separateThinking)
        {
            builder.ConfigureChatClient(b => b.UseMiniMaxThinking());
        }

        return builder;
    }

    // ------------------------------------------------------------------
    // Anthropic-compatible protocol
    // ------------------------------------------------------------------

    private static ISentirumAgentBuilder UseAnthropicProtocol(
        ISentirumAgentBuilder builder,
        string model,
        string apiKey,
        bool configureFunctionInvocation,
        bool separateThinking)
    {
        var anthropicClient = new AnthropicClient()
            .WithOptions(o =>
            {
                o.BaseUrl = DefaultAnthropicBaseUrl.ToString();
                o.AuthToken = apiKey;
                return o;
            });

        builder
            .Configure(o => o.Model = model)
            .UseChatClient(_ => anthropicClient.AsIChatClient(model, null));

        if (configureFunctionInvocation)
        {
            builder.ConfigureChatClient(b => b.UseFunctionInvocation());
        }

        if (separateThinking)
        {
            builder.ConfigureChatClient(b => b.UseMiniMaxThinking());
        }

        return builder;
    }
}
