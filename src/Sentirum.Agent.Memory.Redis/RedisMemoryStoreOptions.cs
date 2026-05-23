using System;

namespace Sentirum.Agent.Memory.Redis;

/// <summary>
/// Options for the Redis-backed Sentirum memory store.
/// </summary>
public sealed class RedisMemoryStoreOptions
{
    /// <summary>
    /// Optional global key prefix applied to every Redis key. Useful when
    /// multiple services share a single Redis instance.
    /// </summary>
    public string KeyPrefix { get; set; } = "sentirum:mem:";

    /// <summary>
    /// Redis database index. Defaults to -1 (use the default database from
    /// the connection multiplexer).
    /// </summary>
    public int DatabaseIndex { get; set; } = -1;

    /// <summary>
    /// Default absolute expiration applied when the caller does not supply
    /// one. <see langword="null"/> means "no default" (entries live forever
    /// unless the caller passes a deadline).
    /// </summary>
    public TimeSpan? DefaultExpiration { get; set; }
}
