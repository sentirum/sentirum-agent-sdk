# Sentirum.Agent.Testing

Test utilities for the Sentirum Agent SDK.

## Packages

- `RecordingChatClient` — wraps a real `IChatClient` and serializes every interaction to JSON.
- `ReplayChatClient` — replays recorded fixtures so tests run offline and deterministically.
- `FakeChatClient` — minimal in-memory fake that returns a canned reply.
- `SentirumAgentTestHost` — preconfigured DI host for agent tests.

## Recording a fixture

```csharp
var realClient = new OpenAIClient(apiKey).GetChatClient("gpt-4o-mini").AsIChatClient();
var recorder = new RecordingChatClient(realClient);

var agent = SentirumAgentTestHost.CreateServices()
    .AddSentirumAgent("demo", b => b.UseChatClient(_ => recorder))
    .BuildServiceProvider()
    .GetAgent("demo");

var response = await agent.RunAsync(session, new ChatMessage(ChatRole.User, "Hello!"));

// Export fixture
File.WriteAllText("fixture.json", recorder.SaveToString());
```

## Replaying a fixture in tests

```csharp
var fixture = File.ReadAllText("fixture.json");
var services = SentirumAgentTestHost.CreateServices()
    .AddReplayAgent("demo", fixture);

var agent = services.BuildServiceProvider().GetAgent("demo");
var response = await agent.RunAsync(session, new ChatMessage(ChatRole.User, "Hello!"));
// → returns the recorded response without network access
```

## Using a fake

```csharp
var services = SentirumAgentTestHost.CreateServices()
    .AddFakeAgent("demo", cannedReply: "42");

var agent = services.BuildServiceProvider().GetAgent("demo");
```
