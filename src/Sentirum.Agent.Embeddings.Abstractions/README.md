# Sentirum.Agent.Embeddings.Abstractions

Embedding and vector search abstractions for Sentirum.Agent SDK.

## Interfaces

- `IEmbeddingGenerator` — Generates dense vector embeddings from text.
- `IVectorStore<TKey>` — CRUD + similarity search over a vector collection.
- `IVectorSearch<TKey>` — Similarity search only.

## DTOs

- `VectorRecord<TKey>` — A stored vector with optional text and metadata.
- `ScoredVector<TKey>` — A vector record paired with its similarity score.
- `VectorSearchOptions` — TopK, min score threshold, and filter expression.
