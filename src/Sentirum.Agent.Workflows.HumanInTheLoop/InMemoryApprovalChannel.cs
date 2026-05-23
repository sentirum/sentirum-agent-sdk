using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Sentirum.Agent.Workflows.HumanInTheLoop;

/// <summary>
/// In-memory single-process <see cref="IApprovalChannel"/>. Suitable
/// for unit tests, samples, and single-tenant demos.
/// </summary>
/// <remarks>
/// <para>
/// Requests and responses are buffered in unbounded
/// <see cref="System.Threading.Channels.Channel{T}"/> instances. Calling
/// <see cref="ApproveAsync"/> / <see cref="RejectAsync"/> from a
/// reviewer-side process completes any matching pending gate.
/// </para>
/// <para>
/// The channel does <b>not</b> persist state across process restarts.
/// For production hosts back this with a queue or database.
/// </para>
/// </remarks>
public sealed class InMemoryApprovalChannel : IApprovalChannel, IAsyncDisposable
{
    private readonly Channel<ApprovalRequest> _requests = Channel.CreateUnbounded<ApprovalRequest>();
    private readonly Channel<ApprovalResponse> _responses = Channel.CreateUnbounded<ApprovalResponse>();

    /// <inheritdoc />
    public async Task PublishAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _requests.Writer.WriteAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ApprovalResponse> ConsumeAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (await _responses.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (_responses.Reader.TryRead(out var response))
            {
                yield return response;
            }
        }
    }

    /// <summary>
    /// Streams pending approval requests to the reviewer side. Exposed
    /// for samples and tests; production reviewers typically read from
    /// whatever persistent queue the custom channel implementation uses.
    /// </summary>
    public async IAsyncEnumerable<ApprovalRequest> WatchRequestsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (await _requests.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (_requests.Reader.TryRead(out var request))
            {
                yield return request;
            }
        }
    }

    /// <summary>
    /// Reviewer-side helper that approves the gate identified by
    /// <paramref name="gateId"/>.
    /// </summary>
    public Task ApproveAsync(string gateId, string? reviewer = null, string? comment = null, string? requestId = null, IReadOnlyDictionary<string, string>? context = null)
        => RespondAsync(new ApprovalResponse(gateId, Approved: true, reviewer, comment, RequestId: requestId, Context: context));

    /// <summary>
    /// Reviewer-side helper that rejects the gate identified by
    /// <paramref name="gateId"/>.
    /// </summary>
    public Task RejectAsync(string gateId, string? reviewer = null, string? comment = null, string? requestId = null, IReadOnlyDictionary<string, string>? context = null)
        => RespondAsync(new ApprovalResponse(gateId, Approved: false, reviewer, comment, RequestId: requestId, Context: context));

    /// <summary>
    /// Pushes a raw response into the channel. Useful when the reviewer
    /// transport has already produced an <see cref="ApprovalResponse"/>.
    /// </summary>
    public async Task RespondAsync(ApprovalResponse response, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        await _responses.Writer.WriteAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _requests.Writer.TryComplete();
        _responses.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}
