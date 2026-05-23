using System;

namespace Sentirum.Agent;

/// <summary>
/// <see cref="ISentirumAgentBuilder"/> extensions for OpenAI-compatible
/// providers — any endpoint that implements the OpenAI Chat Completions
/// wire format.
/// </summary>
public static class OpenAiCompatibleSentirumAgentBuilderExtensions
{
    /// <summary>
    /// Configures the agent to talk to an OpenAI-compatible chat completions
    /// endpoint (Groq, Together, vLLM, LM Studio, Z.AI, OpenRouter, etc.).
    /// </summary>
    /// <param name="builder">The agent builder.</param>
    /// <param name="endpoint">
    /// Base endpoint for the provider — for example
    /// <c>https://api.groq.com/openai/v1</c> or
    /// <c>https://api.z.ai/api/paas/v4</c>.
    /// </param>
    /// <param name="model">
    /// Model identifier as exposed by the provider (e.g.
    /// <c>llama-3.3-70b-versatile</c>, <c>glm-4.6</c>).
    /// </param>
    /// <param name="apiKey">
    /// API key. Required because OpenAI-compatible providers do not share the
    /// OpenAI SDK's <c>OPENAI_API_KEY</c> fallback semantics.
    /// </param>
    /// <param name="configureFunctionInvocation">
    /// When <see langword="true"/> (the default), enables function-invocation
    /// middleware. Disable for providers that do not support tool calling.
    /// </param>
    public static ISentirumAgentBuilder UseOpenAICompatible(
        this ISentirumAgentBuilder builder,
        Uri endpoint,
        string model,
        string apiKey,
        bool configureFunctionInvocation = true)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        // Delegate to the OpenAI provider with the endpoint override. The
        // OpenAI SDK speaks the Chat Completions wire format and is happy
        // to talk to any spec-compliant gateway.
        return builder.UseOpenAI(
            model: model,
            apiKey: apiKey,
            endpoint: endpoint,
            configureFunctionInvocation: configureFunctionInvocation);
    }
}
