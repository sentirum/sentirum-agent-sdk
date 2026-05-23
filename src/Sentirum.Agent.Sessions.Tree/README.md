# Sentirum.Agent.Sessions.Tree

**Tree-based sessions** for the Sentirum Agent SDK — the headline feature
that sets the SDK apart from Microsoft Agent Framework, AutoGen, and
Semantic Kernel, all of which assume a single linear conversation.

A tree session lets you:

- **Fork** a conversation into multiple isolated branches at any point.
- **Run alternatives in parallel** — refund vs. replacement vs. discount —
  each branch with its own tool calls and decisions.
- **Merge** the winning branch back into the parent timeline.
- **Walk** the resulting tree and visualize it (ASCII or otherwise).
- **Compare** branches by token / cost / message count.

## Install

```csharp
services.AddSentirumCore();
services.AddSentirumTreeSessions();          // replaces the in-memory store
services.AddSentirumAgent("support", b => b.UseOpenAI("gpt-4o-mini"));
```

## Quick start

```csharp
var store = services.GetRequiredService<ITreeSessionStore>();
var root  = await store.CreateAsync("support");

await agent.RunAsync(root, new ChatMessage(ChatRole.User, "Siparişimi iptal et"));

// Explore three resolution paths in isolation.
var refund      = await store.ForkAsync(root);
var replacement = await store.ForkAsync(root);
var discount    = await store.ForkAsync(root);

await agent.RunAsync(refund,      new ChatMessage(ChatRole.User, "Para iadesi yap"));
await agent.RunAsync(replacement, new ChatMessage(ChatRole.User, "Aynı ürünü tekrar gönder"));
await agent.RunAsync(discount,    new ChatMessage(ChatRole.User, "Bir sonraki siparişe %20 indirim ver"));

// Compare and pick the winner.
var diff = await store.CompareAsync(refund, replacement);
Console.WriteLine(diff);                      // tokens, messages, depth

// Merge the chosen branch back into the timeline.
await store.MergeAsync(source: refund, target: root);

// Visualize.
var tree = await store.GetTreeAsync(root.Id);
Console.WriteLine(tree.ToAsciiTree());
```

## How forking really works

`ForkAsync` uses `AIAgent.SerializeSessionAsync` + `DeserializeSessionAsync` to
produce a deep copy of the underlying `AgentSession`. The two branches share
no state — tool calls, message history, and provider-specific context are all
independent.

## License

MIT
