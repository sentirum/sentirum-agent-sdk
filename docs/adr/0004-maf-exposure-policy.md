# ADR-0004: Microsoft Agent Framework exposure policy

- **Status:** Accepted
- **Date:** 2026-05-23
- **Applies to:** Sentirum 0.1.0-preview onward

## Context

Sentirum is built on top of Microsoft Agent Framework
(`Microsoft.Agents.AI` 1.6.2) and `Microsoft.Extensions.AI` 10.5.1.
The SDK exposes a few MAF types directly on the public surface:

- `ISentirumAgent.InnerAgent : AIAgent`
- `ISentirumSession.InnerSession : AgentSession?`
- Builder extensions accept `Microsoft.Extensions.AI.AIFunction`,
  `IChatClient`, `ChatClientBuilder`.

The M3 reviewer flagged this as a long-term risk: if MAF changes
session serialization, message shape, or function metadata, every
Sentirum consumer that touched the inner types breaks.

## Decision

1. **Exposing `AIAgent` and `AgentSession` is intentional and
   accepted as a v1.0 contract.** They are escape hatches for power
   users (workflow registration, custom middleware, telemetry).
   Removing them would force us to mirror MAF surface area, which
   defeats the point of building on MAF.
2. **MAF compatibility is pinned per Sentirum minor version.**
   `Directory.Packages.props` declares the exact MAF / MEAI
   versions. A MAF major bump becomes a Sentirum minor bump (with
   migration notes); a MAF minor bump is rolled into the next
   Sentirum patch only after the test suite passes.
3. **Vertical packages (Customer Support, etc.) must not require
   users to touch MAF.** Every common scenario expressible in M5-M8
   has a Sentirum-native API; MAF types remain reachable but
   optional.
4. **Documentation marks `InnerAgent` and `InnerSession` as
   "advanced".** README and the abstractions package's `<remarks>`
   call out the compatibility caveat.

## Consequences

- We get MAF's session serialization, function-invocation
  middleware, telemetry hooks, and future workflow features for
  free. Sentirum stays small.
- Users who touch `InnerAgent` accept the MAF compatibility
  contract.
- The CI matrix runs against the pinned MAF version; we cannot test
  arbitrary downstream MAF versions until MAF stabilizes.

## Alternatives considered

- **Hide MAF entirely behind Sentirum types.** Doubles the public
  surface, slows iteration, and most likely leaks anyway (e.g.
  `ChatMessage` is already MEAI's type).
- **Re-export MAF types under `Sentirum.*` aliases.** Compile-time
  cosmetic only; doesn't change the binding risk.
