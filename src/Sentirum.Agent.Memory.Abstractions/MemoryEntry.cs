using System;

namespace Sentirum.Agent.Memory;

/// <summary>
/// A single memory entry returned by an <see cref="ISentirumMemoryStore"/>.
/// </summary>
/// <param name="Key">The key inside the partition. Unique per partition.</param>
/// <param name="Value">The stored payload. Implementations choose the serialization format.</param>
/// <param name="CreatedAt">UTC timestamp when the entry was first written.</param>
/// <param name="UpdatedAt">UTC timestamp of the most recent write.</param>
/// <param name="ExpiresAt">Optional UTC expiration; entries past this point are not returned.</param>
public readonly record struct MemoryEntry(
    string Key,
    string Value,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ExpiresAt = null);
