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

Use [`0000-template.md`](0000-template.md) when proposing a new ADR.
