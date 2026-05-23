using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace Sentirum.Agent.Providers.MiniMax;

/// <summary>
/// Chat client middleware that separates MiniMax thinking content
/// from the final answer. Thinking text is stored in
/// <see cref="ChatMessage.AdditionalProperties"/> under the
/// <see cref="ThinkingPropertyName"/> key.
/// </summary>
public sealed class MiniMaxThinkingMiddleware : DelegatingChatClient
{
    /// <summary>
    /// Property key used in <see cref="ChatMessage.AdditionalProperties"/>
    /// to store the extracted thinking content.
    /// </summary>
    public const string ThinkingPropertyName = "MiniMax.Thinking";

    /// <summary>
    /// Property key indicating whether the response is still
    /// in a thinking block (streaming scenario).
    /// </summary>
    public const string IsThinkingPropertyName = "MiniMax.IsThinking";

    public MiniMaxThinkingMiddleware(IChatClient innerClient)
        : base(innerClient) { }

    /// <inheritdoc />
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var response = await base.GetResponseAsync(messages, options, cancellationToken);

        if (response.Messages is { Count: > 0 })
        {
            for (var i = 0; i < response.Messages.Count; i++)
            {
                var msg = response.Messages[i];
                if (msg.Role == ChatRole.Assistant && msg.Text is not null)
                {
                    var (thinking, answer) = MiniMaxThinkingParser.Parse(msg.Text);

                    if (thinking is not null)
                    {
                        var props = msg.AdditionalProperties is not null
                            ? new AdditionalPropertiesDictionary(msg.AdditionalProperties)
                            : new AdditionalPropertiesDictionary();

                        props[ThinkingPropertyName] = thinking;

                        response.Messages[i] = new ChatMessage(msg.Role, answer)
                        {
                            AdditionalProperties = props,
                        };
                    }
                }
            }
        }

        return response;
    }

    /// <inheritdoc />
    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var innerStream = base.GetStreamingResponseAsync(messages, options, cancellationToken);
        return ProcessStreamAsync(innerStream, cancellationToken);
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> ProcessStreamAsync(
        IAsyncEnumerable<ChatResponseUpdate> stream,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var buffer = new StringBuilder();
        var thinkingDone = false;

        await foreach (var update in stream.WithCancellation(ct))
        {
            if (update.Text is null)
            {
                yield return update;
                continue;
            }

            if (thinkingDone)
            {
                yield return update;
                continue;
            }

            buffer.Append(update.Text);
            var buffered = buffer.ToString();

            if (MiniMaxThinkingParser.IsInThinkingBlock(buffered))
            {
                var thinkingProps = update.AdditionalProperties is not null
                    ? new AdditionalPropertiesDictionary(update.AdditionalProperties)
                    : new AdditionalPropertiesDictionary();

                thinkingProps[IsThinkingPropertyName] = true;

                yield return new ChatResponseUpdate(update.Role, update.Text)
                {
                    AdditionalProperties = thinkingProps,
                };
                continue;
            }

            var (thinking, answer) = MiniMaxThinkingParser.Parse(buffered);
            thinkingDone = true;
            buffer.Clear();

            if (thinking is not null)
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, thinking)
                {
                    AdditionalProperties = new AdditionalPropertiesDictionary
                    {
                        [ThinkingPropertyName] = thinking,
                    },
                };
            }

            if (!string.IsNullOrWhiteSpace(answer))
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, answer);
            }
        }
    }
}
