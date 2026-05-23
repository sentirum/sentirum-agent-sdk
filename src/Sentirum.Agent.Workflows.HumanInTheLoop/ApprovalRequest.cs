using System.Collections.Generic;

namespace Sentirum.Agent.Workflows.HumanInTheLoop;

/// <summary>
/// Payload sent over a Sentirum HITL request port when the workflow
/// reaches an approval gate. Crosses the workflow boundary via
/// <see cref="Microsoft.Agents.AI.Workflows.RequestPort"/>, so it is
/// kept JSON-serializable and free of MAF-specific types.
/// </summary>
/// <param name="GateId">
/// Identifier of the approval gate that produced this request. Maps
/// 1:1 to <see cref="ApprovalGate.Id"/> so reviewers and audit logs
/// can correlate request ↔ response across runs.
/// </param>
/// <param name="Title">Short headline shown to the reviewer.</param>
/// <param name="Summary">
/// Long-form description of what is being approved. Workflows
/// typically render the prior agent's recommendation here.
/// </param>
/// <param name="Context">
/// Optional structured key/value pairs (customer id, refund amount,
/// recommended action, ...) used by approval UIs to render rich
/// previews. Always non-null; an empty dictionary means "no extra
/// context".
/// </param>
public sealed record ApprovalRequest(
    string GateId,
    string Title,
    string Summary,
    IReadOnlyDictionary<string, string> Context);
