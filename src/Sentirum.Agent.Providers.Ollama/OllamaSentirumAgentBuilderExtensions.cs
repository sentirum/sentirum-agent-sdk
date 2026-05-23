using System;
using Microsoft.Extensions.AI;
using OllamaSharp;

namespace Sentirum.Agent;

/// <summary>
/// Ollama <see cref="ISentirumAgentBuilder"/> extensions.
/// </summary>
public static class OllamaSentirumAgentBuilderExtensions
{
    /// <summary>
    /// Default Ollama endpoint (<c>http://localhost:11434</c>).
    /// </summary>
    public static readonly Uri DefaultEndpoint = new("http://localhost:11434");

    /// <summary>
    /// Configures the agent to use a local or remote Ollama server.
    /// </summary>
    /// <param name="builder">The agent builder.</param>
    /// <param name="model">Ollama model identifier (e.g. <c>llama3.2</c>, <c>qwen3:32b</c>).</param>
    /// <param name="endpoint">
    /// Optional Ollama endpoint. Defaults to <see cref="DefaultEndpoint"/>.
    /// </param>
    /// <param name="configureFunctionInvocation">
    /// When <see langword="true"/> (the default), enables function-invocation
    /// middleware. Only meaningful for tool-capable models.
    /// </param>
    public static ISentirumAgentBuilder UseOllama(
        this ISentirumAgentBuilder builder,
        string model,
        Uri? endpoint = null,
        bool configureFunctionInvocation = true)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        var resolvedEndpoint = endpoint ?? DefaultEndpoint;

        // OllamaApiClient implements IChatClient directly.
        var ollamaClient = new OllamaApiClient(resolvedEndpoint, model);

        builder
            .Configure(o => o.Model = model)
            .UseChatClient(_ => ollamaClient);

        if (configureFunctionInvocation)
        {
            builder.ConfigureChatClient(b => b.UseFunctionInvocation());
        }

        return builder;
    }
}
