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

        foreach (var entry in bucket.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsExpired(entry))
            {
                bucket.TryRemove(entry.Key, out _);
                continue;
            }

            yield return entry;
            await Task.Yield();
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
