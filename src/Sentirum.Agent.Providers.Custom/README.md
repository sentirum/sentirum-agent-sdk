# Sentirum.Agent.Providers.Custom

Base classes and helpers for writing **fully custom `IChatClient` providers**
for the Sentirum Agent SDK. Bring your HTTP / gRPC / proprietary endpoint and
get production conveniences (retries, rate limiting, structured logging,
token tracking) without having to reinvent them.

## When to use this

- Your LLM lives behind a custom REST endpoint that does **not** speak the
  OpenAI or Anthropic wire format.
- You need to route through gRPC, SignalR, a queue, or another transport.
- You have provider-specific behaviors (auth header rotation, request
  signing, multi-region failover) that don't fit into the
  `OpenAICompatible` / `AnthropicCompatible` adapters.

If you only need a different base URL on top of OpenAI or Anthropic, prefer
`Sentirum.Agent.Providers.OpenAICompatible` or `.AnthropicCompatible` instead.

## Quick start

```csharp
public sealed class AcmeChatClient : SentirumChatClientBase
{
    public AcmeChatClient(SentirumChatClientOptions options, ILogger<AcmeChatClient> logger)
        : base(options, logger) { }

    protected override Task<ChatResponse> CallProviderAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        CancellationToken ct)
    {
        // your HTTP/gRPC/queue call here
    }

    protected override IAsyncEnumerable<ChatResponseUpdate> CallProviderStreamingAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        CancellationToken ct)
    {
        // your streaming call here
    }
}
```

Then register the agent:

```csharp
services.AddSentirumAgent("acme", b => b
    .UseChatClient(sp => new AcmeChatClient(
        SentirumChatClientOptions.Default,
        sp.GetRequiredService<ILogger<AcmeChatClient>>())));
```

## License

MIT
