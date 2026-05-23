// Sentirum Agent SDK — Human-in-the-loop sample.
//
// Demonstrates an approval-gated refund pipeline:
//
//   intake ▶ recommender ▶ [approval gate] ▶ outcome
//
// Intake parses a typed RefundRequest. The recommender stage drafts a
// textual recommendation (kept deterministic here so the focus stays on
// HITL semantics rather than LLM output). An ApprovalGate suspends the
// run on a RequestPort; a reviewer running on a separate task approves
// (amount ≤ 100) or rejects (amount > 100). The outcome stage renders
// the final customer-facing verdict.
//
// The exact same gate API composes with MAF agent executors too — see
// the inline note on the `recommender` step for the contract.
//
// Run with:
//   dotnet run --project samples/08-HITL

using System.Collections.Generic;
using System.Globalization;
using Microsoft.Agents.AI.Workflows;
using Sentirum.Agent.Workflows;
using Sentirum.Agent.Workflows.HumanInTheLoop;

// ---------------------------------------------------------------------
// Build the workflow.
// ---------------------------------------------------------------------

var gate = ApprovalGate.Create("refund-approval");

// Intake: pass the typed request through unchanged so downstream stages
// (and the request composer) can read both the amount and the reason.
var intake = ((Func<RefundRequest, IWorkflowContext, ValueTask<RefundRequest>>)
    ((req, _) => new ValueTask<RefundRequest>(req)))
    .BindAsExecutor<RefundRequest, RefundRequest>(
        id: "intake", options: null, threadsafe: true);

// Recommender: produce a textual recommendation. Swap this for an
// `agent.InnerAgent.BindAsExecutor(emitEvents: true)` step to drive
// the recommendation from a Sentirum agent — the contract downstream
// stays the same as long as the next stage can read the resulting
// `List<ChatMessage>`. When you do that, call
// `ApprovalDispatcher.RunAsync(..., sendAgentTurnToken: true)` to wake
// the cached-message pipeline once the typed input has been queued.
var recommender = ((Func<RefundRequest, IWorkflowContext, ValueTask<RefundRecommendation>>)
    ((req, _) =>
    {
        var summary =
            $"İade tutarı ${req.Amount}. Sebep: {req.Reason}. " +
            "Ürünü iade kargosuyla geri alıp tutarı orijinal ödeme yöntemine iade etmeyi öneririm.";
        return new ValueTask<RefundRecommendation>(new RefundRecommendation(summary, req));
    }))
    .BindAsExecutor<RefundRequest, RefundRecommendation>(
        id: "recommender", options: null, threadsafe: true);

var workflowBuilder = new WorkflowBuilder(intake);
workflowBuilder.AddEdge(intake, recommender);

var (_, downstream) = workflowBuilder.WithApprovalGate<RefundRecommendation, string>(
    recommender,
    gate,
    requestComposer: rec => new ApprovalRequest(
        gate.Id,
        Title: "İade onayı",
        Summary: rec.Summary,
        Context: new Dictionary<string, string>
        {
            ["customer"] = rec.Original.CustomerId,
            ["amount"] = rec.Original.Amount.ToString(CultureInfo.InvariantCulture),
            ["reason"] = rec.Original.Reason,
        },
        CorrelationId: string.Empty,
        RequestId: string.Empty),
    onApproved: outcome =>
        $"✅ İade ONAYLANDI ({outcome.Reviewer ?? "ops"}) — " +
        $"{outcome.Request.Context["customer"]} / ${outcome.Request.Context["amount"]}. " +
        $"Not: {outcome.Comment ?? "-"}",
    onRejected: outcome =>
        $"❌ İade REDDEDİLDİ ({outcome.Reviewer ?? "ops"}) — " +
        $"{outcome.Request.Context["customer"]} / ${outcome.Request.Context["amount"]}. " +
        $"Sebep: {outcome.Comment ?? "(belirtilmedi)"}");

// Sink: forward the verdict string as a workflow output.
var sink = ((Func<string, IWorkflowContext, ValueTask<string>>)
    ((s, _) => new ValueTask<string>(s)))
    .BindAsExecutor<string, string>(id: "sink", options: null, threadsafe: true);

workflowBuilder.AddEdge(downstream, sink);
workflowBuilder.WithOutputFrom(sink);

var mafWorkflow = workflowBuilder.Build(validateOrphans: true);
var workflow = new SentirumWorkflow("refund-flow", "refund-flow", mafWorkflow);

// ---------------------------------------------------------------------
// Set up the reviewer side. The policy here is intentionally trivial:
// any refund <= $100 is auto-approved, anything larger is rejected.
// In production this lives behind Slack / Teams / a custom approval UI.
// ---------------------------------------------------------------------

await using var channel = new InMemoryApprovalChannel();
var reviewerCts = new CancellationTokenSource();
var reviewer = Task.Run(async () =>
{
    await foreach (var request in channel.WatchRequestsAsync(reviewerCts.Token))
    {
        Console.WriteLine();
        Console.WriteLine($"-- Reviewer aldı: {request.Title}");
        Console.WriteLine($"   ÖNERİ: {request.Summary}");

        var amount = decimal.Parse(
            request.Context["amount"],
            CultureInfo.InvariantCulture);

        if (amount > 100m)
        {
            await channel.RejectAsync(
                request.GateId,
                reviewer: "policy-bot",
                comment: $"Tutar (${amount}) limit üstünde");
        }
        else
        {
            await channel.ApproveAsync(
                request.GateId,
                reviewer: "policy-bot",
                comment: "Politika dahilinde, otomatik onay");
        }
    }
});

// ---------------------------------------------------------------------
// Drive two scenarios — one will be approved, one rejected.
// ---------------------------------------------------------------------

await RunRefundAsync(new RefundRequest("u-ersin", 45m, "Hasarlı kahve makinesi"));
await RunRefundAsync(new RefundRequest("u-mehmet", 850m, "Yanlış telefon modeli gönderildi"));

reviewerCts.Cancel();
try { await reviewer; } catch (OperationCanceledException) { }

return 0;

async Task RunRefundAsync(RefundRequest req)
{
    Console.WriteLine();
    Console.WriteLine($"=== İade isteği: {req.CustomerId} / ${req.Amount} ===");
    var result = await ApprovalDispatcher.RunAsync(workflow, req, channel);
    foreach (var output in result.Outputs)
    {
        Console.WriteLine(output);
    }
}

internal sealed record RefundRequest(string CustomerId, decimal Amount, string Reason);
internal sealed record RefundRecommendation(string Summary, RefundRequest Original);
