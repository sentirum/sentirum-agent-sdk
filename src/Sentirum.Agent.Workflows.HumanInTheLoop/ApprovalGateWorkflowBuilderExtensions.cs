using System;
using System.Collections.Generic;
using Microsoft.Agents.AI.Workflows;

namespace Sentirum.Agent.Workflows.HumanInTheLoop;

/// <summary>
/// Builds the smallest "approval gate" topology on top of the MAF
/// <see cref="WorkflowBuilder"/>:
/// <code>
/// source ▶ requestComposer ▶ port ◀▶ outcomeProjector ▶ next
/// </code>
/// where <c>source</c> produces some payload, <c>requestComposer</c>
/// turns that payload into an <see cref="ApprovalRequest"/>, the
/// <see cref="ApprovalGate"/> suspends the run until a reviewer
/// responds, and <c>outcomeProjector</c> reshapes the response back
/// into the workflow's downstream message type.
/// </summary>
/// <remarks>
/// <para>
/// The pattern matches the official MAF HITL sample
/// (<c>WorkflowBuilder(numberRequestPort).AddEdge(...).AddEdge(...)</c>)
/// but lifts the request-port plumbing into a single call so consumers
/// do not have to reason about <see cref="ExternalRequest"/> envelopes.
/// </para>
/// <para>
/// Intentionally side-effect free with respect to <c>WithOutputFrom</c>:
/// callers are expected to declare workflow outputs explicitly so the
/// gate is reusable mid-flow.
/// </para>
/// </remarks>
public static class ApprovalGateWorkflowBuilderExtensions
{
    /// <summary>
    /// Wires an <see cref="ApprovalGate"/> between <paramref name="source"/>
    /// and a follow-up executor produced by
    /// <paramref name="onApproved"/> / <paramref name="onRejected"/>.
    /// </summary>
    /// <typeparam name="TSourceOutput">
    /// Output type of the source executor. Used by the request composer
    /// to populate <see cref="ApprovalRequest.Summary"/> /
    /// <see cref="ApprovalRequest.Context"/>.
    /// </typeparam>
    /// <typeparam name="TDownstream">
    /// Message type the downstream executor expects on its inbound edge.
    /// </typeparam>
    /// <param name="builder">The MAF workflow builder under construction.</param>
    /// <param name="source">The executor whose output triggers the gate.</param>
    /// <param name="gate">The approval gate to install.</param>
    /// <param name="requestComposer">
    /// Projects the source's payload onto an <see cref="ApprovalRequest"/>.
    /// </param>
    /// <param name="onApproved">
    /// Translates an approved verdict into the downstream message.
    /// </param>
    /// <param name="onRejected">
    /// Translates a rejected verdict into the downstream message.
    /// </param>
    /// <param name="composerId">
    /// Optional override for the request-composer executor id. Defaults
    /// to <c>{gateId}-composer</c>.
    /// </param>
    /// <param name="projectorId">
    /// Optional override for the response-projector executor id.
    /// Defaults to <c>{gateId}-projector</c>.
    /// </param>
    /// <returns>
    /// The downstream executor binding so callers can chain further
    /// edges from the same fluent expression.
    /// </returns>
    public static (WorkflowBuilder Builder, ExecutorBinding Downstream) WithApprovalGate<TSourceOutput, TDownstream>(
        this WorkflowBuilder builder,
        ExecutorBinding source,
        ApprovalGate gate,
        Func<TSourceOutput, ApprovalRequest> requestComposer,
        Func<ApprovalOutcome, TDownstream> onApproved,
        Func<ApprovalOutcome, TDownstream> onRejected,
        string? composerId = null,
        string? projectorId = null)
        where TSourceOutput : notnull
        where TDownstream : notnull
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(requestComposer);
        ArgumentNullException.ThrowIfNull(onApproved);
        ArgumentNullException.ThrowIfNull(onRejected);

        // Composer returns the ApprovalRequest as its output; the
        // composer→port edge added below routes it to the request port,
        // which emits the RequestInfoEvent the dispatcher listens for.
        // Do *not* call ctx.SendMessageAsync(...) here too — that would
        // double-deliver the request and produce two external requests
        // per source payload.
        //
        // Each request carries a fresh CorrelationId so the projector
        // can disambiguate multiple firings of the same gate in a
        // single run. The response must echo the same id back.
        var composerBinding = ((Func<TSourceOutput, IWorkflowContext, System.Threading.Tasks.ValueTask<ApprovalRequest>>)((payload, _) =>
        {
            var request = requestComposer(payload) with { CorrelationId = Guid.NewGuid().ToString("N") };
            return new System.Threading.Tasks.ValueTask<ApprovalRequest>(request);
        })).BindAsExecutor(composerId ?? $"{gate.Id}-composer", options: null, threadsafe: true);

        var projectorBinding = ((Func<ApprovalResponse, IWorkflowContext, System.Threading.Tasks.ValueTask<TDownstream>>)((response, _) =>
        {
            // The response must carry the same CorrelationId the
            // composer generated so we can match it back to the
            // originating request. If the response is missing the id
            // (legacy channel implementations) we fall back to the
            // GateId, which is safe only when the gate fires once per
            // run.
            var outcome = new ApprovalOutcome(
                new ApprovalRequest(
                    response.GateId,
                    "(unknown)",
                    "(unknown)",
                    response.Context ?? new Dictionary<string, string>(),
                    response.CorrelationId ?? response.GateId,
                    response.RequestId ?? string.Empty),
                response.Approved,
                response.Reviewer,
                response.Comment,
                response.CorrelationId);

            var downstream = outcome.Approved ? onApproved(outcome) : onRejected(outcome);
            return new System.Threading.Tasks.ValueTask<TDownstream>(downstream);
        })).BindAsExecutor(projectorId ?? $"{gate.Id}-projector", options: null, threadsafe: true);

        var portBinding = gate.Port.BindAsExecutor(allowWrappedRequests: false);

        builder
            .AddEdge(source, composerBinding)
            .AddEdge(composerBinding, portBinding)
            .AddEdge(portBinding, projectorBinding);

        return (builder, projectorBinding);
    }
}
