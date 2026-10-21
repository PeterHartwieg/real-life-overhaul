# 0007: A read API for other mods

**Status:** Proposed. **Date:** 21 October 2026.

## Context

Other mods read Flare identities, the social cast, casualties and potholes through reflection. Private fields give no promise about meaning, so a consumer has to review and accept each RLO release separately.

## Decision

The mod exposes `RealLifeOverhaul.ModApi.CityApi`, a read-only public contract with its own version, starting at 1. It reports identities, reservations and slowed lanes through optional bridges joined in `Wiring/ModApiLinks.cs`.

## Consequences

- Names, signatures, status values and the meaning of each answer stay fixed while `ContractVersion` is 1. The same account and city seed keep every citizen field. A change to Flare's identity generation needs a new contract version.
- This is a contract for other mods, not a player feature. It has no option, save keys or Guide page, and needs no feature registration under RLO-ARC-006. It starts no feature and changes no game state.
- `ModApi/CityApi.cs` catches read faults and clears all outputs. It owns no game callbacks or borrowed objects, so it does not call the guard to switch another feature off. A Flare fault makes the citizens' read `Failed`. The other reads report the bodies and lanes RLO still holds whatever state their features are in, because a consumer that missed one would take a body or a lane RLO has not given back.
- Immutable citizen objects are prepared through Flare's population bridge when the population changes. `ModApi/ModApiRules.cs` checks live identities against them before any are returned. Calls run only on the main thread and allocate only when the caller's lists grow.
- Going Public returns `Shared` for every read. Nothing is sent over its network. `EconomyCheck/ModApiChecks.cs` pins the reflected contract, a population fingerprint, consistency, reservation matching and allocation cost.
- The [mod authors' reference](../../reference/mod-api.md) describes the exact contract and the checks that still need a game session.

## Rules

[RLO-ARC-002](../architecture.md#rlo-arc-002-rules-classes-are-game-free), [RLO-ARC-005](../architecture.md#rlo-arc-005-features-meet-only-through-bridges), [RLO-ARC-007](../architecture.md#rlo-arc-007-every-entry-point-is-behind-the-guard), [RLO-ARC-009](../architecture.md#rlo-arc-009-fail-safe), [RLO-PERF-003](../performance.md#rlo-perf-003-no-allocation-in-a-frame-method), [RLO-MP-004](../multiplayer.md#rlo-mp-004-same-inputs-same-answer), [RLO-TST-001](../testing.md#rlo-tst-001-game-free-code-is-compiled-into-a-checker).
