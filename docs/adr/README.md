# Architecture Decision Records

This folder captures durable design decisions that shape the public
Sentirum surface. Each ADR is a numbered Markdown file that explains
the context, the decision, the consequences, and what we rejected.

## Index

| ADR | Title | Status |
| --- | --- | --- |
| [0001](0001-chat-client-ownership-and-disposal.md) | Chat-client ownership and agent disposal | Accepted |
| [0002](0002-tree-session-merge-semantics.md) | Tree-session merge semantics | Accepted |
| [0003](0003-builder-pipeline-ordering.md) | Builder pipeline ordering and naming | Accepted |
| [0004](0004-maf-exposure-policy.md) | Microsoft Agent Framework exposure policy | Accepted |
| [0005](0005-deferred-builder-hooks-asynclocal.md) | Deferred builder hooks via AsyncLocal | Accepted (revisit before v1.0) |
| [0006](0006-provider-credential-policy.md) | Provider credential and secret-handling policy | Accepted |
| [0007](0007-memory-scope-model.md) | Memory scope model — opaque string store, strict partitions | Accepted |
| [0008](0008-context-provider-mounting.md) | Context-provider mounting point — chat-client innermost | Accepted |
| [0009](0009-message-context-provider-choice.md) | Why context providers derive from `MessageAIContextProvider` | Accepted |
| [0010](0010-redis-hash-per-partition-ttl.md) | Redis hash-per-partition with hash-level TTL | Accepted (revisit when per-entry TTL demand appears) |

Use [`0000-template.md`](0000-template.md) when proposing a new ADR.
