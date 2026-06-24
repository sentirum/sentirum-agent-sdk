# Sentirum Agent SDK Documentation

Sentirum Agent SDK is an opinionated, production-oriented **.NET SDK for building
AI agents**, built on top of [Microsoft Agent Framework][maf] and
[`Microsoft.Extensions.AI`][meai].

It provides a fluent builder with sensible defaults, tree-based sessions,
first-class custom provider support, multi-agent workflows, and human-in-the-loop
approval gates — all wired for production with OpenTelemetry, cost tracking, and
recording/replay testing.

## Articles

- [Getting Started](articles/index.md) — create your first agent in 5 lines.
- [Building Custom Providers](articles/custom-providers.md) — the 4-tier provider
  strategy: first-party, compatible adapters, custom base class, or raw
  `IChatClient`.
- [Workflows](articles/workflows.md) — Sequential, Concurrent, ConcurrentJoin,
  and Handoff multi-agent orchestrations.
- [Human-in-the-Loop](articles/hitl.md) — typed approval gates with
  `ApprovalGate`, `ApprovalDispatcher`, and `IApprovalChannel`.

## API Reference

Auto-generated API documentation is available from the reference link in the
navigation.

[maf]: https://learn.microsoft.com/agent-framework/
[meai]: https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai
