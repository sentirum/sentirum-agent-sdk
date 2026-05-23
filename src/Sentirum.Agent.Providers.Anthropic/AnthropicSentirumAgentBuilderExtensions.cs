using System;
using Anthropic;
using Microsoft.Extensions.AI;

namespace Sentirum.Agent;

/// <summary>
/// Anthropic <see cref="ISentirumAgentBuilder"/> extensions.
/// </summary>
public static class AnthropicSentirumAgentBuilderExtensions
{
    /// <summary>
    /// Configures the agent to use the Anthropic Messages API.
    /// </summary>
    /// <param name="builder">The agent builder.</param>
    /// <param name="model">The Anthropic model identifier (e.g. <c>claude-opus-4-6</c>).</param>
    /// <param name="apiKey">
    /// Optional API key. When omitted, the underlying SDK reads the
    /// <c>ANTHROPIC_API_KEY</c> environment variable.
    /// </param>
    /// <param name="maxTokens">
    /// Optional <c>max_tokens</c> ceiling applied to every request. The
    /// Anthropic API requires <c>max_tokens</c>; when omitted the SDK
    /// defaults are used.
    /// </param>
    /// <param name="configureFunctionInvocation">
    /// When <see langword="true"/> (the default) the chat-client pipeline
    /// installs function-invocation middleware so tool calls dispatched to
    /// registered <see cref="AIFunction"/> instances are executed
    /// transparently.
    /// </param>
    public static ISentirumAgentBuilder UseAnthropic(
        this ISentirumAgentBuilder builder,
        string model,
        string? apiKey = null,
        int? maxTokens = null,
        bool configureFunctionInvocation = true)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        var resolvedKey = apiKey
            ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
            ?? throw new InvalidOperationException(
                "An Anthropic API key was not provided and the " +
                "ANTHROPIC_API_KEY environment variable is not set.");

        var anthropicClient = new AnthropicClient()
            .WithOptions(o =>
            {
                o.ApiKey = resolvedKey;
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
