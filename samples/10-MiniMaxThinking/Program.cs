using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.AI;
using Sentirum.Agent;
using Sentirum.Agent.Providers.MiniMax;

var key = Environment.GetEnvironmentVariable("MINIMAX_API_KEY");
if (string.IsNullOrWhiteSpace(key))
{
    Console.Error.WriteLine("Set MINIMAX_API_KEY");
    return 1;
}

var services = new ServiceCollection();

services.AddSentirumAgent("mm", b => b
    .UseMiniMax("MiniMax-M2.7", key, MiniMaxProtocol.OpenAI, separateThinking: true)
    .WithInstructions("You are a helpful assistant. Always think step by step."));

var sp = services.BuildServiceProvider();
var agent = sp.GetRequiredService<ISentirumAgentRegistry>().Find("mm")!;
var sessionStore = sp.GetRequiredService<ISentirumSessionStore>();
var session = await sessionStore.CreateAsync(agent.Id);

Console.WriteLine("=== Non-Streaming Test ===\n");

var response = await agent.RunAsync(
    session,
    new ChatMessage(ChatRole.User, "What is 15 * 37?"),
    CancellationToken.None);

foreach (var msg in response.Messages)
{
    if (msg.Role != ChatRole.Assistant)
    {
        continue;
    }

    var thinking = msg.AdditionalProperties?
        .GetValueOrDefault(MiniMaxThinkingMiddleware.ThinkingPropertyName) as string;
    var answer = msg.Text;

    if (thinking is not null)
    {
        Console.WriteLine($"🧠 THINKING:\n{thinking}\n");
    }

    Console.WriteLine($"💬 ANSWER:\n{answer}\n");
}

Console.WriteLine("=== Streaming Test ===\n");

var session2 = await sessionStore.CreateAsync(agent.Id);

await foreach (var update in agent.RunStreamingAsync(
    session2,
    new ChatMessage(ChatRole.User, "Is 97 a prime number?"),
    CancellationToken.None))
{
    if (update.Text is null)
    {
        continue;
    }

    var isThinking = update.AdditionalProperties?
        .ContainsKey(MiniMaxThinkingMiddleware.IsThinkingPropertyName) == true;

    var hasThinking = update.AdditionalProperties?
        .ContainsKey(MiniMaxThinkingMiddleware.ThinkingPropertyName) == true;

    if (hasThinking)
    {
        var t = update.AdditionalProperties?[MiniMaxThinkingMiddleware.ThinkingPropertyName] as string;
        Console.WriteLine($"\n🧠 THINKING (complete):\n{t}\n");
        Console.Write("💬 ANSWER: ");
    }
    else if (isThinking)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write(update.Text);
        Console.ResetColor();
    }
    else
    {
        Console.Write(update.Text);
    }
}

Console.WriteLine("\n\n✅ Done!");
return 0;
