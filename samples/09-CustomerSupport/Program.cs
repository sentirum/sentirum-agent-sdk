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

using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using Sentirum.Agent;
using Sentirum.Agent.CustomerSupport;
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

    return SupportWorkflowBuilder.CreateTriageWorkflow("support-triage",
    [
        A("refund-specialist"),
        A("replacement-specialist"),
        A("discount-specialist"),
    ]);
});

// ── HITL channel ─────────────────────────────────────────────────────
builder.Services.AddSingleton<IApprovalChannel>(new InMemoryApprovalChannel());

// ── Customer support services (ticket store) ─────────────────────────
builder.Services.AddCustomerSupport();

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
    ISupportTicketStore tickets,
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

    var recommendations = SupportWorkflowBuilder.ExtractRecommendations(wfResult.Outputs);

    // ── Step 3: determine if HITL needed ─────────────────────────────
    var amounts = SupportWorkflowBuilder.ExtractAmounts(recommendations);
    var maxAmount = amounts.DefaultIfEmpty(0m).Max();
    var needsApproval = maxAmount > 100m;

    var ticket = new SupportTicket
    {
        Id = ticketId,
        CustomerId = req.CustomerId,
        Subject = req.Subject,
        Description = req.Description,
        Category = category,
        Recommendations = recommendations,
        MaxAmount = maxAmount,
        Status = needsApproval ? SupportTicketStatus.PendingApproval : SupportTicketStatus.Resolved,
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
        var approvalRequest = SupportApprovalGate.ComposeRequest(threshold: 100m)(wfResult.Outputs);
        await channel.PublishAsync(approvalRequest, ct);
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
app.MapGet("/support/ticket/{id}", (string id, ISupportTicketStore tickets) =>
{
    if (!tickets.TryGet(id, out var ticket))
    {
        return Results.NotFound();
    }

    return Results.Ok(new TicketResponse(
        ticket!.Id,
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
    ISupportTicketStore tickets,
    IApprovalChannel channel,
    CancellationToken ct) =>
{
    if (!tickets.TryGet(id, out var ticket))
    {
        return Results.NotFound();
    }

    if (ticket!.Status != SupportTicketStatus.PendingApproval)
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

            ticket.Status = SupportTicketStatus.Approved;
        }
        else
        {
            await inMem.RejectAsync(
                "support-approval",
                req.Reviewer,
                req.Comment,
                requestId: string.Empty,
                context: new Dictionary<string, string> { ["ticketId"] = id });

            ticket.Status = SupportTicketStatus.Rejected;
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
app.MapGet("/support/tickets", (ISupportTicketStore tickets) =>
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
