// Sentirum Agent SDK — Hello Agent sample.
//
// Run with:
//   export OPENAI_API_KEY=sk-...
//   dotnet run --project samples/01-HelloAgent
//
// If OPENAI_API_KEY is not set, the sample falls back to a smoke-test mode
// that only verifies registration and DI resolution.

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sentirum.Agent;

const string AgentName = "support";

var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
var hasApiKey = !string.IsNullOrWhiteSpace(apiKey);

var builder = Host.CreateApplicationBuilder(args);

if (hasApiKey)
{
    builder.Services.AddSentirumAgent(AgentName, b => b
        .UseOpenAI(
            model: "gpt-4o-mini",
            apiKey: apiKey)
        .WithDescription("Sentirum customer support agent — M1 sample.")
        .WithInstructions("""
            Sen Sentirum'un Türkçe konuşan müşteri destek asistanısın.
            Kısa, kibar ve net cevap ver.
            """));
}
else
{
    builder.Services.AddSentirumCore();
}

using var host = builder.Build();

var registry = host.Services.GetRequiredService<ISentirumAgentRegistry>();
var sessionStore = host.Services.GetRequiredService<ISentirumSessionStore>();

Console.WriteLine($"Sentirum Agent SDK — Hello Agent");
Console.WriteLine($"Registered agents: {string.Join(", ", registry.Agents.Select(a => a.Id))}");

if (!hasApiKey)
{
    Console.WriteLine();
    Console.WriteLine("OPENAI_API_KEY is not set — skipping live call.");
    Console.WriteLine("Set it and rerun to chat with the agent.");
    return;
}

var agent = registry.Find(AgentName)!;
var session = await sessionStore.CreateAsync(agent.Id);

Console.WriteLine();
Console.Write("> Merhaba, sipariş kargo durumumu nasıl öğrenebilirim?\n\n");

await foreach (var update in agent.RunStreamingAsync(
    session,
    new ChatMessage(ChatRole.User, "Merhaba, sipariş kargo durumumu nasıl öğrenebilirim?")))
{
    Console.Write(update.Text);
}

Console.WriteLine();
