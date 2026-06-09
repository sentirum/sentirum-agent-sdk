using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace Sentirum.Agent.Observability;

/// <summary>
/// A <see cref="DelegatingChatClient"/> that enforces a hard token budget.
/// When the accumulated tokens would exceed the budget, the client throws
/// <see cref="TokenBudgetExceededException"/> before forwarding the request.
/// </summary>
public sealed class TokenBudgetChatClient : DelegatingChatClient
{
    private readonly long _maxTokens;
    private long _consumedTokens;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="innerClient">The underlying chat client.</param>
    /// <param name="maxTokens">Maximum total tokens allowed (input + output).</param>
    public TokenBudgetChatClient(IChatClient innerClient, long maxTokens)
        : base(innerClient)
    {
        if (maxTokens <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxTokens),
                maxTokens,
                "Token budget must be greater than zero.");
        }

        _maxTokens = maxTokens;
    }

    /// <summary>
    /// Gets the remaining token budget.
    /// </summary>
    public long RemainingTokens => Math.Max(0, _maxTokens - _consumedTokens);

    /// <inheritdoc />
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var response = await base.GetResponseAsync(messages, options, cancellationToken)
            .ConfigureAwait(false);

        if (response.Usage is not null)
        {
            Accumulate(response.Usage.TotalTokenCount.GetValueOrDefault());
        }

        return response;
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await base.GetResponseAsync(messages, options, cancellationToken)
            .ConfigureAwait(false);

        if (response.Usage is not null)
        {
            Accumulate(response.Usage.TotalTokenCount.GetValueOrDefault());
        }

        foreach (var msg in response.Messages)
        {
            yield return new ChatResponseUpdate(msg.Role, msg.Text ?? string.Empty);
        }
    }

    private void Accumulate(long tokens)
    {
        var newTotal = Interlocked.Add(ref _consumedTokens, tokens);
        if (newTotal > _maxTokens)
        {
            throw new TokenBudgetExceededException(
                _maxTokens,
                newTotal,
                $"Token budget exceeded: {newTotal:N0} / {_maxTokens:N0} tokens.");
        }
    }
}

/// <summary>
/// Thrown when a request would cause the token budget to be exceeded.
/// </summary>
public sealed class TokenBudgetExceededException : InvalidOperationException
{
    /// <summary>
    /// The maximum allowed tokens.
    /// </summary>
    public long Budget { get; }

    /// <summary>
    /// The total tokens that would be consumed.
    /// </summary>
    public long Consumed { get; }

    public TokenBudgetExceededException(long budget, long consumed, string message)
        : base(message)
    {
        Budget = budget;
        Consumed = consumed;
    }
}
