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

        ValidateOptions(options);

        _options = options;
        _logger = logger;
        _retryPipeline = BuildRetryPipeline(options);
    }

    private static void ValidateOptions(SentirumChatClientOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ProviderName))
        {
            throw new ArgumentException(
                "ProviderName must be a non-empty string.",
                nameof(options));
        }

        if (options.Timeout < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.Timeout,
                "Timeout cannot be negative. Use TimeSpan.Zero to disable.");
        }

        if (options.MaxRetries < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.MaxRetries,
                "MaxRetries cannot be negative. Use 0 to disable retries.");
        }

        if (options.RetryBaseDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.RetryBaseDelay,
                "RetryBaseDelay cannot be negative.");
        }
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
        cancellationToken.ThrowIfCancellationRequested();

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
        cancellationToken.ThrowIfCancellationRequested();

        var snapshot = messages as IList<ChatMessage> ?? new List<ChatMessage>(messages);
        var stopwatch = Stopwatch.StartNew();

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

        // Enumerate with WithCancellation so providers that don't observe
        // the token internally still terminate on caller cancellation or
        // timeout. Failures are logged once before re-throwing.
        IAsyncEnumerator<ChatResponseUpdate> enumerator;
        try
        {
            enumerator = CallProviderStreamingAsync(snapshot, options, token)
                .GetAsyncEnumerator(token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogFailure(ex, _options.ProviderName, stopwatch.ElapsedMilliseconds);
            throw;
        }

        await using (enumerator.ConfigureAwait(false))
        {
            while (true)
            {
                ChatResponseUpdate current;
                try
                {
                    if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                    {
                        break;
                    }

                    current = enumerator.Current;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogFailure(ex, _options.ProviderName, stopwatch.ElapsedMilliseconds);
                    throw;
                }

                yield return current;
            }
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
