using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace Sentirum.Agent.Providers.ZAI;

/// <summary>
/// Delegating <see cref="IChatClient"/> that injects Z.AI's
/// <c>thinking: { type: "enabled" }</c> field into every outgoing request so
/// reasoning models emit their <c>reasoning_content</c>.
/// </summary>
internal sealed class ZaiThinkingChatClient : DelegatingChatClient
{
    private readonly bool _enabled;

    public ZaiThinkingChatClient(IChatClient innerClient, bool enabled)
        : base(innerClient)
    {
        _enabled = enabled;
    }

    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return base.GetResponseAsync(messages, ApplyThinking(options), cancellationToken);
    }

    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return base.GetStreamingResponseAsync(messages, ApplyThinking(options), cancellationToken);
    }

    private ChatOptions ApplyThinking(ChatOptions? options)
    {
        var resolved = options?.Clone() ?? new ChatOptions();
        resolved.AdditionalProperties ??= new AdditionalPropertiesDictionary();

        // Z.AI expects: { "thinking": { "type": "enabled" | "disabled" } }
        resolved.AdditionalProperties["thinking"] = JsonNode.Parse(
            JsonSerializer.Serialize(new { type = _enabled ? "enabled" : "disabled" }));

        return resolved;
    }
}
