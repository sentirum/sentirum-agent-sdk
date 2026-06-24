using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

namespace Sentirum.Agent.Testing;

/// <summary>
/// A single recorded interaction between a caller and an <see cref="IChatClient"/>.
/// </summary>
public sealed class ChatInteraction
{
    /// <summary>
    /// The request messages sent to the chat client.
    /// </summary>
    public required List<RecordedMessage> RequestMessages { get; init; }

    /// <summary>
    /// The request options (model, temperature, etc.) if any were supplied.
    /// </summary>
    public RecordedOptions? RequestOptions { get; init; }

    /// <summary>
    /// The response messages returned by the chat client.
    /// </summary>
    public required List<RecordedMessage> ResponseMessages { get; init; }

    /// <summary>
    /// Token usage reported by the provider, if available.
    /// </summary>
    public RecordedUsageDetails? Usage { get; init; }

    /// <summary>
    /// When the interaction was recorded.
    /// </summary>
    public DateTimeOffset RecordedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Optional human-friendly label for the interaction (e.g. "greeting", "tool_call").
    /// </summary>
    public string? Label { get; init; }
}

/// <summary>
/// Simplified, serializable representation of a <see cref="ChatMessage"/>.
/// </summary>
public sealed class RecordedMessage
{
    /// <summary>
    /// The message role (system, user, assistant, tool).
    /// </summary>
    public required string Role { get; init; }

    /// <summary>
    /// The message text content.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Converts a <see cref="ChatMessage"/> to a <see cref="RecordedMessage"/>.
    /// </summary>
    public static RecordedMessage FromChatMessage(ChatMessage message) =>
        new()
        {
            Role = message.Role.Value,
            Text = message.Text ?? string.Empty,
        };

    /// <summary>
    /// Converts this recorded message back to a <see cref="ChatMessage"/>.
    /// </summary>
    public ChatMessage ToChatMessage() =>
        new(new ChatRole(Role), Text);
}

/// <summary>
/// Simplified, serializable representation of <see cref="ChatOptions"/>.
/// </summary>
public sealed class RecordedOptions
{
    public string? ModelId { get; init; }
    public float? Temperature { get; init; }
    public int? MaxOutputTokens { get; init; }
    public int? TopK { get; init; }
    public float? TopP { get; init; }
    public float? FrequencyPenalty { get; init; }
    public float? PresencePenalty { get; init; }
    public List<string>? StopSequences { get; init; }

    /// <summary>
    /// Converts <see cref="ChatOptions"/> to <see cref="RecordedOptions"/>.
    /// </summary>
    public static RecordedOptions? FromChatOptions(ChatOptions? options)
    {
        if (options is null)
        {
            return null;
        }

        return new RecordedOptions
        {
            ModelId = options.ModelId,
            Temperature = options.Temperature,
            MaxOutputTokens = options.MaxOutputTokens,
            TopK = options.TopK,
            TopP = options.TopP,
            FrequencyPenalty = options.FrequencyPenalty,
            PresencePenalty = options.PresencePenalty,
            StopSequences = options.StopSequences is null ? null : new List<string>(options.StopSequences),
        };
    }
}

/// <summary>
/// Token usage details captured from a response.
/// </summary>
public sealed class RecordedUsageDetails
{
    public long? InputTokenCount { get; init; }
    public long? OutputTokenCount { get; init; }
    public long? TotalTokenCount { get; init; }

    /// <summary>
    /// Converts from <see cref="Microsoft.Extensions.AI.UsageDetails"/>.
    /// </summary>
    public static RecordedUsageDetails? FromUsage(Microsoft.Extensions.AI.UsageDetails? usage)
    {
        if (usage is null)
        {
            return null;
        }

        return new RecordedUsageDetails
        {
            InputTokenCount = usage.InputTokenCount,
            OutputTokenCount = usage.OutputTokenCount,
            TotalTokenCount = usage.TotalTokenCount,
        };
    }
}
