namespace Sentirum.Agent.Embeddings;

/// <summary>
/// Generates dense vector embeddings from text.
/// </summary>
public interface IEmbeddingGenerator
{
    /// <summary>
    /// Generates an embedding vector for a single text fragment.
    /// </summary>
    /// <param name="text">The text to embed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A read-only embedding vector.</returns>
    Task<ReadOnlyMemory<float>> GenerateAsync(
        string text,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates embeddings for multiple texts in a single batch call.
    /// </summary>
    /// <param name="texts">Texts to embed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Embedding vectors in the same order as the input texts.</returns>
    Task<IReadOnlyList<ReadOnlyMemory<float>>> GenerateAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The dimensionality of the generated vectors (e.g. 768, 1536).
    /// </summary>
    int Dimensions { get; }
}
