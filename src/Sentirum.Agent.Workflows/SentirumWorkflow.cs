using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI.Workflows;

namespace Sentirum.Agent.Workflows;

/// <summary>
/// Whether the workflow needs the wrapper to send a <see cref="TurnToken"/>
/// after the input is queued. MAF agent executors cache messages and
/// only fire on a <c>TurnToken</c>; custom workflows route their input
/// directly and would be broken if a stray token landed on an
/// untyped executor.
/// </summary>
internal enum WorkflowDispatchMode
{
    /// <summary>Custom workflow — do not auto-dispatch a TurnToken.</summary>
    Direct,

    /// <summary>Agent workflow — auto-dispatch one TurnToken per run.</summary>
    AgentTurn,
}

/// <summary>
/// Default <see cref="ISentirumWorkflow"/> built on top of MAF's
/// <see cref="InProcessExecution"/> host.
/// </summary>
/// <remarks>
/// <para>
/// The wrapper does <b>not</b> hold a long-lived <see cref="Run"/> /
/// <see cref="StreamingRun"/>. A fresh run is created per
/// <see cref="RunAsync{TInput}"/> / <see cref="StreamAsync{TInput}"/>
/// call, which keeps the type safe to share across concurrent callers
/// (the underlying <see cref="Workflow"/> instance is immutable).
/// </para>
/// <para>
/// Callers that need lower-level control (custom checkpoint stores,
/// suspend/resume cycles, sub-workflow hosting) should reach for
/// <see cref="ISentirumWorkflow.InnerWorkflow"/> and drive
/// <see cref="InProcessExecution"/> directly.
/// </para>
/// </remarks>
public sealed class SentirumWorkflow : ISentirumWorkflow
{
    private readonly Workflow _workflow;
    private readonly WorkflowDispatchMode _dispatchMode;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="id">Stable identifier (used for telemetry).</param>
    /// <param name="name">Human-friendly display name.</param>
    /// <param name="workflow">The underlying MAF workflow.</param>
    public SentirumWorkflow(string id, string name, Workflow workflow)
        : this(id, name, workflow, WorkflowDispatchMode.AgentTurn)
    {
    }

    internal SentirumWorkflow(string id, string name, Workflow workflow, WorkflowDispatchMode dispatchMode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(workflow);

        Id = id;
        Name = name;
        _workflow = workflow;
        _dispatchMode = dispatchMode;
    }

    /// <inheritdoc />
    public string Id { get; }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public Workflow InnerWorkflow => _workflow;

    /// <inheritdoc />
    public async Task<SentirumWorkflowResult> RunAsync<TInput>(
        TInput input,
        string? sessionId = null,
        CancellationToken cancellationToken = default)
        where TInput : notnull
    {
        ArgumentNullException.ThrowIfNull(input);

        // Use the streaming API even for the non-streaming surface so we
        // get the complete event log (outputs + diagnostics) in a single
        // pass without a second round-trip through the host.
        await using var run = await InProcessExecution
            .RunStreamingAsync(_workflow, input, sessionId, cancellationToken)
            .ConfigureAwait(false);

        // MAF agent executors cache incoming messages and only fire on a
        // TurnToken. Sentirum auto-sends one for AgentTurn workflows so
        // they don't idle. Custom workflows must opt out (Direct mode)
        // because a stray TurnToken would land on an executor whose
        // input type does not accept it.
        if (_dispatchMode == WorkflowDispatchMode.AgentTurn)
        {
            await run.TrySendMessageAsync(new TurnToken(emitEvents: true)).ConfigureAwait(false);
        }

        var events = new List<WorkflowEvent>();
        var outputs = new List<object?>();

        await foreach (var evt in run.WatchStreamAsync(cancellationToken).ConfigureAwait(false))
        {
            events.Add(evt);
            if (evt is WorkflowOutputEvent output)
            {
                outputs.Add(output.Data);
            }
        }

        var status = await run.GetStatusAsync(cancellationToken).ConfigureAwait(false);
        return new SentirumWorkflowResult(outputs, events, status);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<WorkflowEvent> StreamAsync<TInput>(
        TInput input,
        string? sessionId = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
        where TInput : notnull
    {
        ArgumentNullException.ThrowIfNull(input);

        await using var run = await InProcessExecution
            .RunStreamingAsync(_workflow, input, sessionId, cancellationToken)
            .ConfigureAwait(false);

        // See note in RunAsync — same single-shot Sentirum contract.
        if (_dispatchMode == WorkflowDispatchMode.AgentTurn)
        {
            await run.TrySendMessageAsync(new TurnToken(emitEvents: true)).ConfigureAwait(false);
        }

        await foreach (var evt in run.WatchStreamAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return evt;
        }
    }
}
