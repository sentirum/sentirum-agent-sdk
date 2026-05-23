using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Sentirum.Agent.Memory;
using StackExchange.Redis;

namespace Sentirum.Agent.Memory.Redis;

/// <summary>
/// <see cref="ISentirumMemoryStore"/> implementation backed by Redis.
/// </summary>
/// <remarks>
/// <para>
/// Storage layout:
/// </para>
/// <list type="bullet">
///   <item><description>Each partition maps to a Redis hash:
///     <c>{prefix}{partition-key}</c>. Hash fields are entry keys.</description></item>
///   <item><description>Each entry is stored as a UTF-8 JSON envelope:
///     <c>{"v":"...","ca":epoch,"ua":epoch,"ea":epoch|null}</c>.</description></item>
///   <item><description>Absolute expiration uses Redis key-level TTL.
///     Because per-field TTL is not portable across Redis versions we
///     apply the longest expiration seen on the hash; expired entries are
///     filtered out on read.</description></item>
/// </list>
/// </remarks>
public sealed class RedisMemoryStore : ISentirumMemoryStore
{
    private readonly IConnectionMultiplexer _multiplexer;
    private readonly RedisMemoryStoreOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisMemoryStore"/> class.
    /// </summary>
    public RedisMemoryStore(IConnectionMultiplexer multiplexer, IOptions<RedisMemoryStoreOptions> options)
    {
        ArgumentNullException.ThrowIfNull(multiplexer);
        ArgumentNullException.ThrowIfNull(options);

        _multiplexer = multiplexer;
        _options = options.Value ?? new RedisMemoryStoreOptions();
    }

    private IDatabase GetDb() => _options.DatabaseIndex >= 0
        ? _multiplexer.GetDatabase(_options.DatabaseIndex)
        : _multiplexer.GetDatabase();

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

        var deadline = absoluteExpiration
            ?? (_options.DefaultExpiration is TimeSpan ttl ? DateTimeOffset.UtcNow.Add(ttl) : (DateTimeOffset?)null);

        var now = DateTimeOffset.UtcNow;
        var redisKey = BuildKey(partition);
        var envelope = EnvelopeCodec.Encode(value, createdAt: now, updatedAt: now, expiresAt: deadline);

        var db = GetDb();
        await db.HashSetAsync(redisKey, key, envelope).ConfigureAwait(false);

        if (deadline is DateTimeOffset until)
        {
            // KeyExpireAsync uses the absolute time so concurrent writers
            // converge on a single TTL deadline per hash.
            await db.KeyExpireAsync(redisKey, until.UtcDateTime).ConfigureAwait(false);
        }
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

        var db = GetDb();
        var raw = await db.HashGetAsync(BuildKey(partition), key).ConfigureAwait(false);
        if (!raw.HasValue)
        {
            return null;
        }

        var entry = EnvelopeCodec.Decode(key, raw!);
        if (entry.ExpiresAt is DateTimeOffset deadline && DateTimeOffset.UtcNow >= deadline)
        {
            await db.HashDeleteAsync(BuildKey(partition), key).ConfigureAwait(false);
            return null;
        }

        return entry;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<MemoryEntry> ListAsync(
        MemoryPartition partition,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        partition.Validate();

        var db = GetDb();
        var redisKey = BuildKey(partition);

        // HashScan streams the hash, avoiding HGETALL on large partitions.
        foreach (var pair in db.HashScan(redisKey))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var entry = EnvelopeCodec.Decode(pair.Name!, pair.Value!);
            if (entry.ExpiresAt is DateTimeOffset deadline && DateTimeOffset.UtcNow >= deadline)
            {
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

        return GetDb().HashDeleteAsync(BuildKey(partition), key);
    }

    /// <inheritdoc />
    public async Task<int> ClearAsync(
        MemoryPartition partition,
        CancellationToken cancellationToken = default)
    {
        partition.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        var db = GetDb();
        var redisKey = BuildKey(partition);
        var length = await db.HashLengthAsync(redisKey).ConfigureAwait(false);
        await db.KeyDeleteAsync(redisKey).ConfigureAwait(false);
        return (int)length;
    }

    private RedisKey BuildKey(MemoryPartition partition)
    {
        var suffix = partition.Scope switch
        {
            MemoryScope.Global => "g:",
            MemoryScope.Agent => $"a:{partition.AgentId}",
            MemoryScope.User => $"u:{partition.UserId}",
            MemoryScope.Session => $"s:{partition.SessionId}",
            _ => throw new ArgumentOutOfRangeException(nameof(partition), partition.Scope, "Unknown scope."),
        };

        return $"{_options.KeyPrefix}{suffix}";
    }

    /// <summary>
    /// JSON envelope used to round-trip <see cref="MemoryEntry"/> metadata
    /// through a single Redis hash field. Uses <see cref="JsonSerializer"/>
    /// so payloads containing emoji or other surrogate-pair characters
    /// round-trip safely (a hand-rolled writer is hard to get right and
    /// becomes a data-poison vector if a lone surrogate leaks in —
    /// JsonSerializer rejects lone surrogates at write time, so callers
    /// get a clear exception instead of a write that cannot be read back).
    /// </summary>
    internal static class EnvelopeCodec
    {
        private static readonly JsonSerializerOptions s_envelopeOptions = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        public static string Encode(string value, DateTimeOffset createdAt, DateTimeOffset updatedAt, DateTimeOffset? expiresAt)
        {
            var envelope = new Envelope(
                value,
                createdAt.ToUnixTimeMilliseconds(),
                updatedAt.ToUnixTimeMilliseconds(),
                expiresAt?.ToUnixTimeMilliseconds());

            return JsonSerializer.Serialize(envelope, s_envelopeOptions);
        }

        public static MemoryEntry Decode(string key, string envelope)
        {
            var parsed = JsonSerializer.Deserialize<Envelope>(envelope, s_envelopeOptions)
                ?? throw new InvalidOperationException(
                    $"Failed to decode Redis memory envelope for key '{key}'.");

            return new MemoryEntry(
                key,
                parsed.V ?? string.Empty,
                DateTimeOffset.FromUnixTimeMilliseconds(parsed.Ca),
                DateTimeOffset.FromUnixTimeMilliseconds(parsed.Ua),
                parsed.Ea is long ea ? DateTimeOffset.FromUnixTimeMilliseconds(ea) : null);
        }

        // Camel-case naming via JsonSerializerDefaults.Web keeps the wire
        // format compact ("v"/"ca"/"ua"/"ea") while still letting the type
        // be reflected over.
        private sealed record Envelope(string V, long Ca, long Ua, long? Ea);
    }
}
