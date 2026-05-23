namespace Sentirum.Agent.Embeddings;

/// <summary>
/// Performs similarity search over a collection of vectors.
/// </summary>
/// <typeparam name="TKey">The type of the record key.</typeparam>
public interface IVectorSearch<TKey>
    where TKey : notnull
{
    /// <summary>
    /// Searches the collection for vectors most similar to <paramref name="queryVector"/>.
    /// </summary>
    /// <param name="queryVector">The query embedding vector.</param>
    /// <param name="options">Search options (top-k, filters, etc.).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Scored results ordered by descending similarity.</returns>
    Task<IReadOnlyList<ScoredVector<TKey>>> SearchAsync(
        ReadOnlyMemory<float> queryVector,
        VectorSearchOptions? options = null,
        CancellationToken cancellationToken = default);
}
