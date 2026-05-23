// Sentirum Agent SDK — Memory + Context Providers sample.
//
// Demonstrates how a user profile stored in ISentirumMemoryStore is
// automatically injected into every agent run via WithUserMemory(...).
// Each sample run opens a fresh session so the only thing the agent
// remembers about the user comes from the memory store, not the
// session history.
//
// Run with:
//   ZAI_API_KEY=... dotnet run --project samples/05-Memory

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sentirum.Agent;
using Sentirum.Agent.Memory;

const string UserId = "u-ersin";

var zaiKey = Environment.GetEnvironmentVariable("ZAI_API_KEY");
if (string.IsNullOrWhiteSpace(zaiKey))
{
    Console.Error.WriteLine("Set ZAI_API_KEY to run this sample.");
    return 1;
}

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSentirumInMemoryMemory();

builder.Services.AddSentirumAgent("support", b => b
    .UseZAI("glm-4.6", apiKey: zaiKey)
    .WithInstructions("""
        Sen Sentirum'un Türkçe konuşan müşteri destek asistanısın.
        Kullanıcının bilgilerini sistem mesajından oku ve yanıtlarında bu bilgileri
        doğal şekilde kullan. İsmi varsa hitap ederken kullan.
        """)
    .WithAmbientInstructions(_ => $"Şu anki tarih: {DateTimeOffset.UtcNow:yyyy-MM-dd}")
    .WithUserMemory(userId: UserId, heading: "Kullanıcı hakkında bildiklerimiz:"));

using var host = builder.Build();

// Seed the user's memory partition. In a real app these come from a
// profile service, signup flow, prior conversations, etc.
var memory = host.Services.GetRequiredService<ISentirumMemoryStore>();
var partition = MemoryPartition.ForUser(UserId);
await memory.SetAsync(partition, "name", "Ersin");
await memory.SetAsync(partition, "city", "İstanbul");
await memory.SetAsync(partition, "subscription", "Pro plan, yenileme 2026-09-12");
await memory.SetAsync(partition, "preference", "kısa ve doğrudan yanıt sever");

var registry = host.Services.GetRequiredService<ISentirumAgentRegistry>();
var store = host.Services.GetRequiredService<ISentirumSessionStore>();
var agent = registry.Find("support")!;

// Fresh session — the only thing the agent knows about the user comes
// from the memory partition, injected by the context provider.
var session = await store.CreateAsync(agent.Id);

Console.WriteLine("=== Session 1 (yeni) ===");
await RunTurn(agent, session, "Selam, beni hatırlıyor musun?");

Console.WriteLine();
Console.WriteLine("=== Session 2 (yepyeni) ===");
var session2 = await store.CreateAsync(agent.Id);
await RunTurn(agent, session2, "Aboneliğim ne zaman bitiyor?");

return 0;

static async Task RunTurn(ISentirumAgent agent, ISentirumSession session, string prompt)
{
    Console.WriteLine($"> {prompt}");
    var response = await agent.RunAsync(session, new ChatMessage(ChatRole.User, prompt));
    Console.WriteLine(response.Text);
}
