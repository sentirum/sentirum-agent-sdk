using Microsoft.Agents.AI.Workflows;

namespace Sentirum.Agent.Workflows.HumanInTheLoop;

/// <summary>
/// Identifies a human approval checkpoint inside a Sentirum workflow.
/// </summary>
/// <remarks>
/// <para>
/// An <see cref="ApprovalGate"/> wraps a MAF
/// <see cref="RequestPort{TRequest, TResponse}"/> typed against
/// <see cref="ApprovalRequest"/> /
/// <see cref="ApprovalResponse"/>. Building blocks above this layer
/// (the workflow builder, the dispatcher) treat the gate as the
/// opaque correlator that ties a pending request to its eventual
/// verdict.
/// </para>
/// <para>
/// Use <see cref="Create"/> to mint a new gate inside a workflow;
/// pass the resulting instance to the workflow builder and to the
/// dispatcher so both sides agree on the port id.
/// </para>
/// </remarks>
/// <param name="Id">
/// Stable identifier surfaced on every <see cref="ApprovalRequest"/>
/// emitted from this gate.
/// </param>
/// <param name="Port">
/// The MAF request port that backs this gate.
/// </param>
public sealed record ApprovalGate(
    string Id,
    RequestPort<ApprovalRequest, ApprovalResponse> Port)
{
    /// <summary>
    /// Creates a new gate with a fresh request port keyed on
    /// <paramref name="id"/>.
    /// </summary>
    public static ApprovalGate Create(string id)
    {
        System.ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return new ApprovalGate(id, RequestPort.Create<ApprovalRequest, ApprovalResponse>(id));
    }
}
