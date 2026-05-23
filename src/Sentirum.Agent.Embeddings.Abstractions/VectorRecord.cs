namespace Sentirum.Agent.Embeddings;

/// <summary>
/// A single vector record stored in a vector collection.
/// </summary>
/// <typeparam name="TKey">The type of the record key.</typeparam>
public sealed record VectorRecord<TKey>
    where TKey : notnull
{
    /// <summary>
    /// Unique identifier for this record.
    /// </summary>
    public required TKey Id { get; init; }

    /// <summary>
    /// The dense embedding vector.
    /// </summary>
    public required ReadOnlyMemory<float> Vector { get; init; }

    /// <summary>
    /// Optional textual payload (e.g. the original text chunk).
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// Optional metadata bag for filtering or display.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Metadata { get; init; }

    /// <summary>
    /// UTC timestamp when the record was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
