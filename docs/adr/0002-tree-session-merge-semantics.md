# ADR-0002: Tree-session merge semantics

- **Status:** Accepted
- **Date:** 2026-05-23
- **Applies to:** `Sentirum.Agent.Sessions.Tree` 0.1.0-preview onward

## Context

Sentirum's headline differentiator is *tree-based sessions*: a single
conversation can fork into N parallel branches (refund vs.
replacement vs. discount in the Customer Support vertical, A/B
prompt exploration, etc.) and later one branch can be folded back
into its trunk.

`ForkAsync` is well-defined — it round-trips through
`AIAgent.SerializeSessionAsync` / `DeserializeSessionAsync` to get a
true deep copy. `MergeAsync` is the harder operation. The M3 review
(`gpt-5.5`) flagged five concrete merge bugs:

1. Wrong-direction merges (`MergeAsync(root, fork)`) duplicated
   root's entire history into the fork.
2. Unrelated sessions could be merged.
3. Messages were copied by reference, so post-merge mutation on the
   source leaked into the target.
4. `ReferenceEquals`-based common-prefix detection failed after a
   session round-tripped through serialization.
5. Concurrent forks of the same parent corrupted the children list.

## Decision

`InMemoryTreeSessionStore.MergeAsync` adopts the following contract:

1. **Direction is fixed.** `target` must be an ancestor of `source`
   on the recorded tree. Conceptually: a fork is *always* folded
   back into its trunk. Merging an ancestor into a descendant
   throws `InvalidOperationException` with a message that names the
   ancestor relation.
2. **Unrelated sessions throw.** Sessions that share an agent but
   have no ancestor relationship cannot be merged. (Cross-tree
   transplants are not supported in v0.1 — revisit when a real
   use-case appears.)
3. **The divergence index is recorded at fork time** as
   `SentirumSession.ForkPointMessageCount`, not derived from
   identity comparison of `ChatMessage` references. This survives
   serialize / deserialize round-trips.
4. **Messages are deep-cloned** when transferred. `CloneMessage`
   rebuilds a `ChatMessage` with a fresh `Contents` list and a
   copied `AdditionalPropertiesDictionary`. Post-merge mutation on
   either branch cannot cross.
5. **Concurrent forks of the same parent are safe.**
   `_childrenByParent` stores `ImmutableArray<string>` behind
   `ConcurrentDictionary.AddOrUpdate`, so parallel `ForkAsync` calls
   never observe a partially-mutated list.
6. **`lastMessageCount` is preserved** as an opt-in trim — when
   supplied, only the last N divergent messages are replayed onto
   the target. Useful for partial merges (HITL approval of the final
   answer only).

Out of scope for v0.1:

- Three-way merges across siblings.
- Conflict resolution for tool-call turns where source and target
  both made tool calls after the fork point. We currently always
  replay source onto target *as if target had not moved*. If target
  diverged after the fork, both branches' messages end up
  side-by-side. Document this and revisit with a real conflict case.

## Consequences

- Merge becomes a *safe* operation: no silent history duplication,
  no cross-branch leaks, no surprising direction.
- Distributed implementations (Redis, EF Core) in M4+ must persist
  `ForkPointMessageCount` and the parent chain so they can enforce
  the same contract.
- Cross-tree merge is now explicitly unsupported; document the
  escape hatch (export source messages, append to target manually).

## Alternatives considered

- **Structural message equality** as a fallback to `ReferenceEquals`:
  rejected because tool-call messages with identical text but
  different IDs would falsely match.
- **Allow any direction; rely on user to know what they're doing.**
  Reviewer pushed back hard — the failure mode (silent duplication)
  is too easy to hit and too hard to debug.
