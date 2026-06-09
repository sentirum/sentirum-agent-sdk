using System;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace Sentirum.Agent.Security;

/// <summary>
/// A <see cref="DelegatingChatClient"/> that scans outgoing messages through
/// an <see cref="IContentSafetyClient"/> and throws
/// <see cref="ContentSafetyException"/> when unsafe content is detected.
/// </summary>
public sealed class ContentSafetyChatClient : DelegatingChatClient
{
    private readonly IContentSafetyClient _safetyClient;
    private readonly ContentSafetyThreshold _threshold;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    public ContentSafetyChatClient(
        IChatClient innerClient,
        IContentSafetyClient safetyClient,
        ContentSafetyThreshold threshold = ContentSafetyThreshold.Medium)
        : base(innerClient)
    {
        ArgumentNullException.ThrowIfNull(safetyClient);
        _safetyClient = safetyClient;
        _threshold = threshold;
    }

    /// <inheritdoc />
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await ScanAsync(messages, cancellationToken).ConfigureAwait(false);
        return await base.GetResponseAsync(messages, options, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await ScanAsync(messages, cancellationToken).ConfigureAwait(false);
        await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken)
            .ConfigureAwait(false))
        {
            yield return update;
        }
    }

    private async Task ScanAsync(IEnumerable<ChatMessage> messages, CancellationToken ct)
    {
        foreach (var message in messages)
        {
            if (string.IsNullOrEmpty(message.Text))
            {
                continue;
            }

            var result = await _safetyClient.AnalyzeAsync(message.Text, ct)
                .ConfigureAwait(false);

            var violations = result.Categories
                .Where(c => c.Score >= (int)_threshold)
                .Select(c => c.Category)
                .ToList();

            if (violations.Count > 0)
            {
                throw new ContentSafetyException(
                    message.Text,
                    violations,
                    $"Content safety violation detected: {string.Join(", ", violations)}.");
            }
        }
    }
}

/// <summary>
/// Client that analyzes text for unsafe content.
/// </summary>
public interface IContentSafetyClient
{
    /// <summary>
    /// Analyzes the supplied text and returns safety scores per category.
    /// </summary>
    Task<ContentSafetyResult> AnalyzeAsync(string text, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a content safety analysis.
/// </summary>
public sealed class ContentSafetyResult
{
    /// <summary>
    /// Safety scores per category.
    /// </summary>
    public required IReadOnlyList<ContentSafetyScore> Categories { get; init; }
}

/// <summary>
/// A single category score.
/// </summary>
public sealed class ContentSafetyScore
{
    /// <summary>
    /// Category name (e.g. "hate", "violence", "self-harm").
    /// </summary>
    public required string Category { get; init; }

    /// <summary>
    /// Normalized score (0–100). Higher is more severe.
    /// </summary>
    public required int Score { get; init; }
}

/// <summary>
/// Threshold at which content is rejected.
/// </summary>
public enum ContentSafetyThreshold
{
    /// <summary>Allow all but the most severe content (score ≥ 90).</summary>
    High = 90,

    /// <summary>Reject moderate and severe content (score ≥ 50).</summary>
    Medium = 50,

    /// <summary>Reject any detected unsafe content (score ≥ 1).</summary>
    Low = 1,
}

/// <summary>
/// Thrown when content fails the safety scan.
/// </summary>
public sealed class ContentSafetyException : InvalidOperationException
{
    /// <summary>
    /// The original text that was rejected.
    /// </summary>
    public string OriginalText { get; }

    /// <summary>
    /// The categories that exceeded the threshold.
    /// </summary>
    public IReadOnlyList<string> Violations { get; }

    public ContentSafetyException(string originalText, IReadOnlyList<string> violations, string message)
        : base(message)
    {
        OriginalText = originalText;
        Violations = violations;
    }
}
