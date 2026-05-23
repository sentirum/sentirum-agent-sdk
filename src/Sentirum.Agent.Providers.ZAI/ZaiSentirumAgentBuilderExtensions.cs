using System;
using System.Collections.Generic;
using Microsoft.Extensions.AI;
using Sentirum.Agent.Providers.ZAI;

namespace Sentirum.Agent;

/// <summary>
/// Z.AI (GLM) <see cref="ISentirumAgentBuilder"/> extensions.
/// </summary>
public static class ZaiSentirumAgentBuilderExtensions
{
    /// <summary>
    /// Configures the agent to use Z.AI (GLM models).
    /// </summary>
    /// <param name="builder">The agent builder.</param>
    /// <param name="model">GLM model identifier (e.g. <c>glm-4.6</c>, <c>glm-4.7</c>, <c>glm-4.5-air</c>).</param>
    /// <param name="apiKey">
    /// Optional Z.AI API key. When omitted, falls back to the <c>ZAI_API_KEY</c>
    /// environment variable.
    /// </param>
    /// <param name="protocol">
    /// Wire protocol to use. Defaults to <see cref="ZaiProtocol.OpenAI"/>;
    /// switch to <see cref="ZaiProtocol.Anthropic"/> for the Anthropic-compatible endpoint.
    /// </param>
    /// <param name="maxTokens">
    /// Optional <c>max_tokens</c> ceiling. Only applied on the Anthropic
    /// protocol path (the OpenAI path lets the server decide).
    /// </param>
    public static ISentirumAgentBuilder UseZAI(
        this ISentirumAgentBuilder builder,
        string model,
        string? apiKey = null,
        ZaiProtocol protocol = ZaiProtocol.OpenAI,
        int? maxTokens = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        var resolvedKey = apiKey
            ?? Environment.GetEnvironmentVariable("ZAI_API_KEY")
            ?? throw new InvalidOperationException(
                "A Z.AI API key was not provided and the ZAI_API_KEY " +
                "environment variable is not set.");

        return protocol switch
        {
            ZaiProtocol.OpenAI => builder.UseOpenAICompatible(
                endpoint: ZaiEndpoints.OpenAI,
                model: model,
                apiKey: resolvedKey),

            ZaiProtocol.Anthropic => builder.UseAnthropicCompatible(
                endpoint: ZaiEndpoints.Anthropic,
                model: model,
                authToken: resolvedKey,
                maxTokens: maxTokens),

            _ => throw new ArgumentOutOfRangeException(nameof(protocol), protocol, null),
        };
    }

    /// <summary>
    /// Enables Z.AI's thinking mode for the agent. Adds
    /// <c>thinking: { type: "enabled" }</c> to every request, so reasoning
    /// models (<c>glm-4.6</c>, <c>glm-4.7</c>) emit their reasoning content.
    /// </summary>
    /// <remarks>
    /// This wraps the chat-client pipeline with a delegating layer that
    /// mutates <see cref="ChatOptions.AdditionalProperties"/>. Safe to call
    /// before or after the provider extension.
    /// </remarks>
    public static ISentirumAgentBuilder EnableZaiThinking(
        this ISentirumAgentBuilder builder,
        bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.ConfigureChatClient(b => b.Use((innerClient, _) =>
            new ZaiThinkingChatClient(innerClient, enabled)));
    }
}
