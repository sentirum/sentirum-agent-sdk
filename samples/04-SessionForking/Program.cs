// Sentirum Agent SDK — Session Forking sample.
//
// A single customer support conversation is forked into three parallel
// resolution branches (refund / replacement / discount). Each branch runs in
// isolation, then we visualize the tree and merge the winning branch back.
//
// Run with:
//   ZAI_API_KEY=...    dotnet run --project samples/04-SessionForking
//   OPENAI_API_KEY=... dotnet run --project samples/04-SessionForking

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sentirum.Agent;
using Sentirum.Agent.Sessions.Tree;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSentirumTreeSessions();   // replaces the in-memory store

var zaiKey = Environment.GetEnvironmentVariable("ZAI_API_KEY");
var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

if (!string.IsNullOrWhiteSpace(zaiKey))
{
    builder.Services.AddSentirumAgent("support", b => b
        .UseZAI("glm-4.6", apiKey: zaiKey)
        .WithInstructions("Sen Sentirum müşteri destek asistanısın. Çok kısa cevap ver."));
}
else if (!string.IsNullOrWhiteSpace(openAiKey))
{
    builder.Services.AddSentirumAgent("support", b => b
        .UseOpenAI("gpt-4o-mini", apiKey: openAiKey)
        .WithInstructions("Sen Sentirum müşteri destek asistanısın. Çok kısa cevap ver."));
}
else
{
    Console.WriteLine("Set ZAI_API_KEY or OPENAI_API_KEY to run this sample.");
    return;
}

using var host = builder.Build();
var agent = host.Services.GetRequiredService<ISentirumAgentRegistry>().Find("support")!;
var store = host.Services.GetRequiredService<ITreeSessionStore>();

// Root conversation: customer complaint.
var root = await store.CreateAsync(agent.Id);
await RunAsync(root, "ORD-42 numaralı siparişim kırık geldi, çok üzgünüm. Bir çözüm bulalım.");

// Fork three resolution paths from the same point.
var refund = await store.ForkAsync(root);
var replacement = await store.ForkAsync(root);
var discount = await store.ForkAsync(root);

await RunAsync(refund,      "Tam para iadesi yapalım.");
await RunAsync(replacement, "Aynı ürünü ücretsiz tekrar gönder.");
await RunAsync(discount,    "Bir sonraki siparişe %20 indirim sağla.");

// Compare branches by message count (provider-independent signal).
var refundVsReplacement = await store.CompareAsync(refund, replacement);
var refundVsDiscount    = await store.CompareAsync(refund, discount);

Console.WriteLine();
Console.WriteLine("=== Branch comparison ===");
Console.WriteLine(refundVsReplacement);
Console.WriteLine(refundVsDiscount);

// Visualize the tree.
var tree = await store.GetTreeAsync(root.Id);
Console.WriteLine();
Console.WriteLine("=== Session tree ===");
Console.WriteLine(tree.ToAsciiTree());

// Pretend the team picks "discount" as the winning branch and merges it
// back onto the original timeline. Note the direction: source = branch,
// target = ancestor (root). The opposite direction is now a hard error.
await store.MergeAsync(source: discount, target: root);

// Re-fetch the tree so the printed counts reflect the merge.
tree = await store.GetTreeAsync(root.Id);
Console.WriteLine($"Merged 'discount' branch into root. Root now has {tree.Root.MessageCount} messages.");

async Task RunAsync(ISentirumSession session, string text)
{
    Console.WriteLine();
    Console.WriteLine($"[{session.Id[..8]}] > {text}");
    Console.Write($"[{session.Id[..8]}] < ");
    await foreach (var update in agent.RunStreamingAsync(session, new ChatMessage(ChatRole.User, text)))
    {
        Console.Write(update.Text);
    }
    Console.WriteLine();
}
