# Sentirum Agent SDK — Planning

This document is the long-form companion to [README.md](../README.md). It
captures the architecture, NuGet matrix, milestone plan, and the design
decisions behind the SDK.

## 1. Positioning

> **Sentirum.Agent** = .NET için, üretim-hazır, çoklu-LLM destekli, plugin
> tabanlı agent geliştirme kütüphanesi. Microsoft Agent Framework (MAF) ve
> `Microsoft.Extensions.AI` üzerine kurulu opinionated bir runtime + DX
> katmanıdır.

Three core promises:

1. **Az kod, çok davranış.** `AddSentirumAgent()` ile başla, agent çalışsın.
2. **Provider-agnostic.** OpenAI, Azure OpenAI, Anthropic, Ollama, ve her tür
   custom endpoint.
3. **Extensible.** Tools, hooks, sessions, memory, observability — her şey
   pluggable.

## 2. Architecture (3 katmanlı)

```
┌─────────────────────────────────────────────────┐
│  Layer 3:  Sentirum.Agent.* (opinionated DX)    │
│            AgentBuilder, Tree Sessions, vs.     │
├─────────────────────────────────────────────────┤
│  Layer 2:  Microsoft.Agents.AI (MAF)            │
│            ChatClientAgent, Workflows, Hosting  │
├─────────────────────────────────────────────────┤
│  Layer 1:  Microsoft.Extensions.AI.IChatClient  │
│            ⬇ custom providers plug in here       │
└─────────────────────────────────────────────────┘
```

Sentirum **wraps** MAF rather than forking it. Anything you can do with MAF
directly, you can still do — `ISentirumAgent` exposes the underlying `AIAgent`,
and `ISentirumSession` exposes the underlying `AgentSession`.

## 3. NuGet package matrix

| Layer | Package | Description |
| --- | --- | --- |
| **Core** | `Sentirum.Agent.Abstractions` | Contracts, options, DTOs. |
| | `Sentirum.Agent.Core` | Builder, runtime, MAF wrappers. |
| | `Sentirum.Agent.Hosting` | DI extensions, `IHostedService`. |
| **Providers** | `Sentirum.Agent.Providers.OpenAI` | OpenAI. |
| | `Sentirum.Agent.Providers.AzureOpenAI` | Azure OpenAI. |
| | `Sentirum.Agent.Providers.Anthropic` | Anthropic. |
| | `Sentirum.Agent.Providers.Ollama` | Local LLM. |
| | `Sentirum.Agent.Providers.OpenAICompatible` | Groq / Together / vLLM / LM Studio / custom URL. |
| | `Sentirum.Agent.Providers.AnthropicCompatible` | Bedrock / proxies. |
| | `Sentirum.Agent.Providers.Custom` | `SentirumChatClientBase` + helpers for full-custom HTTP/gRPC providers. |
| **Tools** | `Sentirum.Agent.Tools.Core` | `[Tool]` attribute, registry. |
| | `Sentirum.Agent.Tools.Mcp` | MCP client / server. |
| | `Sentirum.Agent.Tools.WebSearch` | Pluggable web search adapter. |
| **Sessions / Memory** | `Sentirum.Agent.Sessions.Tree` | Branching sessions. |
| | `Sentirum.Agent.Memory.InMemory` | Default in-memory store. |
| | `Sentirum.Agent.Memory.Redis` | Distributed sessions. |
| | `Sentirum.Agent.Memory.EntityFramework` | SQL-backed persistence. |
| **Workflows** | `Sentirum.Agent.Workflows` | MAF Workflows wrapper + DX. |
| **Observability** | `Sentirum.Agent.Observability` | OTel sink, cost + token tracking. |
| **Security** | `Sentirum.Agent.Security` | PII redaction, content filter middleware. |
| **Hosting** | `Sentirum.Agent.AspNetCore` | SSE / A2A endpoint helpers. |
| **Vertical** | `Sentirum.Agent.CustomerSupport` | Opinionated builder + adapters. |
| | `Sentirum.Agent.CustomerSupport.Zendesk` | (optional adapter) |
| **Testing** | `Sentirum.Agent.Testing` | Fakes, recording / replay. |
| **CLI** | `Sentirum.Agent.Cli` | `dotnet tool` for scaffolding. |

## 4. Custom provider strategy

Any class that implements `Microsoft.Extensions.AI.IChatClient` is automatically
compatible with MAF and therefore with Sentirum — agents, tool calling,
middleware, workflows, hosting, A2A all come for free.

Sentirum will ship four levels of custom-provider support:

| Scenario | Path |
| --- | --- |
| OpenAI-compatible endpoint (Groq, Together, vLLM, LM Studio, custom URL) | `Sentirum.Agent.Providers.OpenAICompatible` — base URL + key. |
| Anthropic-compatible endpoint (Bedrock, proxies) | `Sentirum.Agent.Providers.AnthropicCompatible`. |
| Fully custom HTTP API | `SentirumChatClientBase` — override one method, get retries, token tracking, rate limiting, logging. |
| gRPC / SignalR / queue-based transport | Implement `IChatClient` directly. |

## 5. Customer-support vertical (M8)

The primary launch vertical. On top of MAF's built-in hand-off / HITL /
checkpoint, the vertical package adds:

- `SupportAgentBuilder.WithTier1().WithEscalation().WithKnowledgeBase(...)`
- Ticket-aware tree sessions — each ticket is a session, branches are
  alternative resolution attempts.
- PII redaction middleware out of the box.
- Helpdesk adapters (Zendesk first, Intercom / Freshdesk later).
- Sentiment + escalation triggers as a context provider.
- Recording / replay for regression tests against real conversations.

## 6. Target framework policy

- **M0–M5:** `net10.0` only.
- **M6+:** add `net8.0` (LTS).
- `netstandard2.x`: **no** — MAF 1.6.2 stops at `netstandard2.0` and we gain
  little from supporting it.

## 7. Milestones

| Milestone | Duration | Output |
| --- | --- | --- |
| **M0** ✅ | 1 week | `slnx` + CPM + CI + `Abstractions` package + `01-HelloAgent`. |
| **M1** | 2 weeks | `Core` + `Hosting` + `Providers.OpenAI` + `01-HelloAgent` end-to-end. |
| **M2** | 1–2 weeks | `SentirumChatClientBase` + `OpenAICompatible` + Anthropic + Ollama. |
| **M3** | 2 weeks | `Tools.Core` + tree sessions + MCP. |
| **M4** | 1–2 weeks | `Memory.InMemory` + `Memory.Redis` + context providers (RAG). |
| **M5** | 2 weeks | `Workflows` wrapper + handoff + tool approval. |
| **M6** | 1 week | OTel + PII redaction + cost tracking. |
| **M7** | 1 week | SSE + A2A hosting. |
| **M8** | 2–3 weeks | `Sentirum.Agent.CustomerSupport` + full e2e sample. |
| **M9** | 2 weeks | DocFX, integration tests, NuGet publish — `v1.0.0`. |

**Total:** ~14–17 weeks for a single contributor.

## 8. Architecture Decision Records

Long-form ADRs live in [`docs/adr/`](./adr/) once they accumulate.

| # | Title | Status |
| --- | --- | --- |
| 0001 | Wrap Microsoft Agent Framework (do not fork) | Accepted |
| 0002 | Target `net10.0` only at M0 | Accepted |
| 0003 | Use the new `.slnx` solution format | Accepted |
| 0004 | License: MIT | Accepted |
| 0005 | NuGet prefix: `Sentirum.Agent.*` | Accepted |
