# Sentirum Agent SDK

An opinionated, production-oriented **.NET SDK for building AI agents**, built
on top of [Microsoft Agent Framework][maf] and [`Microsoft.Extensions.AI`][meai].

> **Status:** early. M0–M3 + the M3.5 hardening sprint are in.
> Current package version: `0.1.0-preview`. Until v1.0, every minor preview
> release may break. See [`docs/adr/`](docs/adr/) for the durable design
> decisions that now back the public surface.

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
| `Sentirum.Agent.Providers.OpenAI` | OpenAI provider with optional endpoint override. |
| `Sentirum.Agent.Providers.OpenAICompatible` | Any OpenAI-compatible endpoint (Groq, Together, vLLM, LM Studio, Z.AI, OpenRouter). |
| `Sentirum.Agent.Providers.Anthropic` | Anthropic Messages API. |
| `Sentirum.Agent.Providers.AnthropicCompatible` | Any Anthropic-compatible endpoint (Bedrock proxy, Z.AI Anthropic route). |
| `Sentirum.Agent.Providers.Ollama` | Local LLM via OllamaSharp. |
| `Sentirum.Agent.Providers.Custom` | `SentirumChatClientBase` — base class with retry / timeout / structured logging for fully custom providers. |
| `Sentirum.Agent.Providers.ZAI` | Z.AI (GLM) convenience: `UseZAI(model, key, protocol)` + thinking mode helpers. |
| `Sentirum.Agent.Tools.Core` | `[Tool]` attribute + reflection-based discovery + `WithTools<T>()`. |
| `Sentirum.Agent.Sessions.Tree` | Tree-based sessions: fork / merge / walk / visualize. |
| `Sentirum.Agent.Memory.Abstractions` | `ISentirumMemoryStore` + `MemoryScope` (Global / Agent / User / Session). |
| `Sentirum.Agent.Memory.InMemory` | In-process memory store. |
| `Sentirum.Agent.Memory.Redis` | Distributed memory store backed by Redis. |
| `Sentirum.Agent.Memory.EntityFrameworkCore` | SQL memory store backed by EF Core. |
| `Sentirum.Agent.Context` | `WithUserMemory()`, `WithAmbientInstructions()`, `WithKnowledgeBase()` context providers over the MAF `MessageAIContextProvider` pipeline. |

More packages (Workflows, CustomerSupport) land per the milestone plan below.

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
| **M2** ✅ | Multi-provider: Anthropic, Ollama, OpenAI/Anthropic-compatible adapters, `SentirumChatClientBase`, Z.AI convenience + `02-MultiProvider`. |
| **M3** ✅ | Tools (`[Tool]` + `WithTools<T>()`) and tree sessions (fork / merge / walk / visualize) + `03-ToolCalling` + `04-SessionForking`. |
| **M3.5** ✅ | Hardening pass after the `gpt-5.5` code review: thread-safe forks, direction-safe merges with deep-cloned messages, agent disposal, tool-signature validation, options validation, streaming-cancellation observability, ADR-0001–0006. |
| **M4** ✅ | Memory (`InMemory` / `Redis` / `EntityFrameworkCore`) + context providers (`WithUserMemory`, `WithAmbientInstructions`, `WithKnowledgeBase`) + samples `05-Memory` and `06-Rag` verified live against Z.AI / GLM-4.6. |
| **M4.5** ✅ | Hardening pass after the `glm-5` code review: strict `MemoryPartition.Validate()`, EF Core upsert via `ExecuteUpdateAsync` + side-effect-free `GetAsync`, Redis envelope via `JsonSerializer` (surrogate-safe), multi-tenant `WithUserMemory(Func<...>)` + `WithSessionMemory(Func<...>)`, ADR-0007–0010, +15 tests. |
| **M5** ✅ | Workflows + Human-in-the-Loop: `Sentirum.Agent.Workflows` (Sequential / Concurrent / Handoff / UseWorkflow over MAF `Microsoft.Agents.AI.Workflows` 1.6.2) + `Sentirum.Agent.Workflows.HumanInTheLoop` (typed `ApprovalGate`, `ApprovalDispatcher`, `IApprovalChannel` / `InMemoryApprovalChannel`). Samples `07-Workflow` (concurrent + sequential triage live against Z.AI / GLM-4.6) and `08-HITL` (refund approval pipeline with policy-bot reviewer) plus ADR-0011 and +16 tests. |
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
