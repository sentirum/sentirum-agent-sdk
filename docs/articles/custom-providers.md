# Building Custom Providers

Sentirum ships a **4-tier provider strategy** that covers every LLM integration
scenario, from "just works" first-party providers to fully custom HTTP/gRPC
endpoints.

## Tier 1 — First-party providers

Purpose-built packages that wrap the official SDK and wire Sentirum conventions
(retries, function invocation, structured logging) in a single call.

| Package | Extension method |
| --- | --- |
| `Sentirum.Agent.Providers.OpenAI` | `UseOpenAI(model)` |
| `Sentirum.Agent.Providers.Anthropic` | `UseAnthropic(model)` |
| `Sentirum.Agent.Providers.Ollama` | `UseOllama(model)` |
| `Sentirum.Agent.Providers.MiniMax` | `UseMiniMax(model)` |
| `Sentirum.Agent.Providers.ZAI` | `UseZAI(model)` |
| `Sentirum.Agent.Providers.AzureOpenAI` | `UseAzureOpenAI(...)` |

Each extension reads a provider-specific environment variable when no explicit
API key is supplied and throws `InvalidOperationException` with a clear message
if neither source is available.

```csharp
services.AddSentirumAgent("bot", b => b.UseOpenAI("gpt-4o-mini"));
```

## Tier 2 — Compatible adapters

When your endpoint speaks the OpenAI or Anthropic wire protocol but is hosted
elsewhere (Groq, Together, vLLM, LM Studio, OpenRouter, AWS Bedrock proxy), use
the compatible adapter packages:

| Package | Extension method |
| --- | --- |
| `Sentirum.Agent.Providers.OpenAICompatible` | `UseOpenAICompatible(model, endpoint, ...)` |
| `Sentirum.Agent.Providers.AnthropicCompatible` | `UseAnthropicCompatible(model, endpoint, ...)` |

```csharp
services.AddSentirumAgent("groq", b => b
    .UseOpenAICompatible("llama-3.3-70b-versatile",
        endpoint: new Uri("https://api.groq.com/openai/v1"),
        apiKey: "gsk_..."));
```

## Tier 3 — Custom provider base class

When your LLM endpoint uses a proprietary protocol (gRPC, SignalR, custom REST,
or a queue-based transport), inherit from `SentirumChatClientBase` in the
`Sentirum.Agent.Providers.Custom` package. You implement two methods and get
retries, timeout handling, and structured logging for free.

```csharp
using Sentirum.Agent.Providers.Custom;

public sealed class AcmeChatClient : SentirumChatClientBase
{
    public AcmeChatClient(SentirumChatClientOptions options, ILogger<AcmeChatClient> logger)
        : base(options, logger) { }

    protected override Task<ChatResponse> CallProviderAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        CancellationToken ct)
    {
        // Your HTTP / gRPC / queue call here.
    }

    protected override IAsyncEnumerable<ChatResponseUpdate> CallProviderStreamingAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        CancellationToken ct)
    {
        // Your streaming call here.
    }
}
```

Register it with the builder:

```csharp
services.AddSentirumAgent("acme", b => b
    .UseChatClient(sp => new AcmeChatClient(
        SentirumChatClientOptions.Default,
        sp.GetRequiredService<ILogger<AcmeChatClient>>())));
```

## Tier 4 — Raw `IChatClient`

For maximum control, implement `Microsoft.Extensions.AI.IChatClient` directly
and pass it to `UseChatClient`. This bypasses all Sentirum base-class conveniences
but integrates cleanly with the rest of the pipeline (function invocation,
telemetry, context providers).

```csharp
services.AddSentirumAgent("raw", b => b
    .UseChatClient(_ => new MyRawChatClient()));
```

## Choosing the right tier

| Scenario | Tier |
| --- | --- |
| Using OpenAI, Anthropic, Ollama, MiniMax, Z.AI, or Azure OpenAI directly | **1** |
| Hosting behind an OpenAI/Anthropic-compatible proxy | **2** |
| Custom protocol with retries/logging baked in | **3** |
| Full control, no Sentirum helpers | **4** |
