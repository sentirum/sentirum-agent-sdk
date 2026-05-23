// Sentirum Agent SDK — Multi-Provider sample.
//
// Demonstrates running the same agent persona against six different providers:
//   * OpenAI                     (OPENAI_API_KEY)
//   * Anthropic                  (ANTHROPIC_API_KEY)
//   * Z.AI via OpenAI protocol   (ZAI_API_KEY)
//   * Z.AI via Anthropic protocol(ZAI_API_KEY)
//   * Ollama (local)             (no key; default http://localhost:11434)
//   * Generic OpenAI-compatible  (CUSTOM_BASE_URL + CUSTOM_API_KEY + CUSTOM_MODEL)
//
// Each provider only runs if its env vars are present. Run with:
//   dotnet run --project samples/02-MultiProvider

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sentirum.Agent;
using Sentirum.Agent.Providers.ZAI;

const string Question = "Selam! Sentirum müşteri destek SDK'sını bir cümlede tanıt.";
const string Instructions = """
                            Sen Sentirum'un Türkçe konuşan müşteri destek asistanısın.
                            Çok kısa, kibar ve net cevap ver.
                            """;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSentirumCore();

var registeredProviders = new List<string>();

void Register(string name, Action<ISentirumAgentBuilder> configure, bool when = true)
{
    if (!when)
    {
        return;
    }

    builder.Services.AddSentirumAgent(name, b =>
    {
        b.WithInstructions(Instructions);
        configure(b);
    });
    registeredProviders.Add(name);
}

Register("openai",
    b => b
.UseOpenAI("gpt-4o-mini", apiKey: Environment.GetEnvironmentVariable("OPENAI_API_KEY")),
    when: !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY")));

Register("anthropic",
    b => b.UseAnthropic("claude-haiku-4-5", apiKey: Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"), maxTokens: 512),
    when: !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")));

Register("zai-openai",
    b => b.UseZAI("glm-4.6", apiKey: Environment.GetEnvironmentVariable("ZAI_API_KEY"), protocol: ZaiProtocol.OpenAI)
        .EnableZaiThinking(),
    when: !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ZAI_API_KEY")));

Register("zai-anthropic",
    b => b.UseZAI("glm-4.7", apiKey: Environment.GetEnvironmentVariable("ZAI_API_KEY"), protocol: ZaiProtocol.Anthropic, maxTokens: 512),
    when: !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ZAI_API_KEY")));

Register("ollama",
    b => b.UseOllama("llama3.2"),
    when: string.Equals(Environment.GetEnvironmentVariable("ENABLE_OLLAMA"), "true", StringComparison.OrdinalIgnoreCase));

Register("custom",
    b => b.UseOpenAICompatible(
        endpoint: new Uri(Environment.GetEnvironmentVariable("CUSTOM_BASE_URL")!),
        model: Environment.GetEnvironmentVariable("CUSTOM_MODEL")!,
        apiKey: Environment.GetEnvironmentVariable("CUSTOM_API_KEY")!),
    when: !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CUSTOM_BASE_URL")));

using var host = builder.Build();
var registry = host.Services.GetRequiredService<ISentirumAgentRegistry>();
var sessionStore = host.Services.GetRequiredService<ISentirumSessionStore>();

Console.WriteLine("Sentirum Agent SDK — Multi-Provider sample");
Console.WriteLine($"Registered providers: {(registeredProviders.Count == 0 ? "(none)" : string.Join(", ", registeredProviders))}");

if (registeredProviders.Count == 0)
{
    Console.WriteLine();
    Console.WriteLine("No provider env vars set. Set at least one of:");
    Console.WriteLine("  OPENAI_API_KEY, ANTHROPIC_API_KEY, ZAI_API_KEY,");
    Console.WriteLine("  ENABLE_OLLAMA=true, CUSTOM_BASE_URL+CUSTOM_MODEL+CUSTOM_API_KEY");
    return;
}

foreach (var providerName in registeredProviders)
{
    var agent = registry.Find(providerName)!;
    var session = await sessionStore.CreateAsync(agent.Id);

    Console.WriteLine();
    Console.WriteLine($"=== {providerName} ===");
    Console.Write("> ");

    try
    {
        await foreach (var update in agent.RunStreamingAsync(
                           session,
                           new ChatMessage(ChatRole.User, Question)))
        {
            Console.Write(update.Text);
        }

        Console.WriteLine();
    }
    catch (Exception ex)
    {
        Console.WriteLine();
        Console.WriteLine($"[{providerName}] failed: {ex.GetType().Name}: {ex.Message}");
    }
}
