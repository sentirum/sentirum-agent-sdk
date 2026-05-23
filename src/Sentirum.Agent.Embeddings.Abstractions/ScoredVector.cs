namespace Sentirum.Agent.Embeddings;

/// <summary>
/// A vector record paired with its similarity score from a search operation.
/// </summary>
/// <typeparam name="TKey">The type of the record key.</typeparam>
public sealed record ScoredVector<TKey>
    where TKey : notnull
{
    /// <summary>
    /// The matched record.
    /// </summary>
    public required VectorRecord<TKey> Record { get; init; }

    /// <summary>
    /// Similarity score (higher is better). Exact range depends on the
    /// distance function (cosine similarity, dot product, etc.).
    /// </summary>
    public required float Score { get; init; }
}
