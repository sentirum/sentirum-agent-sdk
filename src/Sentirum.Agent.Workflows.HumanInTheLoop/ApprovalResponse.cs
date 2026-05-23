namespace Sentirum.Agent.Workflows.HumanInTheLoop;

/// <summary>
/// Reviewer's verdict for a paused <see cref="ApprovalRequest"/>.
/// </summary>
/// <param name="GateId">
/// Must match the <see cref="ApprovalRequest.GateId"/> the workflow
/// suspended on. The dispatcher uses this to route the response back
/// to the correct gate when multiple gates are pending in the same
/// run.
/// </param>
/// <param name="Approved">
/// <see langword="true"/> if the reviewer approved, <see langword="false"/>
/// to reject.
/// </param>
/// <param name="Reviewer">
/// Optional reviewer identifier (operator name, agent id, etc.).
/// Persisted in the workflow event log for audit purposes.
/// </param>
/// <param name="Comment">
/// Optional free-form note. Appended to the workflow event log and
/// surfaced through <see cref="ApprovalOutcome.Comment"/>.
/// </param>
/// <param name="CorrelationId">
/// Must match the <see cref="ApprovalRequest.CorrelationId"/> the
/// composer embedded in the original request so the projector can
/// disambiguate multiple firings of the same gate in a single run.
/// </param>
/// <param name="RequestId">
/// The MAF ExternalRequest.RequestId that the dispatcher generated for
/// this pending request. Enables out-of-order response routing without
/// relying on a FIFO queue per gate.
/// </param>
public sealed record ApprovalResponse(
    string GateId,
    bool Approved,
    string? Reviewer = null,
    string? Comment = null,
    string? CorrelationId = null,
    string? RequestId = null);
