# Sentirum.Agent.Abstractions

Core abstractions and contracts for the **Sentirum Agent SDK**. This package
contains only interfaces, DTOs, and base types — no runtime behavior.

It builds on top of:

- [Microsoft Agent Framework](https://learn.microsoft.com/agent-framework/) (`Microsoft.Agents.AI.*`)
- [Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai) (`IChatClient`)

Reference this package when authoring custom providers, tools, or extensions
without taking a dependency on the full runtime.

## Install

```bash
dotnet add package Sentirum.Agent.Abstractions
```

## License

MIT
