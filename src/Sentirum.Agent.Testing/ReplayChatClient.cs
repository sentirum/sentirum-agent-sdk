using System;
using System.Collections.Generic;
using System.IO;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace Sentirum.Agent.Testing;

/// <summary>
/// An <see cref="IChatClient"/> that replays responses from a previously
/// recorded fixture. Useful for fast, deterministic tests that do not need
/// to hit a real LLM endpoint.
/// </summary>
public sealed class ReplayChatClient : IChatClient
{
    private readonly ReadOnlyCollection<ChatInteraction> _interactions;
    private readonly MatchStrategy _matchStrategy;
    private int _nextIndex;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Defines how incoming requests are matched against recorded interactions.
    /// </summary>
    public enum MatchStrategy
    {
        /// <summary>
        /// Requires the exact same message count, roles, and text.
        /// </summary>
        Exact,

        /// <summary>
        /// Matches on message roles and text, ignoring order of system messages.
        /// </summary>
        Fuzzy,

        /// <summary>
        /// Returns interactions in strict recording order, ignoring request content.
        /// </summary>
        Sequential,
    }

    /// <summary>
    /// Initializes a new instance with the supplied interactions.
    /// </summary>
    public ReplayChatClient(IEnumerable<ChatInteraction> interactions, MatchStrategy matchStrategy = MatchStrategy.Exact)
    {
        ArgumentNullException.ThrowIfNull(interactions);
        _interactions = interactions.ToList().AsReadOnly();
        _matchStrategy = matchStrategy;

        if (_interactions.Count == 0)
        {
            throw new ArgumentException("At least one interaction is required.", nameof(interactions));
        }
    }

    /// <summary>
    /// Loads interactions from a JSON stream.
    /// </summary>
    public static ReplayChatClient LoadFrom(Stream stream, MatchStrategy matchStrategy = MatchStrategy.Exact)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var interactions = JsonSerializer.Deserialize<List<ChatInteraction>>(stream, s_jsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize interactions from stream.");
        return new ReplayChatClient(interactions, matchStrategy);
    }

    /// <summary>
    /// Loads interactions from a JSON string.
    /// </summary>
    public static ReplayChatClient LoadFromString(string json, MatchStrategy matchStrategy = MatchStrategy.Exact)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var interactions = JsonSerializer.Deserialize<List<ChatInteraction>>(json, s_jsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize interactions from JSON.");
        return new ReplayChatClient(interactions, matchStrategy);
    }

    /// <inheritdoc />
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var requestMessages = messages.ToList();
        var match = FindMatch(requestMessages);

        var responseMessages = match.ResponseMessages
            .Select(m => m.ToChatMessage())
            .ToList();

        var response = new ChatResponse(responseMessages)
        {
            Usage = match.Usage is null ? null : new Microsoft.Extensions.AI.UsageDetails
            {
                InputTokenCount = match.Usage.InputTokenCount,
                OutputTokenCount = match.Usage.OutputTokenCount,
                TotalTokenCount = match.Usage.TotalTokenCount,
            },
        };
        return Task.FromResult(response);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var requestMessages = messages.ToList();
        var match = FindMatch(requestMessages);

        return StreamCore(match, cancellationToken);
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> StreamCore(
        ChatInteraction match,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var recorded in match.ResponseMessages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new ChatResponseUpdate(new ChatRole(recorded.Role), recorded.Text);
            await Task.Yield();
        }
    }

    private ChatInteraction FindMatch(List<ChatMessage> requestMessages)
    {
        if (_matchStrategy == MatchStrategy.Sequential)
        {
            var idx = Interlocked.Increment(ref _nextIndex) - 1;
            if (idx >= _interactions.Count)
            {
                throw new InvalidOperationException(
                    $"ReplayChatClient ran out of recorded interactions. " +
                    $"Requested interaction {idx + 1} but only {_interactions.Count} are available.");
            }
            return _interactions[idx];
        }

        var requestRecorded = requestMessages.Select(RecordedMessage.FromChatMessage).ToList();

        foreach (var interaction in _interactions)
        {
            if (IsMatch(requestRecorded, interaction.RequestMessages))
            {
                return interaction;
            }
        }

        var requestPreview = string.Join(
            " | ",
            requestMessages.Select(m => $"{m.Role.Value}: {m.Text}"));

        throw new InvalidOperationException(
            $"ReplayChatClient could not find a matching recorded interaction for request: {requestPreview}");
    }

    private bool IsMatch(List<RecordedMessage> actual, List<RecordedMessage> expected)
    {
        if (_matchStrategy == MatchStrategy.Exact)
        {
            if (actual.Count != expected.Count)
            {
                return false;
            }

            for (var i = 0; i < actual.Count; i++)
            {
                if (!string.Equals(actual[i].Role, expected[i].Role, StringComparison.Ordinal) ||
                    !string.Equals(actual[i].Text, expected[i].Text, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        // Fuzzy: ignore order of system messages, match rest in order
        var actualSystem = actual.Where(m => m.Role.Equals("system", StringComparison.OrdinalIgnoreCase)).ToList();
        var expectedSystem = expected.Where(m => m.Role.Equals("system", StringComparison.OrdinalIgnoreCase)).ToList();
        var actualNonSystem = actual.Where(m => !m.Role.Equals("system", StringComparison.OrdinalIgnoreCase)).ToList();
        var expectedNonSystem = expected.Where(m => !m.Role.Equals("system", StringComparison.OrdinalIgnoreCase)).ToList();

        if (actualSystem.Count != expectedSystem.Count || actualNonSystem.Count != expectedNonSystem.Count)
        {
            return false;
        }

        foreach (var a in actualSystem)
        {
            if (!expectedSystem.Any(e => e.Text == a.Text))
            {
                return false;
            }
        }

        for (var i = 0; i < actualNonSystem.Count; i++)
        {
            if (actualNonSystem[i].Role != expectedNonSystem[i].Role ||
                actualNonSystem[i].Text != expectedNonSystem[i].Text)
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
