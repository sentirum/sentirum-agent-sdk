# Sentirum Agent SDK

[![CI](https://github.com/sentirum/sentirum-agent-sdk/actions/workflows/ci.yml/badge.svg)](https://github.com/sentirum/sentirum-agent-sdk/actions)
[![NuGet](https://img.shields.io/nuget/vpre/Sentirum.Agent.Core.svg)](https://www.nuget.org/packages/Sentirum.Agent.Core/)

An opinionated, production-oriented **.NET SDK for building AI agents**, built
on top of [Microsoft Agent Framework][maf] and [`Microsoft.Extensions.AI`][meai].

> **Status:** M0–M9 complete. Current package version: `v1.0.0`.
> See [`docs/adr/`](docs/adr/) for durable design decisions.

## Why Sentirum?

Microsoft Agent Framework gives you the low-level building blocks (`AIAgent`,
`AgentSession`, `AIContextProvider`, `Workflow`). Sentirum gives you the
opinionated runtime on top:

- �� **Fluent builder** with sensible defaults — agents in 5 lines.
- �� **Tree-based sessions** — branch and fork conversations natively.
- �� **First-class custom providers** — point at any HTTP, gRPC, or proprietary
  LLM endpoint via `IChatClient`.
- 🏗️ **Workflows** — Sequential / Concurrent / Handoff over MAF workflows.
- 🚦 **Human-in-the-Loop** — typed approval gates with dispatcher + channel abstractions.
- 🏢 **Customer-support vertical** — end-to-end demo API: classify → parallel specialists → HITL gate.
- �� **OpenTelemetry, cost and token tracking** wired by default.
- �� **Recording / replay** test SDK so you can capture real conversations and
  use them as fixtures.

## Packages

29 packages on NuGet. See the [planning document](docs/planning.md) for the
full package matrix and roadmap.

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
| `Sentirum.Agent.Providers.MiniMax` | MiniMax dual-protocol: OpenAI-compatible (`api.minimax.io/v1`) and Anthropic-compatible (`api.minimax.io/anthropic`). Models: MiniMax-M2.7. |
| `Sentirum.Agent.Providers.AzureOpenAI` | Azure OpenAI Service with API-key and Azure AD (`DefaultAzureCredential`) auth. |
| `Sentirum.Agent.Tools.Core` | `[Tool]` attribute + reflection-based discovery + `WithTools<T>()`. |
| `Sentirum.Agent.Sessions.Tree` | Tree-based sessions: fork / merge / walk / visualize. |
| `Sentirum.Agent.Memory.Abstractions` | `ISentirumMemoryStore` + `MemoryScope` (Global / Agent / User / Session). |
| `Sentirum.Agent.Memory.InMemory` | In-process memory store. |
| `Sentirum.Agent.Memory.Redis` | Distributed memory store backed by Redis. |
| `Sentirum.Agent.Memory.EntityFrameworkCore` | SQL memory store backed by EF Core. |
| `Sentirum.Agent.Context` | `WithUserMemory()`, `WithAmbientInstructions()`, `WithKnowledgeBase()` context providers over the MAF `MessageAIContextProvider` pipeline. |
| `Sentirum.Agent.Embeddings.Abstractions` | `IEmbeddingGenerator`, `IVectorStore<TKey>`, `IVectorSearch<TKey>`, `VectorRecord<TKey>`, `ScoredVector<TKey>`. |
| `Sentirum.Agent.Embeddings` | `InMemoryVectorStore<TKey>` (cosine similarity), `SentirumKnowledgeBase<TKey>` (RAG bridge), DI registration helpers. |
| `Sentirum.Agent.Workflows` | Sequential / Concurrent / Handoff / UseWorkflow wrappers over MAF `Microsoft.Agents.AI.Workflows` 1.6.2. |
| `Sentirum.Agent.Workflows.HumanInTheLoop` | Typed `ApprovalGate`, `ApprovalDispatcher`, `IApprovalChannel` / `InMemoryApprovalChannel`. |
| `Sentirum.Agent.Testing` | `RecordingChatClient`, `ReplayChatClient`, `FakeChatClient`, and `SentirumAgentTestHost` for unit and integration tests. |
| `Sentirum.Agent.Observability` | OpenTelemetry spans, per-request cost tracking, and token budget enforcement. |
| `Sentirum.Agent.AspNetCore` | `MapSentirumAgent()`, SSE streaming, and A2A protocol endpoints. |
| `Sentirum.Agent.CustomerSupport` | Support ticket domain model, triage workflow factory, amount-based approval gate. |
| `Sentirum.Agent.Tools.Mcp` | Model Context Protocol integration: consume MCP server tools as `AIFunction` instances. |

## Target framework

`net8.0` and `net10.0`. `net8.0` LTS support added in M6.

## Build locally

```bash
dotnet restore Sentirum.Agent.slnx
dotnet build  Sentirum.Agent.slnx -c Release
dotnet test   Sentirum.Agent.slnx -c Release --no-build
```

## M8 Customer Support Vertical (sample)

The [`09-CustomerSupport`](samples/09-CustomerSupport) sample is a
production-ish ASP.NET Core Minimal API that wires every SDK primitive
into a single support-triage pipeline:

```bash
# Run
ZAI_API_KEY=... dotnet run --project samples/09-CustomerSupport

# Create a ticket (classifier → parallel specialists → HITL gate if >$100)
curl -X POST http://localhost:5000/support/ticket \
  -H 'Content-Type: application/json' \
  -d '{"customerId":"u-123","subject":"Damaged item","description":"My package arrived crushed. I paid $149 for it."}'
# → {"id":"a1b2c3d4e5f6","status":"PendingApproval","category":"damaged-product",...}

# Approve (or reject) a pending ticket
curl -X POST http://localhost:5000/support/approve/a1b2c3d4e5f6 \
  -H 'Content-Type: application/json' \
  -d '{"approved":true,"reviewer":"ops@sentirum","comment":"lgtm"}'
```

Features demonstrated: multi-agent DI registry, `ConcurrentJoin` workflow,
`InMemoryApprovalChannel` HITL gate, `ISentirumMemoryStore` audit trail,
typed JSON DTOs.

## Roadmap

| Milestone | Scope | Status |
| --- | --- | --- |
| **M0** | Solution foundation, CPM, CI, `Abstractions` package | ✅ |
| **M1** | Core runtime + Hosting + OpenAI provider + `01-HelloAgent` | ✅ |
| **M2** | Multi-provider: Anthropic, Ollama, compatible adapters, Z.AI + `02-MultiProvider` | ✅ |
| **M3** | Tools (`[Tool]` + `WithTools<T>()`) and tree sessions + `03-ToolCalling` + `04-SessionForking` | ✅ |
| **M3.5** | Hardening: thread-safe forks, agent disposal, tool validation, ADR-0001–0006 | ✅ |
| **M4** | Memory (`InMemory` / `Redis` / `EF Core`) + context providers + `05-Memory` + `06-Rag` | ✅ |
| **M4.5** | Hardening: `MemoryPartition` validation, Redis envelopes, ADR-0007–0010 | ✅ |
| **M5** | Workflows + HITL + `07-Workflow` + `08-HITL` + ADR-0011 | ✅ |
| **M6** | `net8.0` multi-targeting, `Testing` package, Observability, Security, AsyncLocal fixes | ✅ |
| **M7** | ASP.NET Core hosting (SSE, A2A) via `Sentirum.Agent.AspNetCore` | ✅ |
| **M8** | Customer Support Vertical + `09-CustomerSupport` | ✅ |
| **M9** | Docs, integration tests, NuGet stable — `v1.0.0` | ✅ |

## License

MIT — see [LICENSE](LICENSE).

[maf]: https://learn.microsoft.com/agent-framework/
[meai]: https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai
