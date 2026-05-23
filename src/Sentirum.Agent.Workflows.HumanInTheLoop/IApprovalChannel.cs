using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Sentirum.Agent.Workflows.HumanInTheLoop;

/// <summary>
/// Pluggable transport for shipping approval requests to a reviewer
/// and waiting for their verdict.
/// </summary>
/// <remarks>
/// <para>
/// The Sentirum HITL runtime drives an approval gate by publishing
/// <see cref="ApprovalRequest"/> instances to a channel and then
/// awaiting matching <see cref="ApprovalResponse"/> envelopes. The
/// default <see cref="InMemoryApprovalChannel"/> ships requests to an
/// in-memory queue for unit tests and single-process demos.
/// </para>
/// <para>
/// Production hosts plug their own implementation that posts the
/// request to Slack / Teams / a custom approval UI and persists the
/// pending state across process restarts.
/// </para>
/// </remarks>
public interface IApprovalChannel
{
    /// <summary>
    /// Hands a pending <see cref="ApprovalRequest"/> to the reviewer
    /// transport. Implementations should return immediately after the
    /// request has been durably enqueued.
    /// </summary>
    Task PublishAsync(ApprovalRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams responses as they arrive. The Sentirum runtime correlates
    /// each response back to its originating gate via
    /// <see cref="ApprovalResponse.GateId"/>, so a single channel can
    /// service many concurrent gates.
    /// </summary>
    IAsyncEnumerable<ApprovalResponse> ConsumeAsync(CancellationToken cancellationToken = default);
}
