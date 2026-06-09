using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace Sentirum.Agent.Testing;

/// <summary>
/// An <see cref="IChatClient"/> decorator that records every interaction
/// to an in-memory list. The recordings can be exported as JSON fixtures
/// for later replay by <see cref="ReplayChatClient"/>.
/// </summary>
public sealed class RecordingChatClient : IChatClient
{
    private readonly IChatClient _inner;
    private readonly List<ChatInteraction> _interactions = [];
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };


    /// <summary>
    /// Initializes a new instance that wraps <paramref name="inner"/>.
    /// </summary>
    public RecordingChatClient(IChatClient inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    /// <summary>
    /// Gets the interactions recorded so far.
    /// </summary>
    public IReadOnlyList<ChatInteraction> Interactions => _interactions;

    /// <inheritdoc />
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var requestMessages = messages.ToList();
        var response = await _inner.GetResponseAsync(requestMessages, options, cancellationToken)
            .ConfigureAwait(false);

        var interaction = new ChatInteraction
        {
            RequestMessages = requestMessages.Select(RecordedMessage.FromChatMessage).ToList(),
            RequestOptions = RecordedOptions.FromChatOptions(options),
            ResponseMessages = response.Messages.Select(RecordedMessage.FromChatMessage).ToList(),
            Usage = RecordedUsageDetails.FromUsage(response.Usage),
        };

        lock (_interactions)
        {
            _interactions.Add(interaction);
        }

        return response;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var requestMessages = messages.ToList();
        var updates = new List<ChatResponseUpdate>();

        var stream = _inner.GetStreamingResponseAsync(requestMessages, options, cancellationToken);
        await foreach (var update in stream.ConfigureAwait(false))
        {
            updates.Add(update);
            yield return update;
        }

        var interaction = new ChatInteraction
        {
            RequestMessages = requestMessages.Select(RecordedMessage.FromChatMessage).ToList(),
            RequestOptions = RecordedOptions.FromChatOptions(options),
            ResponseMessages = updates
                .Where(u => !string.IsNullOrEmpty(u.Text))
                .Select(u => new RecordedMessage { Role = u.Role?.Value ?? "assistant", Text = u.Text! })
                .ToList(),
            Usage = null, // streaming usage is typically not available per-chunk
        };

        lock (_interactions)
        {
            _interactions.Add(interaction);
        }
    }

    /// <summary>
    /// Serializes all recorded interactions to JSON and writes them to
    /// <paramref name="stream"/>.
    /// </summary>
    public void SaveTo(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var options = s_jsonOptions;
        JsonSerializer.Serialize(stream, _interactions, options);
    }

    /// <summary>
    /// Serializes all recorded interactions to a JSON string.
    /// </summary>
    public string SaveToString()
    {
        var options = s_jsonOptions;
        return JsonSerializer.Serialize(_interactions, options);
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null) =>
        _inner.GetService(serviceType, serviceKey);

    /// <inheritdoc />
    public void Dispose() => _inner.Dispose();
}
