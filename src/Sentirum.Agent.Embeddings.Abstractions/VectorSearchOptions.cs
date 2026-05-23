namespace Sentirum.Agent.Embeddings;

/// <summary>
/// Options that control vector similarity search behaviour.
/// </summary>
public sealed record VectorSearchOptions
{
    /// <summary>
    /// Maximum number of results to return. Default is 5.
    /// </summary>
    public int TopK { get; init; } = 5;

    /// <summary>
    /// Minimum similarity score threshold. Results below this score
    /// are filtered out. Default is 0 (no threshold).
    /// </summary>
    public float? MinScore { get; init; }

    /// <summary>
    /// Optional metadata filter expression.
    /// Format depends on the backing store (e.g. JSONPath, SQL WHERE).
    /// </summary>
    public string? Filter { get; init; }
}
