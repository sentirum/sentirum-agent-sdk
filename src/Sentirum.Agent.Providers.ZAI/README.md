# Sentirum.Agent.Providers.ZAI

[Z.AI (GLM)](https://docs.z.ai) provider for the **Sentirum Agent SDK**.

Z.AI is interesting because it exposes the same GLM model family behind
**two** different wire protocols:

| Protocol | Endpoint | Use when |
| --- | --- | --- |
| `ZaiProtocol.OpenAI` *(default)* | `https://api.z.ai/api/paas/v4` | You want OpenAI-style tool calling, structured outputs, the full OpenAI client surface. |
| `ZaiProtocol.Anthropic` | `https://api.z.ai/api/anthropic` | You're migrating from Claude / Claude Code / want Anthropic-style messages. |

```csharp
// Default — OpenAI protocol
services.AddSentirumAgent("glm", b => b
    .UseZAI("glm-4.6", apiKey: zaiKey)
    .EnableZaiThinking()
    .WithInstructions("You are a Sentirum support agent."));

// Anthropic protocol — same model family, different wire format
services.AddSentirumAgent("glm-claude", b => b
    .UseZAI("glm-4.7", apiKey: zaiKey, protocol: ZaiProtocol.Anthropic));
```

## Thinking mode

`EnableZaiThinking()` adds the `thinking: { type: "enabled" }` field to every
request via `ChatOptions.AdditionalProperties` so reasoning models such as
`glm-4.6` and `glm-4.7` emit their `reasoning_content` chunks.

## API key

You can pass `apiKey` explicitly or set `ZAI_API_KEY` in the environment;
the latter is what most Z.AI tutorials assume.

## Known limitations

- The Anthropic-protocol path uses the official `Anthropic` .NET SDK, which
  strictly validates response shapes. Z.AI's gateway sometimes omits newer
  Anthropic fields (e.g. `web_fetch_requests`) that cause an
  `AnthropicInvalidDataException` for non-trivial replies. **Prefer
  `ZaiProtocol.OpenAI` unless you specifically need the Anthropic wire
  format.** A future release may add a tolerant deserializer.
- Tool calling on the Anthropic-protocol path requires the model to support
  Anthropic-style tools; GLM models generally do, but provider-specific
  shapes may vary.

## License

MIT
