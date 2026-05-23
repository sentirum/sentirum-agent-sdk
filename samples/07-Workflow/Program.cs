// Sentirum Agent SDK — Workflow sample.
//
// Demonstrates two of the three M5 workflow shapes against the same
// customer-support triage scenario:
//
//   1. Concurrent fan-out — three "specialist" agents (refund, replacement,
//      discount) all look at the same complaint in parallel and produce
//      independent recommendations. The aggregator joins them so the
//      operator can compare options side by side.
//
//   2. Sequential pipeline — a classifier agent labels the complaint,
//      then a responder agent drafts the customer-facing reply using
//      the classifier's output as input. Mirrors the typical
//      "tag → answer" pipeline shipping in production support flows.
//
// Both shapes run live against Z.AI / GLM-4.6.
//
// Run with:
//   ZAI_API_KEY=... dotnet run --project samples/07-Workflow

using System.Linq;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sentirum.Agent;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Sentirum.Agent.Workflows;

var zaiKey = Environment.GetEnvironmentVariable("ZAI_API_KEY");
if (string.IsNullOrWhiteSpace(zaiKey))
{
    Console.Error.WriteLine("Set ZAI_API_KEY to run this sample.");
    return 1;
}

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSentirumAgent("refund-specialist", b => b
    .UseZAI("glm-4.6", apiKey: zaiKey)
    .WithInstructions("""
        Sen iade uzmanısın. Müşterinin şikayetine göre kısa bir iade önerisi yaz.
        Format: "ÖNERİ (İADE): <tutar / koşul / süre>". 2 cümleyi geçme.
        """));

builder.Services.AddSentirumAgent("replacement-specialist", b => b
    .UseZAI("glm-4.6", apiKey: zaiKey)
    .WithInstructions("""
        Sen değişim uzmanısın. Şikayete göre değişim önerisi yaz.
        Format: "ÖNERİ (DEĞİŞİM): <yeni ürün / kargo süresi>". 2 cümleyi geçme.
        """));

builder.Services.AddSentirumAgent("discount-specialist", b => b
    .UseZAI("glm-4.6", apiKey: zaiKey)
    .WithInstructions("""
        Sen indirim uzmanısın. Şikayete göre bir telafi indirimi öner.
        Format: "ÖNERİ (İNDİRİM): %<oran> sonraki sipariş için". 2 cümleyi geçme.
        """));

builder.Services.AddSentirumAgent("classifier", b => b
    .UseZAI("glm-4.6", apiKey: zaiKey)
    .WithInstructions("""
        Müşteri şikayetini şu kategorilerden BİRİNE etiketle:
        [hasarli-urun, gec-teslimat, eksik-urun, faturalama, diger].
        Sadece etiketi yaz, açıklama yapma.
        """));

builder.Services.AddSentirumAgent("responder", b => b
    .UseZAI("glm-4.6", apiKey: zaiKey)
    .WithInstructions("""
        Sen Sentirum müşteri destek asistanısın. Girdi olarak [etiket] alacaksın.
        Etiket türüne uygun, 2-3 cümlelik, sıcak ve çözüm odaklı bir Türkçe yanıt yaz.
        Müşteri ismi vermediği için "Değerli müşterimiz" diye hitap et.
        """));

using var host = builder.Build();
var registry = host.Services.GetRequiredService<ISentirumAgentRegistry>();

ISentirumAgent Agent(string id) => registry.Find(id)
    ?? throw new InvalidOperationException($"Agent '{id}' not found.");

// -------------------------------------------------------------------------
// 1. Concurrent triage — three specialists react to the same complaint.
// -------------------------------------------------------------------------

Console.WriteLine("=== Senaryo 1: Paralel triage (3 uzman aynı şikâyete bakar) ===");

var triage = SentirumWorkflowBuilder.Create("triage")
    .WithName("Customer Support Triage")
    .ConcurrentJoin(new[]
    {
        Agent("refund-specialist"),
        Agent("replacement-specialist"),
        Agent("discount-specialist"),
    })
    .Build();

const string Complaint =
    "Geçen hafta sipariş ettiğim kahve makinesi hasarlı geldi. " +
    "Kutusu yırtılmıştı, ısıtıcı plakası eğri. Ne yapabiliriz?";

var triageResult = await triage.RunAsync(new List<ChatMessage>
{
    new(ChatRole.User, Complaint),
});

PrintMessages("Triage çıktısı", triageResult.Outputs);

// -------------------------------------------------------------------------
// 2. Sequential pipeline — classifier ▶ responder.
// -------------------------------------------------------------------------

Console.WriteLine();
Console.WriteLine("=== Senaryo 2: Sıralı boru hattı (classifier → responder) ===");

var pipeline = SentirumWorkflowBuilder.Create("pipeline")
    .WithName("Classify-then-Respond")
    .Sequential(Agent("classifier"), Agent("responder"))
    .Build();

var pipelineResult = await pipeline.RunAsync(new List<ChatMessage>
{
    new(ChatRole.User, Complaint),
});

PrintMessages("Pipeline çıktısı", pipelineResult.Outputs);

return 0;

static void PrintMessages(string heading, IReadOnlyList<object?> outputs)
{
    Console.WriteLine();
    Console.WriteLine($"--- {heading} ---");
    foreach (var output in outputs)
    {
        switch (output)
        {
            case IEnumerable<ChatMessage> msgs:
                foreach (var m in msgs)
                {
                    if (!string.IsNullOrWhiteSpace(m.Text))
                    {
                        Console.WriteLine(m.Text);
                    }
                }
                break;
            case ChatMessage msg:
                if (!string.IsNullOrWhiteSpace(msg.Text))
                {
                    Console.WriteLine(msg.Text);
                }
                break;
            case AgentResponseUpdate update:
                Console.Write(update.Text);
                break;
            case AgentResponse resp:
                Console.WriteLine(resp.Text);
                break;
            default:
                Console.WriteLine($"<{output?.GetType().Name}>");
                break;
        }
    }
}
