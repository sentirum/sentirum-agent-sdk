# Getting Started

Sentirum Agent SDK is an opinionated .NET runtime for building AI agents on top
of [Microsoft Agent Framework](https://learn.microsoft.com/agent-framework/) and
`Microsoft.Extensions.AI`.

## Prerequisites

- .NET 8 SDK or later
- An LLM provider API key (e.g. OpenAI, Anthropic, or a compatible endpoint)

## Install the packages

```bash
dotnet add package Sentirum.Agent.Hosting
dotnet add package Sentirum.Agent.Providers.OpenAI
```

## Create your first agent

Register an agent with the dependency injection container using
`AddSentirumAgent` and select a provider with `UseOpenAI`:

```csharp
using Microsoft.Extensions.AI;
using Sentirum.Agent;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSentirumAgent("greeter", agent => agent
    .UseOpenAI("gpt-4o-mini"));

var app = builder.Build();

// Resolve the agent by name and chat.
var agent = app.Services.GetRequiredKeyedService<ISentirumAgent>("greeter");
var session = await agent.CreateSessionAsync();

var reply = await session.SendMessageAsync("Hello! Tell me a joke.");
Console.WriteLine(reply);
```

`UseOpenAI` reads the `OPENAI_API_KEY` environment variable when no explicit key
is passed. Function-invocation middleware is wired automatically so any registered
`AIFunction` tools are executed transparently.

## Use a different provider

The same builder pattern works for every provider package:

```csharp
// Anthropic
services.AddSentirumAgent("claude", b => b.UseAnthropic("claude-sonnet-4-20250514"));

// Ollama (local)
services.AddSentirumAgent("local", b => b.UseOllama("llama3"));

// OpenAI-compatible (Groq, Together, vLLM, LM Studio, etc.)
services.AddSentirumAgent("groq", b => b
    .UseOpenAICompatible("llama-3.3-70b-versatile",
        endpoint: new Uri("https://api.groq.com/openai/v1"),
        apiKey: "gsk_..."));
```

## Add tools

Decorate a class with `[Tool]` and register it with the builder:

```csharp
using Sentirum.Agent.Tools;

[Tool]
public static class MathTools
{
    [Description("Adds two numbers")]
    public static double Add(double a, double b) => a + b;
}

services.AddSentirumAgent("math", b => b
    .UseOpenAI("gpt-4o-mini")
    .WithTools<MathTools>());
```

## Next steps

- [Building Custom Providers](custom-providers.md) — integrate your own LLM endpoint.
- [Workflows](workflows.md) — compose multi-agent orchestrations.
- [Human-in-the-Loop](hitl.md) — add approval gates to your workflows.
