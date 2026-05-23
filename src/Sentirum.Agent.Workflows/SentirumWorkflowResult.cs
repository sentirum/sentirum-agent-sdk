using System.Collections.Generic;
using Microsoft.Agents.AI.Workflows;

namespace Sentirum.Agent.Workflows;

/// <summary>
/// The aggregated result of a non-streaming workflow run.
/// </summary>
/// <param name="Outputs">
/// All <see cref="WorkflowOutputEvent"/> payloads emitted by the
/// workflow's terminal executors, in emission order.
/// </param>
/// <param name="Events">
/// Full event log captured during the run. Useful for diagnostics,
/// auditing, and approval-trail rendering.
/// </param>
/// <param name="Status">
/// Final <see cref="RunStatus"/> reported by the MAF run. A workflow
/// that suspended on a <see cref="RequestPort"/> reports
/// <see cref="RunStatus.PendingRequests"/>.
/// </param>
public sealed record SentirumWorkflowResult(
    IReadOnlyList<object?> Outputs,
    IReadOnlyList<WorkflowEvent> Events,
    RunStatus Status);
