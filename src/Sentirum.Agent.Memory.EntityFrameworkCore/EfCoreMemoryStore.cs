using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sentirum.Agent.Memory;

namespace Sentirum.Agent.Memory.EntityFrameworkCore;

/// <summary>
/// EF Core <see cref="ISentirumMemoryStore"/> implementation. Generic over
/// the user's <typeparamref name="TContext"/> so it can plug into any
/// existing application context that contains a
/// <c>DbSet&lt;SentirumMemoryRecord&gt;</c>.
/// </summary>
/// <typeparam name="TContext">Concrete <see cref="DbContext"/> type.</typeparam>
/// <remarks>
/// <para>
/// Concurrency model: each operation resolves the <see cref="DbContext"/>
/// from DI (typically scoped) and applies a single SaveChanges per call.
/// For high-throughput workloads pair the registration with a pooled
/// DbContext factory.
/// </para>
/// <para>
/// Expired rows are filtered out on read and lazily deleted on the next
/// write to the same key. A background sweeper job is out of scope for
/// v0.1; see ADR-0007.
/// </para>
/// </remarks>
public sealed class EfCoreMemoryStore<TContext> : ISentirumMemoryStore
    where TContext : DbContext
{
    private readonly TContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="EfCoreMemoryStore{TContext}"/> class.
    /// </summary>
    public EfCoreMemoryStore(TContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    private DbSet<SentirumMemoryRecord> Set => _context.Set<SentirumMemoryRecord>();

    /// <inheritdoc />
    /// <remarks>
    /// Implemented as an atomic upsert via
    /// <c>ExecuteUpdateAsync</c> + conditional <c>Add</c>: we try to update
    /// first and only fall back to insert when no row exists. Two concurrent
    /// writers therefore converge on a single row instead of racing on the
    /// unique index and surfacing <c>DbUpdateException</c>.
    /// </remarks>
    public async Task SetAsync(
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

        var now = DateTimeOffset.UtcNow;

        // Step 1: attempt the update path. Set-based ExecuteUpdateAsync
        // bypasses the change tracker and runs as a single round-trip.
        var updated = await ApplyPartition(Set, partition)
            .Where(r => r.Key == key)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(r => r.Value, value)
                    .SetProperty(r => r.UpdatedAt, now)
                    .SetProperty(r => r.ExpiresAt, absoluteExpiration),
                cancellationToken)
            .ConfigureAwait(false);

        if (updated > 0)
        {
            return;
        }

        // Step 2: insert path. We translate the unique-index violation that
        // can happen when two callers reach this branch concurrently into
        // an updated path so the caller observes upsert semantics.
        var record = new SentirumMemoryRecord
        {
            Scope = (int)partition.Scope,
            AgentId = partition.AgentId,
            UserId = partition.UserId,
            SessionId = partition.SessionId,
            Key = key,
            Value = value,
            CreatedAt = now,
            UpdatedAt = now,
            ExpiresAt = absoluteExpiration,
        };
        var addedEntry = Set.Add(record);

        try
        {
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // A concurrent insert won the race; detach only the entity
            // we just added so the caller's own tracked instances are
            // not affected.
            addedEntry.State = EntityState.Detached;

            await ApplyPartition(Set, partition)
                .Where(r => r.Key == key)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(r => r.Value, value)
                        .SetProperty(r => r.UpdatedAt, now)
                        .SetProperty(r => r.ExpiresAt, absoluteExpiration),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Reads are side-effect free: expired rows are filtered out but not
    /// deleted. Cleanup belongs to a separate sweeper job (out of scope
    /// for v0.1; tracked in ADR-0007). This keeps <c>GetAsync</c> safe to
    /// compose inside an ambient transaction.
    /// </remarks>
    public async Task<MemoryEntry?> GetAsync(
        MemoryPartition partition,
        string key,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        partition.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        // AsNoTracking so the read does not pollute the DbContext's change
        // tracker; matches the ListAsync path.
        var row = await ApplyPartition(Set.AsNoTracking(), partition)
            .FirstOrDefaultAsync(r => r.Key == key, cancellationToken)
            .ConfigureAwait(false);

        if (row is null || IsExpired(row))
        {
            return null;
        }

        return ToEntry(row);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<MemoryEntry> ListAsync(
        MemoryPartition partition,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        partition.Validate();

        var query = ApplyPartition(Set.AsNoTracking(), partition);

        await foreach (var row in query.AsAsyncEnumerable().WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (IsExpired(row))
            {
                continue;
            }

            yield return ToEntry(row);
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(
        MemoryPartition partition,
        string key,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        partition.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        var deleted = await ApplyPartition(Set, partition)
            .Where(r => r.Key == key)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        return deleted > 0;
    }

    /// <inheritdoc />
    public async Task<int> ClearAsync(
        MemoryPartition partition,
        CancellationToken cancellationToken = default)
    {
        partition.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        return await ApplyPartition(Set, partition)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<SentirumMemoryRecord?> FindAsync(
        MemoryPartition partition,
        string key,
        CancellationToken cancellationToken)
    {
        return await ApplyPartition(Set, partition)
            .FirstOrDefaultAsync(r => r.Key == key, cancellationToken)
            .ConfigureAwait(false);
    }

    private static IQueryable<SentirumMemoryRecord> ApplyPartition(
        IQueryable<SentirumMemoryRecord> source,
        MemoryPartition partition)
    {
        var scope = (int)partition.Scope;
        return source.Where(r =>
            r.Scope == scope
            && r.AgentId == partition.AgentId
            && r.UserId == partition.UserId
            && r.SessionId == partition.SessionId);
    }

    private static bool IsExpired(SentirumMemoryRecord row) =>
        row.ExpiresAt is DateTimeOffset deadline && DateTimeOffset.UtcNow >= deadline;

    private static MemoryEntry ToEntry(SentirumMemoryRecord row) =>
        new(row.Key, row.Value, row.CreatedAt, row.UpdatedAt, row.ExpiresAt);
}
