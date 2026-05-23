using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Sentirum.Agent.Memory;

/// <summary>
/// JSON convenience over <see cref="ISentirumMemoryStore"/>. The store itself
/// is serializer-agnostic; these helpers default to
/// <see cref="System.Text.Json"/> with permissive options so callers can hold
/// strongly typed payloads without writing boilerplate.
/// </summary>
public static class SentirumMemoryStoreExtensions
{
    private static readonly JsonSerializerOptions DefaultOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    /// <summary>
    /// Serializes <paramref name="value"/> to JSON and writes it to the
    /// store. See <see cref="ISentirumMemoryStore.SetAsync"/>.
    /// </summary>
    public static Task SetJsonAsync<T>(
        this ISentirumMemoryStore store,
        MemoryPartition partition,
        string key,
        T value,
        JsonSerializerOptions? options = null,
        DateTimeOffset? absoluteExpiration = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        var json = JsonSerializer.Serialize(value, options ?? DefaultOptions);
        return store.SetAsync(partition, key, json, absoluteExpiration, cancellationToken);
    }

    /// <summary>
    /// Reads and JSON-deserializes the entry at <paramref name="key"/>.
    /// Returns <see langword="default"/> when the entry is missing.
    /// </summary>
    public static async Task<T?> GetJsonAsync<T>(
        this ISentirumMemoryStore store,
        MemoryPartition partition,
        string key,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        var entry = await store.GetAsync(partition, key, cancellationToken).ConfigureAwait(false);
        if (entry is null)
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(entry.Value.Value, options ?? DefaultOptions);
    }
}
