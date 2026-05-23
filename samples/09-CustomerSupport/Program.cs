// Sentirum Agent SDK — Customer Support Vertical (M8)
//
// End-to-end demo showing every SDK primitive working together:
//
//   1. ASP.NET Core Minimal API endpoint
//   2. Agent classification (Z.AI / GLM-4.6)
//   3. Parallel workflow — refund / replacement / discount specialists
//   4. HITL approval gate for high-value tickets ($100+)
//   5. InMemory memory store for ticket audit trail
//   6. Typed request/response DTOs
//
// Endpoints:
//   POST /support/ticket        → create ticket, run workflow
//   GET  /support/ticket/{id}   → query ticket status & result
//   POST /support/approve/{id}  → approve/reject a pending HITL gate
//
// Run with:
//   ZAI_API_KEY=... dotnet run --project samples/09-CustomerSupport

using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json.Serialization;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Sentirum.Agent;
using Sentirum.Agent.Memory;
using Sentirum.Agent.Memory.InMemory;
using Sentirum.Agent.Workflows;
using Sentirum.Agent.Workflows.HumanInTheLoop;

var zaiKey = Environment.GetEnvironmentVariable("ZAI_API_KEY");
if (string.IsNullOrWhiteSpace(zaiKey))
{
    Console.Error.WriteLine("Set ZAI_API_KEY to run this sample.");
    Environment.Exit(1);
}

var builder = WebApplication.CreateBuilder(args);

// ── Register memory store (single instance, thread-safe) ─────────────
builder.Services.AddSingleton<ISentirumMemoryStore>(new InMemoryMemoryStore
{
    MaxTotalEntries = 10_000,
});

// ── Register agents ──────────────────────────────────────────────────
builder.Services.AddSentirumAgent("classifier", b => b
    .UseZAI("glm-4.6", apiKey: zaiKey)
    .WithInstructions("""
        You are a support ticket classifier. Read the customer complaint
        and label it with exactly one category:
        [damaged-product, late-delivery, missing-item, billing, other].
        Respond with ONLY the category label, no explanation.
        """));

builder.Services.AddSentirumAgent("refund-specialist", b => b
    .UseZAI("glm-4.6", apiKey: zaiKey)
    .WithInstructions("""
        You are a refund specialist. Given a customer complaint, write a
        short refund recommendation (≤2 sentences). Format:
        "REFUND: <amount/conditions>".
        """));

builder.Services.AddSentirumAgent("replacement-specialist", b => b
    .UseZAI("glm-4.6", apiKey: zaiKey)
    .WithInstructions("""
        You are a replacement specialist. Given a complaint, write a short
        replacement recommendation (≤2 sentences). Format:
        "REPLACEMENT: <item/shipping-time>".
        """));

builder.Services.AddSentirumAgent("discount-specialist", b => b
    .UseZAI("glm-4.6", apiKey: zaiKey)
    .WithInstructions("""
        You are a compensation specialist. Given a complaint, suggest a
        discount or credit (≤2 sentences). Format:
        "COMPENSATION: <percentage/credit>".
        """));

// ── Build the triage workflow ────────────────────────────────────────
builder.Services.AddSingleton<ISentirumWorkflow>(sp =>
{
    var registry = sp.GetRequiredService<ISentirumAgentRegistry>();
    ISentirumAgent A(string id) => registry.Find(id)
        ?? throw new InvalidOperationException($"Agent '{id}' not found.");

    return SentirumWorkflowBuilder.Create("support-triage")
        .WithName("Customer Support Triage")
        .ConcurrentJoin(new[]
        {
            A("refund-specialist"),
            A("replacement-specialist"),
            A("discount-specialist"),
        })
        .Build();
});

// ── HITL channel ─────────────────────────────────────────────────────
builder.Services.AddSingleton<IApprovalChannel>(new InMemoryApprovalChannel());

// ── Ticket store (in-memory) ─────────────────────────────────────────
builder.Services.AddSingleton(new TicketStore());

var app = builder.Build();

// ═════════════════════════════════════════════════════════════════════
// Endpoints
// ═════════════════════════════════════════════════════════════════════

