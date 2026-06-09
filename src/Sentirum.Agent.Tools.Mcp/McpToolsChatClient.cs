using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

namespace Sentirum.Agent.Tools.Mcp;

/// <summary>
/// A <see cref="DelegatingChatClient"/> that discovers tools from an MCP
/// server and injects them into every <see cref="ChatOptions"/> request.
/// The MCP client is created lazily on first use.
/// </summary>
public sealed class McpToolsChatClient : DelegatingChatClient
{
    private readonly IClientTransport? _transport;
    private McpClient? _client;
    private List<AIFunction>? _cachedTools;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance with the supplied MCP transport.
    /// The actual MCP client connection is deferred until the first
    /// chat request.
    /// </summary>
    public McpToolsChatClient(IChatClient innerClient, IClientTransport transport)
        : base(innerClient)
    {
        ArgumentNullException.ThrowIfNull(transport);
        _transport = transport;
    }

    /// <summary>
    /// Initializes a new instance with a pre-connected MCP client.
    /// </summary>
    public McpToolsChatClient(IChatClient innerClient, McpClient client)
        : base(innerClient)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    private async Task<McpClient> GetClientAsync(CancellationToken ct)
    {
        if (_client is not null)
        {
            return _client;
        }

        if (_transport is null)
        {
            throw new InvalidOperationException("No MCP transport configured.");
        }

        await _initLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _client ??= await McpClient.CreateAsync(_transport, cancellationToken: ct)
                .ConfigureAwait(false);
            return _client;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task<IReadOnlyList<AIFunction>> GetToolsAsync(CancellationToken ct)
    {
        if (_cachedTools is not null)
        {
            return _cachedTools;
        }

        var client = await GetClientAsync(ct).ConfigureAwait(false);

        await _initLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_cachedTools is not null)
            {
                return _cachedTools;
            }

            var mcpTools = await client.ListToolsAsync(cancellationToken: ct)
                .ConfigureAwait(false);

            _cachedTools = mcpTools.Cast<AIFunction>().ToList();
            return _cachedTools;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <inheritdoc />
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var tools = await GetToolsAsync(cancellationToken).ConfigureAwait(false);
        var merged = MergeTools(options, tools);
        return await base.GetResponseAsync(messages, merged, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var tools = await GetToolsAsync(cancellationToken).ConfigureAwait(false);
        var merged = MergeTools(options, tools);

        var stream = base.GetStreamingResponseAsync(messages, merged, cancellationToken);
        await foreach (var update in stream.ConfigureAwait(false))
        {
            yield return update;
        }
    }

    private static ChatOptions MergeTools(ChatOptions? options, IReadOnlyList<AIFunction> mcpTools)
    {
        var result = options is null
            ? new ChatOptions()
            : new ChatOptions
            {
                ModelId = options.ModelId,
                Temperature = options.Temperature,
                MaxOutputTokens = options.MaxOutputTokens,
                TopK = options.TopK,
                TopP = options.TopP,
                FrequencyPenalty = options.FrequencyPenalty,
                PresencePenalty = options.PresencePenalty,
                StopSequences = options.StopSequences?.ToList(),
                ResponseFormat = options.ResponseFormat,
                Seed = options.Seed,
                AdditionalProperties = options.AdditionalProperties is null
                    ? null
                    : new AdditionalPropertiesDictionary(options.AdditionalProperties),
            };

        var existing = options?.Tools?.ToList() ?? [];
        result.Tools = [.. existing, .. mcpTools];
        return result;
    }
}
