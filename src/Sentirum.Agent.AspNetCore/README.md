# Sentirum.Agent.AspNetCore

ASP.NET Core hosting helpers for Sentirum agents.

## Endpoints

### Agent HTTP / SSE

```csharp
app.MapSentirumAgent("/agents/support", agentName: "support");
app.MapSentirumAgentStreaming("/agents/support/stream", agentName: "support");
```

### A2A Protocol

```csharp
app.MapA2AEndpoints("support", new AgentCard
{
    Name = "Support Agent",
    Description = "Handles customer support inquiries.",
    Url = "https://api.example.com",
});
```

Exposes:
- `GET /.well-known/agent.json` — agent card
- `POST /a2a/tasks` — create task
- `GET /a2a/tasks/{id}` — task status
- `GET /a2a/tasks/{id}/stream` — SSE streaming

## SSE Streaming

The streaming endpoint returns `text/event-stream` with JSON-encoded
`ChatResponseUpdate` lines:

```
data: {"role":"assistant","text":"Hello"}

data: {"role":"assistant","text":" world"}

```