// POST /support/ticket
// Body: { "customerId": "u-123", "subject": "...", "description": "..." }
app.MapPost("/support/ticket", async (
    CreateTicketRequest req,
    ISentirumAgentRegistry agents,
    ISentirumWorkflow workflow,
    ISentirumMemoryStore memory,
    TicketStore tickets,
    IApprovalChannel channel,
    CancellationToken ct) =>
{
    var ticketId = Guid.NewGuid().ToString("n")[..12];
    var createdAt = DateTimeOffset.UtcNow;

    // ── Step 1: classify ─────────────────────────────────────────────
    var classifier = agents.Find("classifier")!;
    var classifyResult = await classifier.RunAsync(
        new SentirumSession(ticketId, classifier.Id, null!, null),
        new ChatMessage(ChatRole.User, req.Description),
        ct);

    var category = classifyResult.Messages.LastOrDefault()?.Text?.Trim().ToLowerInvariant()
        ?? "other";

    // ── Step 2: run parallel specialists ─────────────────────────────
    var wfResult = await workflow.RunAsync(
        new List<ChatMessage> { new(ChatRole.User, req.Description) },
        sessionId: ticketId,
        cancellationToken: ct);

    var recommendations = ExtractRecommendations(wfResult.Outputs);

    // ── Step 3: determine if HITL needed ─────────────────────────────
    var amounts = ExtractAmounts(recommendations);
    var maxAmount = amounts.DefaultIfEmpty(0m).Max();
    var needsApproval = maxAmount > 100m;

    var ticket = new Ticket
    {
        Id = ticketId,
        CustomerId = req.CustomerId,
        Subject = req.Subject,
        Description = req.Description,
        Category = category,
        Recommendations = recommendations,
        MaxAmount = maxAmount,
        Status = needsApproval ? TicketStatus.PendingApproval : TicketStatus.Resolved,
        CreatedAt = createdAt,
    };

    tickets.Upsert(ticket);

    // ── Step 4: persist audit trail ──────────────────────────────────
    var partition = MemoryPartition.ForSession(ticketId);
    await memory.SetAsync(partition, "category", category, cancellationToken: ct);
    await memory.SetAsync(partition, "recommendations", string.Join(" | ", recommendations), cancellationToken: ct);
    await memory.SetAsync(partition, "needs-approval", needsApproval.ToString(), cancellationToken: ct);

    // ── Step 5: if HITL needed, publish to channel ───────────────────
    if (needsApproval)
    {
        await channel.PublishAsync(new ApprovalRequest(
            "support-approval",
            $"Ticket {ticketId} requires approval",
            $"Max recommendation: ${maxAmount}\n{string.Join("\n", recommendations)}",
            new Dictionary<string, string>
            {
                ["ticketId"] = ticketId,
                ["maxAmount"] = maxAmount.ToString(CultureInfo.InvariantCulture),
                ["category"] = category,
            },
            CorrelationId: Guid.NewGuid().ToString("N"),
            RequestId: string.Empty),
            ct);
    }

    return Results.Created($"/support/ticket/{ticketId}", new TicketResponse(
        ticket.Id,
        ticket.Status.ToString(),
        ticket.Category,
        ticket.Recommendations,
        ticket.MaxAmount,
        ticket.CreatedAt));
});

// GET /support/ticket/{id}
app.MapGet("/support/ticket/{id}", (string id, TicketStore tickets) =>
{
    if (!tickets.TryGet(id, out var ticket))
    {
        return Results.NotFound();
    }

    return Results.Ok(new TicketResponse(
        ticket.Id,
        ticket.Status.ToString(),
        ticket.Category,
        ticket.Recommendations,
        ticket.MaxAmount,
        ticket.CreatedAt));
});

