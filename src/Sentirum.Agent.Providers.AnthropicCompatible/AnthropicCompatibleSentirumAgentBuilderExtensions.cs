using System;
using Anthropic;
using Microsoft.Extensions.AI;

namespace Sentirum.Agent;

/// <summary>
/// <see cref="ISentirumAgentBuilder"/> extensions for Anthropic-compatible
/// providers — any endpoint that implements the Anthropic Messages API.
/// </summary>
public static class AnthropicCompatibleSentirumAgentBuilderExtensions
{
    /// <summary>
    /// Configures the agent to talk to an Anthropic-compatible Messages
    /// endpoint (Z.AI's Anthropic route, Bedrock proxies, custom gateways).
    /// </summary>
    /// <param name="builder">The agent builder.</param>
    /// <param name="endpoint">
    /// Base endpoint for the provider — for example
    /// <c>https://api.z.ai/api/anthropic</c>.
    /// </param>
    /// <param name="model">
    /// Model identifier as exposed by the provider (e.g. <c>glm-4.7</c>).
    /// </param>
    /// <param name="authToken">
    /// Bearer token. Sent as <c>Authorization: Bearer ...</c>, which is what
    /// every non-canonical Anthropic-compatible gateway expects.
    /// </param>
    /// <param name="maxTokens">
    /// Optional <c>max_tokens</c> ceiling applied to every request.
    /// </param>
    /// <param name="configureFunctionInvocation">
    /// When <see langword="true"/> (the default), enables function-invocation
    /// middleware. Disable for gateways that do not support tool calling.
    /// </param>
    public static ISentirumAgentBuilder UseAnthropicCompatible(
        this ISentirumAgentBuilder builder,
        Uri endpoint,
        string model,
        string authToken,
        int? maxTokens = null,
        bool configureFunctionInvocation = true)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(authToken);

        var anthropicClient = new AnthropicClient()
            .WithOptions(o =>
            {
                o.BaseUrl = endpoint.ToString();
                o.AuthToken = authToken;
                return o;
            });

        builder
            .Configure(o => o.Model = model)
            .UseChatClient(_ => anthropicClient.AsIChatClient(model, maxTokens));

        if (configureFunctionInvocation)
        {
            builder.ConfigureChatClient(b => b.UseFunctionInvocation());
        }

        return builder;
    }
}
