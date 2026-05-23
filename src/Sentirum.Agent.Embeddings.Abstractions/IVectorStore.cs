namespace Sentirum.Agent.Embeddings;

/// <summary>
/// A typed vector collection supporting CRUD and similarity search.
/// </summary>
/// <typeparam name="TKey">The type of the record key.</typeparam>
public interface IVectorStore<TKey> : IVectorSearch<TKey>
    where TKey : notnull
{
    /// <summary>
    /// Name of the collection (table, index, etc.).
    /// </summary>
    string CollectionName { get; }

    /// <summary>
    /// Number of dimensions for vectors in this collection.
    /// </summary>
    int Dimensions { get; }

    /// <summary>
    /// Adds or updates a single record.
    /// </summary>
    Task UpsertAsync(
        VectorRecord<TKey> record,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds or updates multiple records in a batch.
    /// </summary>
    Task UpsertAsync(
        IEnumerable<VectorRecord<TKey>> records,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a record by its unique key.
    /// </summary>
    /// <returns>The record, or <c>null</c> if not found.</returns>
    Task<VectorRecord<TKey>?> GetAsync(
        TKey key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a record by its unique key.
    /// </summary>
    /// <returns><c>true</c> if the record was found and removed.</returns>
    Task<bool> DeleteAsync(
        TKey key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all records from the collection.
    /// </summary>
    /// <returns>Number of records removed.</returns>
    Task<int> ClearAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Number of records currently stored.
    /// </summary>
    Task<long> CountAsync(
        CancellationToken cancellationToken = default);
}
