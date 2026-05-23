using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Sentirum.Agent.Memory;

/// <summary>
/// Scoped key/value memory used by the Sentirum agent runtime to hold
/// user profiles, agent state, session notes, and other small structured
/// payloads.
/// </summary>
/// <remarks>
/// <para>
/// Implementations are expected to be thread-safe. Operations always run
/// against a single <see cref="MemoryPartition"/> so callers cannot
/// accidentally read across users / sessions / agents.
/// </para>
/// <para>
/// Values are stored as opaque strings; callers serialize their own
/// payloads (typically JSON) so the store implementation stays neutral
/// across providers and the serializer choice belongs to the caller.
/// </para>
/// </remarks>
public interface ISentirumMemoryStore
{
    /// <summary>
    /// Writes (insert-or-update) <paramref name="value"/> at
    /// <paramref name="key"/> inside <paramref name="partition"/>.
    /// </summary>
    /// <param name="partition">Partition identifier (scope + ids).</param>
    /// <param name="key">Key inside the partition.</param>
    /// <param name="value">Opaque value payload.</param>
    /// <param name="absoluteExpiration">Optional expiration time; <see langword="null"/> means "never".</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task SetAsync(
        MemoryPartition partition,
        string key,
        string value,
        DateTimeOffset? absoluteExpiration = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the value at <paramref name="key"/> inside
    /// <paramref name="partition"/>. Returns <see langword="null"/> when the
    /// key does not exist (or has expired).
    /// </summary>
    Task<MemoryEntry?> GetAsync(
        MemoryPartition partition,
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every entry inside <paramref name="partition"/>. Implementations
    /// that cannot enumerate (e.g. a strict K/V cache) throw
    /// <see cref="NotSupportedException"/>.
    /// </summary>
    IAsyncEnumerable<MemoryEntry> ListAsync(
        MemoryPartition partition,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the entry at <paramref name="key"/>. Returns
    /// <see langword="true"/> when an entry was removed.
    /// </summary>
    Task<bool> DeleteAsync(
        MemoryPartition partition,
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears every entry inside <paramref name="partition"/>. Returns the
    /// number of entries that were removed.
    /// </summary>
    Task<int> ClearAsync(
        MemoryPartition partition,
        CancellationToken cancellationToken = default);
}
