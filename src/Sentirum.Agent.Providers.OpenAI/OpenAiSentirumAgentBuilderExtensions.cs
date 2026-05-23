using System;
using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;

namespace Sentirum.Agent;

/// <summary>
/// OpenAI <see cref="ISentirumAgentBuilder"/> extensions.
/// </summary>
public static class OpenAiSentirumAgentBuilderExtensions
{
    /// <summary>
    /// Configures the agent to use the OpenAI chat completions API for the
    /// supplied <paramref name="model"/>.
    /// </summary>
    /// <param name="builder">The agent builder.</param>
    /// <param name="model">The OpenAI model identifier (e.g. <c>gpt-4o-mini</c>).</param>
    /// <param name="apiKey">
    /// Optional API key. When omitted, the underlying SDK falls back to the
    /// <c>OPENAI_API_KEY</c> environment variable.
    /// </param>
    /// <param name="configureFunctionInvocation">
    /// When <see langword="true"/> (the default), the chat client pipeline
    /// adds function-invocation middleware so tool calls dispatched to
    /// registered <see cref="AIFunction"/> instances are executed
    /// transparently.
    /// </param>
    public static ISentirumAgentBuilder UseOpenAI(
        this ISentirumAgentBuilder builder,
        string model,
        string? apiKey = null,
        bool configureFunctionInvocation = true)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        var resolvedKey = apiKey
            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            ?? throw new InvalidOperationException(
                "An OpenAI API key was not provided and the OPENAI_API_KEY " +
                "environment variable is not set.");

        var openAiClient = new OpenAIClient(new ApiKeyCredential(resolvedKey));

        builder
            .Configure(o => o.Model = model)
            .UseChatClient(_ => openAiClient.GetChatClient(model).AsIChatClient());

        if (configureFunctionInvocation)
        {
            builder.ConfigureChatClient(b => b.UseFunctionInvocation());
        }

        return builder;
    }
}
