# ADR-0010: Redis hash-per-partition with hash-level TTL

- **Status:** Accepted (revisit when per-entry TTL demand appears)
- **Date:** 2026-05-23
- **Applies to:** `Sentirum.Agent.Memory.Redis` 0.1.0-preview onward

## Context

`RedisMemoryStore` stores each `MemoryPartition` as a single Redis
hash. Entry keys become hash fields; the value is a JSON envelope
carrying the payload plus `CreatedAt` / `UpdatedAt` / `ExpiresAt`
metadata. Absolute expiration uses `KeyExpireAsync` at the hash level
(`HEXPIRE` is only available in Redis 7.4+ and is not exposed by
StackExchange.Redis 2.8.x).

This means **only one TTL deadline applies to the entire hash**: the
most recent `SetAsync` call that included an `absoluteExpiration` wins.
If two entries in the same partition have different expirations the
shorter one effectively replaces the longer one for the whole hash.

The M4 reviewer (`zai-anthropic/glm-5`) flagged this as a semantic
trap worth recording before customers depend on per-entry TTL.

## Decision

1. **Storage layout stays hash-per-partition.** Most M4 scenarios
   (customer profiles, session notes, agent state) share an expiration
   policy across the entire partition, so the hash layout is the
   right default.
2. **TTL is hash-level.** `KeyExpireAsync(absolute deadline)` runs on
   every `SetAsync` with an explicit `absoluteExpiration`. The
   metadata envelope still carries the per-entry `ExpiresAt` so the
   read path filters expired entries even when the hash key itself
   has not yet been evicted.
3. **`RedisMemoryStoreOptions.DefaultExpiration`** applies the same
   policy uniformly when the caller does not supply one. Callers who
   need divergent expirations per entry should partition more
   aggressively (one partition per logical record) or wait for a
   future `RedisMemoryStoreV2` that lands on per-field TTL.

## Consequences

- Predictable footprint: each partition is a single Redis key, easy
  to inspect (`HGETALL`) and to evict (`DEL`).
- `ClearAsync` is a single `KeyDeleteAsync` call (fast, atomic).
- The trade-off is documented on `RedisMemoryStore.SetAsync` XML and
  in the package README.

## Alternatives considered

- **One Redis key per entry** (`{prefix}{partition-key}:{entry-key}`).
  Gives true per-entry TTL but turns `ListAsync` into a `SCAN`
  operation, which is more expensive and harder to bound. Revisit
  when a customer hits the hash-level TTL trap in production.
- **Wait for Redis 7.4 `HEXPIRE`.** Per-field TTL would let us keep
  the hash layout *and* per-entry expiration. Blocked on
  StackExchange.Redis exposing the command; add when the dependency
  catches up.
- **Run a background sweeper that re-encodes the hash without
  expired fields.** Adds operational complexity (a hosted service in
  every Sentirum host) for marginal benefit. Skip.
