# Sentirum.Agent.Providers.MiniMax

MiniMax provider for the Sentirum Agent SDK.

## Supported Protocols

| Protocol | Base URL | Description |
|---|---|---|
| OpenAI-Compatible | `https://api.minimax.io/v1` | Chat Completions wire format (default) |
| Anthropic-Compatible | `https://api.minimax.io/anthropic` | Messages API wire format |

## Usage

```csharp
// OpenAI-compatible (default)
services.AddSentirumAgent("minimax", b => b
    .UseMiniMax("MiniMax-M2.7", apiKey: "your-token-plan-key"));

// Anthropic-compatible
services.AddSentirumAgent("minimax", b => b
    .UseMiniMax("MiniMax-M2.7", apiKey: "your-token-plan-key",
        protocol: MiniMaxProtocol.Anthropic));
```

## Models

- `MiniMax-M2.7`
- `MiniMax-M2.7-highspeed`
