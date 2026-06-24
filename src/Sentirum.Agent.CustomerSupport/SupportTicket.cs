using System;
using System.Collections.Generic;
using Sentirum.Agent.CustomerSupport.Sentiment;

namespace Sentirum.Agent.CustomerSupport;

/// <summary>
/// A customer support ticket and its current state.
/// </summary>
public sealed class SupportTicket
{
    /// <summary>
    /// Unique ticket identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Customer identifier.
    /// </summary>
    public required string CustomerId { get; init; }

    /// <summary>
    /// Ticket subject line.
    /// </summary>
    public required string Subject { get; init; }

    /// <summary>
    /// Full customer description of the issue.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Classified category (e.g. "damaged-product", "late-delivery", etc.).
    /// </summary>
    public string Category { get; set; } = "other";

    /// <summary>
    /// Recommendations produced by specialist agents.
    /// </summary>
    public List<string> Recommendations { get; init; } = [];

    /// <summary>
    /// The maximum monetary amount mentioned across all recommendations.
    /// </summary>
    public decimal MaxAmount { get; set; }

    /// <summary>
    /// The customer-facing reply drafted by the Tier-1 responder. Populated
    /// once the triage + responder pipeline has run.
    /// </summary>
    public string? Reply { get; set; }

    /// <summary>
    /// The latest sentiment score recorded for the customer's message
    /// (null if not analyzed). Typed so callers can read polarity, label,
    /// and confidence without re-parsing a string.
    /// </summary>
    public SentimentScore? Sentiment { get; set; }

    /// <summary>
    /// Current ticket lifecycle state.
    /// </summary>
    public SupportTicketStatus Status { get; set; } = SupportTicketStatus.New;

    /// <summary>
    /// When the ticket was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Lifecycle states for a <see cref="SupportTicket"/>.
/// </summary>
public enum SupportTicketStatus
{
    New,
    Classified,
    PendingApproval,
    Approved,
    Rejected,
    Resolved,
}