// POST /support/approve/{id}
// Body: { "approved": true, "reviewer": "ops@sentirum", "comment": "lgtm" }
app.MapPost("/support/approve/{id}", async (
    string id,
    ApproveRequest req,
    TicketStore tickets,
    IApprovalChannel channel,
    CancellationToken ct) =>
{
    if (!tickets.TryGet(id, out var ticket))
    {
        return Results.NotFound();
    }

    if (ticket.Status != TicketStatus.PendingApproval)
    {
        return Results.BadRequest(new { error = "Ticket is not pending approval." });
    }

    // Cast to InMemoryApprovalChannel so we can use the reviewer-side
    // ApproveAsync / RejectAsync helpers. Production code would use a
    // typed channel implementation (Slack, Teams, etc.) with its own
    // transport; the cast here is only for the single-process demo.
    if (channel is InMemoryApprovalChannel inMem)
    {
        if (req.Approved)
        {
            await inMem.ApproveAsync(
                "support-approval",
                req.Reviewer,
                req.Comment,
                requestId: string.Empty,
                context: new Dictionary<string, string> { ["ticketId"] = id });

            ticket.Status = TicketStatus.Approved;
        }
        else
        {
            await inMem.RejectAsync(
                "support-approval",
                req.Reviewer,
                req.Comment,
                requestId: string.Empty,
                context: new Dictionary<string, string> { ["ticketId"] = id });

            ticket.Status = TicketStatus.Rejected;
        }
    }

    tickets.Upsert(ticket);

    return Results.Ok(new TicketResponse(
        ticket.Id,
        ticket.Status.ToString(),
        ticket.Category,
        ticket.Recommendations,
        ticket.MaxAmount,
        ticket.CreatedAt));
});

// GET /support/tickets (list all)
app.MapGet("/support/tickets", (TicketStore tickets) =>
{
    var all = tickets.GetAll()
        .Select(t => new TicketResponse(
            t.Id,
            t.Status.ToString(),
            t.Category,
            t.Recommendations,
            t.MaxAmount,
            t.CreatedAt))
        .ToList();

    return Results.Ok(all);
});

app.Run();

// ═════════════════════════════════════════════════════════════════════
// Helpers
// ═════════════════════════════════════════════════════════════════════

static List<string> ExtractRecommendations(IReadOnlyList<object?> outputs)
{
    var result = new List<string>();
    foreach (var output in outputs)
    {
        if (output is IEnumerable<ChatMessage> msgs)
        {
            result.AddRange(msgs
                .Select(m => m.Text)
                .Where(t => !string.IsNullOrWhiteSpace(t)));
        }
        else if (output is ChatMessage msg && !string.IsNullOrWhiteSpace(msg.Text))
        {
            result.Add(msg.Text);
        }
    }
    return result;
}

static IEnumerable<decimal> ExtractAmounts(List<string> recommendations)
{
    foreach (var rec in recommendations)
    {
        foreach (var token in rec.Split(new[] { ' ', '\t', '\n', '$', ',', '%' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (decimal.TryParse(token, NumberStyles.Number, CultureInfo.InvariantCulture, out var v))
            {
                yield return v;
            }
        }
    }
}

// ═════════════════════════════════════════════════════════════════════
// DTOs
// ═════════════════════════════════════════════════════════════════════

public sealed record CreateTicketRequest(
    [property: JsonPropertyName("customerId")] string CustomerId,
    [property: JsonPropertyName("subject")] string Subject,
    [property: JsonPropertyName("description")] string Description);

public sealed record ApproveRequest(
    [property: JsonPropertyName("approved")] bool Approved,
    [property: JsonPropertyName("reviewer")] string Reviewer,
    [property: JsonPropertyName("comment")] string? Comment);

public sealed record TicketResponse(
    string Id,
    string Status,
    string Category,
    List<string> Recommendations,
    decimal MaxAmount,
    DateTimeOffset CreatedAt);

// ═════════════════════════════════════════════════════════════════════
// Domain
// ═════════════════════════════════════════════════════════════════════

public enum TicketStatus
{
    PendingApproval,
    Resolved,
    Approved,
    Rejected,
}

public sealed class Ticket
{
    public required string Id { get; init; }
    public required string CustomerId { get; init; }
    public required string Subject { get; init; }
    public required string Description { get; init; }
    public required string Category { get; set; }
    public required List<string> Recommendations { get; init; }
    public required decimal MaxAmount { get; init; }
    public required TicketStatus Status { get; set; }
    public required DateTimeOffset CreatedAt { get; init; }
}

public sealed class TicketStore
{
    private readonly ConcurrentDictionary<string, Ticket> _tickets = new(StringComparer.Ordinal);

    public void Upsert(Ticket ticket) => _tickets[ticket.Id] = ticket;
    public bool TryGet(string id, out Ticket ticket) => _tickets.TryGetValue(id, out ticket!);
    public IEnumerable<Ticket> GetAll() => _tickets.Values;
}

// (end of file)
