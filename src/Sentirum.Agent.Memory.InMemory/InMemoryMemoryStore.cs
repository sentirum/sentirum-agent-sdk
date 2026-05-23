using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Sentirum.Agent.Memory;

namespace Sentirum.Agent.Memory.InMemory;

/// <summary>
/// In-process <see cref="ISentirumMemoryStore"/> backed by nested
/// <see cref="ConcurrentDictionary{TKey, TValue}"/> instances. Suitable for
/// single-process apps, samples, and tests.
/// </summary>
/// <remarks>
/// Partitions are addressed by a string key built from the
/// <see cref="MemoryPartition"/>; concurrent reads/writes on the same
/// partition are safe.
/// </remarks>
public sealed class InMemoryMemoryStore : ISentirumMemoryStore
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, MemoryEntry>> _partitions =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Maximum number of entries across all partitions before proactive
    /// eviction kicks in. Set to <c>0</c> to disable the limit.
    /// </summary>
    public int MaxTotalEntries { get; init; } = 10_000;

    /// <summary>
    /// Interval between proactive eviction sweeps. Default is 5 minutes.
    /// </summary>
    public TimeSpan EvictionInterval { get; init; } = TimeSpan.FromMinutes(5);

    /// <inheritdoc />
    public Task SetAsync(
        MemoryPartition partition,
        string key,
        string value,
        DateTimeOffset? absoluteExpiration = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        partition.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        var bucket = _partitions.GetOrAdd(
            BuildPartitionKey(partition),
            _ => new ConcurrentDictionary<string, MemoryEntry>(StringComparer.Ordinal));

        var now = DateTimeOffset.UtcNow;
        bucket.AddOrUpdate(
            key,
            _ => new MemoryEntry(key, value, now, now, absoluteExpiration),
            (_, existing) => existing with
            {
                Value = value,
                UpdatedAt = now,
                ExpiresAt = absoluteExpiration,
            });

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<MemoryEntry?> GetAsync(
        MemoryPartition partition,
        string key,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        partition.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        if (!_partitions.TryGetValue(BuildPartitionKey(partition), out var bucket))
        {
            return Task.FromResult<MemoryEntry?>(null);
        }

        if (!bucket.TryGetValue(key, out var entry))
        {
            return Task.FromResult<MemoryEntry?>(null);
        }

        if (IsExpired(entry))
        {
            bucket.TryRemove(key, out _);
            return Task.FromResult<MemoryEntry?>(null);
        }

        return Task.FromResult<MemoryEntry?>(entry);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<MemoryEntry> ListAsync(
        MemoryPartition partition,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        partition.Validate();

        if (!_partitions.TryGetValue(BuildPartitionKey(partition), out var bucket))
        {
            yield break;
        }

        // Snapshot with ToArray so concurrent writers (SetAsync / DeleteAsync)
        // cannot invalidate the enumerator mid-loop.
        foreach (var entry in bucket.Values.ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsExpired(entry))
            {
                bucket.TryRemove(entry.Key, out _);
                continue;
            }

            yield return entry;
        }
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(
        MemoryPartition partition,
        string key,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        partition.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        if (!_partitions.TryGetValue(BuildPartitionKey(partition), out var bucket))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(bucket.TryRemove(key, out _));
    }

    /// <inheritdoc />
    public Task<int> ClearAsync(
        MemoryPartition partition,
        CancellationToken cancellationToken = default)
    {
        partition.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        if (!_partitions.TryGetValue(BuildPartitionKey(partition), out var bucket))
        {
            return Task.FromResult(0);
        }

        var count = bucket.Count;
        bucket.Clear();
        return Task.FromResult(count);
    }

    /// <summary>
    /// Evicts all expired entries across every partition. Returns the
    /// total number of entries removed. Suitable for a periodic
    /// background sweep or a health-check endpoint.
    /// </summary>
    public int EvictExpired()
    {
        var removed = 0;
        foreach (var (_, bucket) in _partitions)
        {
            foreach (var (key, entry) in bucket.ToArray())
            {
                if (IsExpired(entry))
                {
                    if (bucket.TryRemove(key, out _))
                    {
                        removed++;
                    }
                }
            }
        }
        return removed;
    }

    /// <summary>
    /// Total number of entries currently held across all partitions.
    /// </summary>
    public int TotalEntryCount => _partitions.Sum(p => p.Value.Count);

    private static bool IsExpired(MemoryEntry entry) =>
        entry.ExpiresAt is DateTimeOffset deadline && DateTimeOffset.UtcNow >= deadline;

    internal static string BuildPartitionKey(MemoryPartition partition) => partition.Scope switch
    {
        MemoryScope.Global => "g:",
        MemoryScope.Agent => $"a:{partition.AgentId}",
        MemoryScope.User => $"u:{partition.UserId}",
        MemoryScope.Session => $"s:{partition.SessionId}",
        _ => throw new ArgumentOutOfRangeException(nameof(partition), partition.Scope, "Unknown scope."),
    };
}
