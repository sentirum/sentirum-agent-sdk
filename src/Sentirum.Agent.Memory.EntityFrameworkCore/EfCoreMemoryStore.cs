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

        var existing = await FindAsync(partition, key, cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;

        if (existing is null)
        {
            Set.Add(new SentirumMemoryRecord
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
            });
        }
        else
        {
            existing.Value = value;
            existing.UpdatedAt = now;
            existing.ExpiresAt = absoluteExpiration;
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<MemoryEntry?> GetAsync(
        MemoryPartition partition,
        string key,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        partition.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        var row = await FindAsync(partition, key, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            return null;
        }

        if (IsExpired(row))
        {
            Set.Remove(row);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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

        var row = await FindAsync(partition, key, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            return false;
        }

        Set.Remove(row);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public async Task<int> ClearAsync(
        MemoryPartition partition,
        CancellationToken cancellationToken = default)
    {
        partition.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        var rows = await ApplyPartition(Set, partition)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (rows.Count == 0)
        {
            return 0;
        }

        Set.RemoveRange(rows);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return rows.Count;
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
