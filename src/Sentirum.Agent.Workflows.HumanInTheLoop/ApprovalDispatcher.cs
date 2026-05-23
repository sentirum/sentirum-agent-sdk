using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI.Workflows;
using Sentirum.Agent.Workflows;

namespace Sentirum.Agent.Workflows.HumanInTheLoop;

/// <summary>
/// Drives a Sentirum workflow that contains one or more
/// <see cref="ApprovalGate"/> instances against an
/// <see cref="IApprovalChannel"/>.
/// </summary>
/// <remarks>
/// <para>
/// The dispatcher streams the workflow run, intercepts every
/// <see cref="RequestInfoEvent"/> whose payload is an
/// <see cref="ApprovalRequest"/>, forwards it to the channel, and
/// pumps matching <see cref="ApprovalResponse"/> envelopes back into
/// the workflow. Non-approval workflow events are surfaced through
/// the returned <see cref="ApprovalRunResult"/> for diagnostics and
/// audit logging.
/// </para>
/// <para>
/// The dispatcher is intentionally agnostic about the channel
/// implementation. <see cref="InMemoryApprovalChannel"/> is fine for
/// tests; production hosts plug their own transport.
/// </para>
/// </remarks>
public static class ApprovalDispatcher
{
    /// <summary>
    /// Runs <paramref name="workflow"/> to completion against
    /// <paramref name="channel"/>, dispatching every approval request
    /// emitted from a Sentirum <see cref="ApprovalGate"/>.
    /// </summary>
    /// <typeparam name="TInput">Workflow input type.</typeparam>
    public static async Task<ApprovalRunResult> RunAsync<TInput>(
        ISentirumWorkflow workflow,
        TInput input,
        IApprovalChannel channel,
        string? sessionId = null,
        bool sendAgentTurnToken = false,
        CancellationToken cancellationToken = default)
        where TInput : notnull
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(channel);

        await using var run = await InProcessExecution
            .RunStreamingAsync(workflow.InnerWorkflow, input, sessionId, cancellationToken)
            .ConfigureAwait(false);

        // The dispatcher does *not* auto-send a TurnToken by default
        // because approval workflows are typically built via
        // WithApprovalGate on a custom WorkflowBuilder — a stray
        // TurnToken there would land on the request port's projector
        // and break the run. Opt in with sendAgentTurnToken when the
        // workflow embeds an MAF agent executor (assessor pattern) that
        // would otherwise wait for the token forever.
        if (sendAgentTurnToken)
        {
            var sent = await run.TrySendMessageAsync(new TurnToken(emitEvents: false)).ConfigureAwait(false);
            if (!sent)
            {
                throw new InvalidOperationException(
                    "Approval workflow could not dispatch TurnToken. " +
                    "Ensure the workflow contains at least one agent executor.");
            }
        }

        // RequestId → originating ExternalRequest. We need the envelope
        // to call CreateResponse(...) once a verdict arrives.
        var pending = new ConcurrentDictionary<string, ExternalRequest>(StringComparer.Ordinal);

        var events = new List<WorkflowEvent>();
        var outputs = new List<object?>();
        var observed = new List<ApprovalRequest>();

        // Start the response pump concurrently so the channel's
        // ConsumeAsync stream is drained even while we are inside the
        // event loop. It exits when the workflow run halts.
        using var pumpCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var pumpTask = PumpResponsesAsync(run, channel, pending, pumpCts.Token);

        try
        {
            await foreach (var evt in run.WatchStreamAsync(cancellationToken).ConfigureAwait(false))
            {
                events.Add(evt);

                switch (evt)
                {
                    case RequestInfoEvent req
                        when req.Request.TryGetDataAs(out ApprovalRequest? approvalRequest)
                        && approvalRequest is not null:

                        // Stamp the MAF RequestId onto the payload so the
                        // reviewer can echo it back for out-of-order routing.
                        var stamped = approvalRequest with { RequestId = req.Request.RequestId };
                        observed.Add(stamped);
                        pending[req.Request.RequestId] = req.Request;

                        await channel
                            .PublishAsync(stamped, cancellationToken)
                            .ConfigureAwait(false);
                        break;

                    case WorkflowOutputEvent output:
                        outputs.Add(output.Data);
                        break;
                }
            }
        }
        finally
        {
            pumpCts.Cancel();
            try
            {
                await pumpTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when we cancel the pump after the workflow halts.
            }
            catch (Exception pumpEx)
            {
                // The pump may fail (e.g. channel disposed while iterating).
                // We do not let this swallow the workflow result; surface it
                // via a synthetic error event so callers can observe it in
                // the run log.
                events.Add(new WorkflowErrorEvent(pumpEx));
            }
        }

        var status = await run.GetStatusAsync(cancellationToken).ConfigureAwait(false);
        return new ApprovalRunResult(outputs, events, status, observed);
    }

    private static async Task PumpResponsesAsync(
        StreamingRun run,
        IApprovalChannel channel,
        ConcurrentDictionary<string, ExternalRequest> pending,
        CancellationToken cancellationToken)
    {
        await foreach (var response in channel.ConsumeAsync(cancellationToken).ConfigureAwait(false))
        {
            // Out-of-order responses are safe because we route by the
            // MAF RequestId the dispatcher stashed when the gate fired,
            // not by a FIFO queue per gate.
            var requestId = response.RequestId ?? string.Empty;
            if (string.IsNullOrEmpty(requestId)
                || !pending.TryRemove(requestId, out var externalRequest))
            {
                // Late or unmatched response — the workflow may have
                // already halted or the response carries an id from a
                // previous run. Drop silently.
                continue;
            }

            await run
                .SendResponseAsync(externalRequest.CreateResponse(response))
                .ConfigureAwait(false);
        }
    }
}

/// <summary>
/// Outcome of an <see cref="ApprovalDispatcher.RunAsync{TInput}"/> call.
/// </summary>
/// <param name="Outputs">All workflow outputs in emission order.</param>
/// <param name="Events">Complete workflow event log.</param>
/// <param name="Status">Final <see cref="RunStatus"/>.</param>
/// <param name="ApprovalRequests">
/// Every <see cref="ApprovalRequest"/> the dispatcher forwarded to the
/// channel. Useful for assertions in tests and for rendering an audit
/// trail after the run.
/// </param>
public sealed record ApprovalRunResult(
    IReadOnlyList<object?> Outputs,
    IReadOnlyList<WorkflowEvent> Events,
    RunStatus Status,
    IReadOnlyList<ApprovalRequest> ApprovalRequests);
