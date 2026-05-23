using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI.Workflows;

namespace Sentirum.Agent.Workflows;

/// <summary>
/// A Sentirum-facing wrapper around Microsoft Agent Framework's
/// <see cref="Workflow"/>. Hides the lower-level
/// <see cref="WorkflowBuilder"/> / <see cref="Run"/> ceremony so callers
/// can focus on agent composition.
/// </summary>
/// <remarks>
/// <para>
/// Implementations are expected to be safe to invoke concurrently —
/// each <see cref="RunAsync"/> / <see cref="StreamAsync"/> call opens
/// its own MAF run instance under the hood.
/// </para>
/// <para>
/// The wrapper deliberately exposes the underlying <see cref="Workflow"/>
/// via <see cref="InnerWorkflow"/> so advanced consumers can reach for
/// MAF-native APIs (visualization, custom hosting, checkpoint stores)
/// without forking the SDK.
/// </para>
/// </remarks>
public interface ISentirumWorkflow
{
    /// <summary>
    /// Gets a stable identifier for this workflow (typically the
    /// registered name).
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets a human-friendly display name for this workflow.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the underlying Microsoft Agent Framework workflow.
    /// </summary>
    Workflow InnerWorkflow { get; }

    /// <summary>
    /// Runs the workflow to completion and returns the aggregated result.
    /// </summary>
    /// <typeparam name="TInput">Type of the workflow input.</typeparam>
    /// <param name="input">Input payload handed to the start executor.</param>
    /// <param name="sessionId">
    /// Optional logical session id for telemetry / checkpoint correlation.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<SentirumWorkflowResult> RunAsync<TInput>(
        TInput input,
        string? sessionId = null,
        CancellationToken cancellationToken = default)
        where TInput : notnull;

    /// <summary>
    /// Runs the workflow and streams every <see cref="WorkflowEvent"/>
    /// as it is produced. The stream completes when the workflow halts.
    /// </summary>
    /// <typeparam name="TInput">Type of the workflow input.</typeparam>
    /// <param name="input">Input payload handed to the start executor.</param>
    /// <param name="sessionId">
    /// Optional logical session id for telemetry / checkpoint correlation.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    IAsyncEnumerable<WorkflowEvent> StreamAsync<TInput>(
        TInput input,
        string? sessionId = null,
        CancellationToken cancellationToken = default)
        where TInput : notnull;
}
