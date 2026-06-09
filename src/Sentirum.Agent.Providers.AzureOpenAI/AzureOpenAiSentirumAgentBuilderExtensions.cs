using System;
using Azure;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.AI;

namespace Sentirum.Agent;

/// <summary>
/// Azure OpenAI <see cref="ISentirumAgentBuilder"/> extensions.
/// </summary>
public static class AzureOpenAiSentirumAgentBuilderExtensions
{
    /// <summary>
    /// Configures the agent to use the Azure OpenAI chat completions API.
    /// </summary>
    /// <param name="builder">The agent builder.</param>
    /// <param name="deployment">The Azure OpenAI deployment name (e.g. <c>gpt-4o</c>).</param>
    /// <param name="endpoint">The Azure OpenAI resource endpoint (e.g. <c>https://my-resource.openai.azure.com</c>).</param>
    /// <param name="apiKey">The Azure OpenAI API key. When omitted, falls back to the <c>AZURE_OPENAI_API_KEY</c> environment variable.</param>
    /// <param name="configureFunctionInvocation">
    /// When <see langword="true"/> (the default), adds function-invocation middleware.
    /// </param>
    public static ISentirumAgentBuilder UseAzureOpenAI(
        this ISentirumAgentBuilder builder,
        string deployment,
        Uri endpoint,
        string? apiKey = null,
        bool configureFunctionInvocation = true)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(deployment);
        ArgumentNullException.ThrowIfNull(endpoint);

        var resolvedKey = apiKey
            ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")
            ?? throw new InvalidOperationException(
                "An Azure OpenAI API key was not provided and the AZURE_OPENAI_API_KEY " +
                "environment variable is not set. Use the UseAzureOpenAI overload that " +
                "accepts a TokenCredential for Azure AD authentication.");

        var azureClient = new AzureOpenAIClient(endpoint, new AzureKeyCredential(resolvedKey));

        builder
            .Configure(o => o.Model = deployment)
            .UseChatClient(_ => azureClient.GetChatClient(deployment).AsIChatClient());

        if (configureFunctionInvocation)
        {
            builder.ConfigureChatClient(b => b.UseFunctionInvocation());
        }

        return builder;
    }

    /// <summary>
    /// Configures the agent to use the Azure OpenAI chat completions API with
    /// Azure Active Directory (Entra ID) authentication via
    /// <see cref="DefaultAzureCredential"/>.
    /// </summary>
    /// <param name="builder">The agent builder.</param>
    /// <param name="deployment">The Azure OpenAI deployment name.</param>
    /// <param name="endpoint">The Azure OpenAI resource endpoint.</param>
    /// <param name="credential">The Azure token credential. Defaults to <see cref="DefaultAzureCredential"/>.</param>
    /// <param name="configureFunctionInvocation">
    /// When <see langword="true"/> (the default), adds function-invocation middleware.
    /// </param>
    public static ISentirumAgentBuilder UseAzureOpenAI(
        this ISentirumAgentBuilder builder,
        string deployment,
        Uri endpoint,
        TokenCredential credential,
        bool configureFunctionInvocation = true)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(deployment);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(credential);

        var azureClient = new AzureOpenAIClient(endpoint, credential);

        builder
            .Configure(o => o.Model = deployment)
            .UseChatClient(_ => azureClient.GetChatClient(deployment).AsIChatClient());

        if (configureFunctionInvocation)
        {
            builder.ConfigureChatClient(b => b.UseFunctionInvocation());
        }

        return builder;
    }
}
