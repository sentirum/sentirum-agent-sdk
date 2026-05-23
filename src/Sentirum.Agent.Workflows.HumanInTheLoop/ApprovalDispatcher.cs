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
            await run.TrySendMessageAsync(new TurnToken(emitEvents: false)).ConfigureAwait(false);
        }

        // RequestId → originating ExternalRequest. We need the envelope
        // to call CreateResponse(...) once a verdict arrives.
        var pending = new ConcurrentDictionary<string, ExternalRequest>(StringComparer.Ordinal);
        // GateId → list of pending RequestIds waiting for this gate.
        var gateToRequests = new ConcurrentDictionary<string, ConcurrentQueue<string>>(StringComparer.Ordinal);

        var events = new List<WorkflowEvent>();
        var outputs = new List<object?>();
        var observed = new List<ApprovalRequest>();

        // Start the response pump concurrently so the channel's
        // ConsumeAsync stream is drained even while we are inside the
        // event loop. It exits when the workflow run halts.
        using var pumpCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var pumpTask = PumpResponsesAsync(run, channel, pending, gateToRequests, pumpCts.Token);

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

                        observed.Add(approvalRequest);
                        pending[req.Request.RequestId] = req.Request;
                        var queue = gateToRequests.GetOrAdd(
                            approvalRequest.GateId,
                            _ => new ConcurrentQueue<string>());
                        queue.Enqueue(req.Request.RequestId);

                        await channel
                            .PublishAsync(approvalRequest, cancellationToken)
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
        }

        var status = await run.GetStatusAsync(cancellationToken).ConfigureAwait(false);
        return new ApprovalRunResult(outputs, events, status, observed);
    }

    private static async Task PumpResponsesAsync(
        StreamingRun run,
        IApprovalChannel channel,
        ConcurrentDictionary<string, ExternalRequest> pending,
        ConcurrentDictionary<string, ConcurrentQueue<string>> gateToRequests,
        CancellationToken cancellationToken)
    {
        await foreach (var response in channel.ConsumeAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!gateToRequests.TryGetValue(response.GateId, out var queue)
                || !queue.TryDequeue(out var requestId)
                || !pending.TryRemove(requestId, out var externalRequest))
            {
                // Late or unmatched response — surface in the run's
                // event log for diagnostics but do not throw, the
                // workflow may have already halted.
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
