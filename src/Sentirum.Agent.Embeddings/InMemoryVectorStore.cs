using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Sentirum.Agent.Embeddings;

/// <summary>
/// In-memory vector store using brute-force cosine similarity.
/// Suitable for prototyping, small datasets, and unit tests.
/// Not intended for production workloads.
/// </summary>
/// <typeparam name="TKey">The type of the record key.</typeparam>
public sealed class InMemoryVectorStore<TKey> : IVectorStore<TKey>
    where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, VectorRecord<TKey>> _records = new();

    /// <inheritdoc />
    public string CollectionName { get; }

    /// <inheritdoc />
    public int Dimensions { get; }

    /// <summary>
    /// Maximum number of records before proactive eviction kicks in.
    /// Set to <c>0</c> to disable. Default is <c>10_000</c>.
    /// </summary>
    public int MaxRecordCount { get; init; } = 10_000;

    public InMemoryVectorStore(string collectionName, int dimensions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionName);
        if (dimensions <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dimensions), "Must be positive.");
        }

        CollectionName = collectionName;
        Dimensions = dimensions;
    }

    /// <inheritdoc />
    public Task UpsertAsync(
        VectorRecord<TKey> record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();

        ValidateDimensions(record.Vector);

        _records[record.Id] = record;

        if (MaxRecordCount > 0 && _records.Count > MaxRecordCount)
        {
            EvictOldest();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpsertAsync(
        IEnumerable<VectorRecord<TKey>> records,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateDimensions(record.Vector);
            _records[record.Id] = record;
        }

        if (MaxRecordCount > 0 && _records.Count > MaxRecordCount)
        {
            EvictOldest();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<VectorRecord<TKey>?> GetAsync(
        TKey key,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        cancellationToken.ThrowIfCancellationRequested();

        _records.TryGetValue(key, out var record);
        return Task.FromResult(record);
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(
        TKey key,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(_records.TryRemove(key, out _));
    }

    /// <inheritdoc />
    public Task<int> ClearAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var count = _records.Count;
        _records.Clear();
        return Task.FromResult(count);
    }

    /// <inheritdoc />
    public Task<long> CountAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult((long)_records.Count);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ScoredVector<TKey>>> SearchAsync(
        ReadOnlyMemory<float> queryVector,
        VectorSearchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ValidateDimensions(queryVector);

        options ??= new VectorSearchOptions();
        var topK = options.TopK;
        var minScore = options.MinScore;

        var results = new List<ScoredVector<TKey>>();

        foreach (var (_, record) in _records)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var score = CosineSimilarity(queryVector.Span, record.Vector.Span);

            if (minScore.HasValue && score < minScore.Value)
            {
                continue;
            }

            results.Add(new ScoredVector<TKey>
            {
                Record = record,
                Score = score,
            });
        }

        results.Sort((a, b) => b.Score.CompareTo(a.Score));

        if (results.Count > topK)
        {
            results.RemoveRange(topK, results.Count - topK);
        }

        return Task.FromResult<IReadOnlyList<ScoredVector<TKey>>>(results);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ValidateDimensions(ReadOnlyMemory<float> vector)
    {
        if (vector.Length != Dimensions)
        {
            throw new ArgumentException(
                $"Expected {Dimensions} dimensions, got {vector.Length}.",
                nameof(vector));
        }
    }

    private static float CosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length != b.Length)
        {
            throw new ArgumentException("Vectors must have the same length.");
        }

        float dot = 0;
        float normA = 0;
        float normB = 0;

        for (var i = 0; i < a.Length; i++)
        {
            var ai = a[i];
            var bi = b[i];
            dot += ai * bi;
            normA += ai * ai;
            normB += bi * bi;
        }

        var denominator = MathF.Sqrt(normA) * MathF.Sqrt(normB);
        return denominator < 1e-8f ? 0f : dot / denominator;
    }

    private void EvictOldest()
    {
        var excess = _records.Count - MaxRecordCount;
        if (excess <= 0)
        {
            return;
        }

        var toRemove = _records
            .OrderBy(r => r.Value.CreatedAt)
            .Take(excess)
            .Select(r => r.Key)
            .ToList();

        foreach (var key in toRemove)
        {
            _records.TryRemove(key, out _);
        }
    }
}
