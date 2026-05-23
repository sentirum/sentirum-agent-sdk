# Sentirum.Agent.Embeddings

Default embeddings and vector store implementations for Sentirum.Agent SDK.

## Features

- `InMemoryVectorStore<TKey>` — Brute-force cosine similarity search, suitable
  for prototyping and small datasets. Supports eviction (MaxRecordCount).
- `SentirumKnowledgeBase<TKey>` — Bridges `IVectorStore<TKey>` + `IEmbeddingGenerator`
  to implement `IKnowledgeBase` for the RAG pipeline.
- DI registration helpers via `AddInMemoryVectorStore<TKey>()` and `AddEmbeddingGenerator()`.

## Usage

```csharp
services.AddInMemoryVectorStore<string>("docs", dimensions: 1536);
services.AddEmbeddingGenerator(sp => new OpenAIEmbeddingGenerator("text-embedding-3-small", apiKey));
```
