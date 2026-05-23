namespace Sentirum.Agent.Workflows.HumanInTheLoop;

/// <summary>
/// Convenience projection of the reviewer's verdict, plus the
/// request that produced it, for downstream executors.
/// </summary>
/// <param name="Request">The original approval request payload.</param>
/// <param name="Approved">Whether the reviewer approved the request.</param>
/// <param name="Reviewer">Reviewer identifier, if supplied.</param>
/// <param name="Comment">Reviewer comment, if supplied.</param>
public sealed record ApprovalOutcome(
    ApprovalRequest Request,
    bool Approved,
    string? Reviewer,
    string? Comment);
