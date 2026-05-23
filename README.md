# Sentirum Agent SDK

An opinionated, production-oriented **.NET SDK for building AI agents**, built
on top of [Microsoft Agent Framework][maf] and [`Microsoft.Extensions.AI`][meai].

> **Status:** very early. M0 (foundation) is in. M1 (Core MVP) is next.
> Until v1.0, every minor preview release may break.

## Why Sentirum?

Microsoft Agent Framework gives you the low-level building blocks (`AIAgent`,
`AgentSession`, `AIContextProvider`, `Workflow`). Sentirum gives you the
opinionated runtime on top:

- �� **Fluent builder** with sensible defaults — agents in 5 lines.
- �� **Tree-based sessions** — branch and fork conversations natively.
- �� **First-class custom providers** — point at any HTTP, gRPC, or proprietary
  LLM endpoint via `IChatClient`.
- ��️ **Customer-support vertical** — opinionated handoff, escalation, PII
  redaction, knowledge-base retrieval out of the box.
- �� **OpenTelemetry, cost and token tracking** wired by default.
- �� **Recording / replay** test SDK so you can capture real conversations and
  use them as fixtures.

## Packages

See the [planning document](docs/planning.md) for the full package matrix and
roadmap. The current published surface is:

| Package | Purpose |
| --- | --- |
| `Sentirum.Agent.Abstractions` | Interfaces, options, and DTOs — the contract everything else builds against. |
| `Sentirum.Agent.Core` | Default runtime: agent, session, registry, builder, in-memory session store. |
| `Sentirum.Agent.Hosting` | `IServiceCollection.AddSentirumAgent(...)` registration. |
| `Sentirum.Agent.Providers.OpenAI` | OpenAI provider wrapped behind `IChatClient`. |

More packages (additional providers, Sessions.Tree, Workflows, CustomerSupport)
land per the milestone plan below.

## Target framework

`net10.0` only at M0. `net8.0` LTS support is planned once the surface stabilizes.

## Build locally

```bash
dotnet restore Sentirum.Agent.slnx
dotnet build  Sentirum.Agent.slnx -c Release
dotnet test   Sentirum.Agent.slnx -c Release --no-build
```

## Roadmap (high level)

| Milestone | Scope |
| --- | --- |
| **M0** ✅ | Solution foundation, CPM, CI, `Abstractions` package, sample. |
| **M1** ✅ | Core runtime + Hosting + OpenAI provider + `01-HelloAgent` end-to-end. |
| **M2** | Custom providers (`SentirumChatClientBase`, OpenAI-compatible, Anthropic, Ollama). |
| **M3** | Tool registry + tree sessions + MCP. |
| **M4** | Memory + context providers + RAG. |
| **M5** | Workflows wrapper + handoff + HITL. |
| **M6** | Observability + security (PII redaction, content filters). |
| **M7** | ASP.NET Core hosting (SSE, A2A). |
| **M8** | `Sentirum.Agent.CustomerSupport` opinionated vertical + full sample. |
| **M9** | Docs, integration tests, NuGet publish — `v1.0.0`. |

## License

MIT — see [LICENSE](LICENSE).

[maf]: https://learn.microsoft.com/agent-framework/
[meai]: https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai
