using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace Sentirum.Agent.Providers.Custom;

/// <summary>
/// Abstract base for custom <see cref="IChatClient"/> providers. Override
/// <see cref="CallProviderAsync"/> and <see cref="CallProviderStreamingAsync"/>
/// to plug in any transport (HTTP, gRPC, queue, SignalR, etc.); the base
/// handles timeouts, retries with exponential backoff, structured logging,
/// and basic telemetry.
/// </summary>
/// <remarks>
/// <para>
/// Retries are only applied to the non-streaming call path. Streaming calls
/// run with a timeout (when configured) but are <em>not</em> retried, since
/// chunks may already have been emitted to the caller.
/// </para>
/// <para>
/// Implementations should treat retryable failures (network errors, 429,
/// 5xx) as exceptions; the base class catches every exception that is not
/// an <see cref="OperationCanceledException"/> bound to the caller's token.
/// </para>
/// </remarks>
public abstract class SentirumChatClientBase : IChatClient
{
    private readonly SentirumChatClientOptions _options;
    private readonly ILogger _logger;
    private readonly ResiliencePipeline _retryPipeline;

    /// <summary>
    /// Initializes a new instance of the <see cref="SentirumChatClientBase"/> class.
    /// </summary>
    protected SentirumChatClientBase(SentirumChatClientOptions options, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options;
        _logger = logger;
        _retryPipeline = BuildRetryPipeline(options);
    }

    /// <summary>
    /// Gets the options that govern retry, timeout, and logging behavior.
    /// </summary>
    protected SentirumChatClientOptions Options => _options;

    /// <summary>
    /// Gets the logger used for diagnostics.
    /// </summary>
    protected ILogger Logger => _logger;

    /// <summary>
    /// Calls the underlying provider for a non-streaming completion.
    /// Implementations should throw on transport errors so the base can retry.
    /// </summary>
    protected abstract Task<ChatResponse> CallProviderAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        CancellationToken cancellationToken);

    /// <summary>
    /// Calls the underlying provider for a streaming completion.
    /// </summary>
    protected abstract IAsyncEnumerable<ChatResponseUpdate> CallProviderStreamingAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        CancellationToken cancellationToken);

    /// <inheritdoc />
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        // Snapshot the messages so the retry policy can replay them.
        var snapshot = messages as IList<ChatMessage> ?? new List<ChatMessage>(messages);
        var stopwatch = Stopwatch.StartNew();

        if (_options.LogRequests)
        {
            _logger.LogRequest(_options.ProviderName, snapshot.Count);
        }

        try
        {
            var response = await _retryPipeline.ExecuteAsync(
                async ct => await CallProviderAsync(snapshot, options, ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);

            if (_options.LogRequests)
            {
                _logger.LogResponse(_options.ProviderName, stopwatch.ElapsedMilliseconds);
            }

            return response;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogFailure(ex, _options.ProviderName, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var snapshot = messages as IList<ChatMessage> ?? new List<ChatMessage>(messages);

        if (_options.LogRequests)
        {
            _logger.LogStreamingRequest(_options.ProviderName, snapshot.Count);
        }

        using var timeoutCts = _options.Timeout > TimeSpan.Zero
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : null;

        if (timeoutCts is not null)
        {
            timeoutCts.CancelAfter(_options.Timeout);
        }

        var token = timeoutCts?.Token ?? cancellationToken;

        await foreach (var update in CallProviderStreamingAsync(snapshot, options, token)
            .ConfigureAwait(false))
        {
            yield return update;
        }
    }

    /// <inheritdoc />
    public virtual object? GetService(Type serviceType, object? serviceKey = null) => null;

    /// <summary>
    /// Disposes any owned resources. Override to dispose your transport.
    /// </summary>
    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    private static ResiliencePipeline BuildRetryPipeline(SentirumChatClientOptions options)
    {
        var builder = new ResiliencePipelineBuilder();

        if (options.Timeout > TimeSpan.Zero)
        {
            builder.AddTimeout(options.Timeout);
        }

        if (options.MaxRetries > 0)
        {
            builder.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = options.MaxRetries,
                BackoffType = DelayBackoffType.Exponential,
                Delay = options.RetryBaseDelay,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder()
                    .Handle<Exception>(ex => ex is not OperationCanceledException),
            });
        }

        return builder.Build();
    }
}
