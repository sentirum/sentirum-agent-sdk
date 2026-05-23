# Sentirum.Agent.Memory.Abstractions

Contracts for the Sentirum memory layer. Defines:

- `ISentirumMemoryStore` — scoped key/value memory used by the agent
  runtime and by context providers.
- `MemoryScope` / `MemoryPartition` — addressing model (`Global`,
  `Agent`, `User`, `Session`).
- `MemoryEntry` — record returned by `Get` / `List`.
- `SentirumMemoryStoreExtensions.SetJsonAsync<T>` /
  `GetJsonAsync<T>` — opt-in JSON helpers over the opaque string store.

The store is intentionally serializer-agnostic: callers pick JSON,
MessagePack, or whatever. The opaque-string contract keeps Redis / EF
Core / InMemory back-ends interchangeable without baking a serializer
choice into the wire format.
