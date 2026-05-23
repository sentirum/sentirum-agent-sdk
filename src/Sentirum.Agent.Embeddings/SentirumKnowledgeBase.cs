using Sentirum.Agent.Context;

namespace Sentirum.Agent.Embeddings;

/// <summary>
/// An <see cref="IKnowledgeBase"/> implementation backed by a
/// <see cref="IVectorStore{TKey}"/> and <see cref="IEmbeddingGenerator"/>.
/// Queries are embedded and searched via cosine similarity.
/// </summary>
public sealed class SentirumKnowledgeBase<TKey> : IKnowledgeBase
    where TKey : notnull
{
    private readonly IVectorStore<TKey> _store;
    private readonly IEmbeddingGenerator _embeddings;

    public SentirumKnowledgeBase(IVectorStore<TKey> store, IEmbeddingGenerator embeddings)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _embeddings = embeddings ?? throw new ArgumentNullException(nameof(embeddings));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<KnowledgeBaseSnippet>> SearchAsync(
        string query,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        if (maxResults <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxResults), "Must be positive.");
        }

        var queryVector = await _embeddings.GenerateAsync(query, cancellationToken);

        var results = await _store.SearchAsync(
            queryVector,
            new VectorSearchOptions { TopK = maxResults },
            cancellationToken);

        return results.Select(r => new KnowledgeBaseSnippet(
            Title: r.Record.Text?[..Math.Min(100, r.Record.Text?.Length ?? 0)] ?? r.Record.Id?.ToString() ?? "",
            Content: r.Record.Text ?? "",
            Score: r.Score,
            SourceUrl: r.Record.Metadata?.TryGetValue("url", out var url) == true ? url?.ToString() : null))
            .ToList();
    }
}
