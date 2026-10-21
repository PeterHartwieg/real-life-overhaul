# City reads for other mods

`RealLifeOverhaul.ModApi.CityApi` is a public static type in `RealLifeOverhaul.dll`. Its public static `int ContractVersion` getter returns `1`, independently of RLO's release number. A consumer may bind it by reflection without referencing RLO at build time. Check the contract version and every member you use once, then keep typed delegates for repeated reads.

Call on the game's main thread. Reuse your own lists, with capacity for 400 citizens. Every method clears all its lists before reading. Only `Ready` gives an answer; every other result leaves every list empty, and `ReadCitizens` sets `citySeed` to zero. Null lists or two outputs using the same list return `Failed`. Calls never throw, start a feature or change RLO or the game. They allocate nothing beyond growing your lists. Returned citizen objects are immutable and may be kept; body and waypoint objects belong to the game and are valid only while the game keeps them.

## Methods

All four methods are public and static and return `RealLifeOverhaul.ModApi.CityApiStatus`:

```csharp
CityApiStatus ReadCitizens(List<CityApiCitizen> into, out int citySeed);
CityApiStatus ReadReservedCitizens(List<int> indexes);
CityApiStatus ReadReservedBodies(List<object> pedestrians, List<object> characters);
CityApiStatus ReadSlowedLanes(List<object> lanes, List<int> originalLimits, List<int> currentLimits);
```

`ReadCitizens` reads Flare's city population for the current account. Indexes are unique and run from zero to the count minus one; Flare's account key is `"c" + Index`. It checks the live citizen against the prepared identity and avatar before returning anyone. An age or sex disagreement returns `Refreshing`.

`ReadReservedCitizens` reports the indexes represented by live social cast slots, including a person seated in a car. A match requires both `First + " " + Last` and the appearance seed. Age is not part of the match because the street clamps a contact's age to 18 through 85. Indexes are returned once each.

`ReadReservedBodies` reports the game's `Streets.Pedestrians.Pedestrian` objects held by casualties in `pedestrians`. `characters` contains each social cast body's `CityPerson.Character`, whether out or shelved, and each active ragdoll's `Ragdoll.Character`, including bodies resting without physics. Bodies are listed while RLO has them, also when their feature's option has just been switched off. Each object appears once in its list. These are object references, not ids or copied bodies.

`ReadSlowedLanes` fills three parallel lists. At each index, `lanes` holds a game `GleyTrafficSystem.Waypoint`, `originalLimits` holds its limit before RLO took it, and `currentLimits` holds its current `maxSpeed`. The read uses RLO's kept pothole entries and does not work the speed rule out again. List order is not a contract.

## Citizen fields

`RealLifeOverhaul.ModApi.CityApiCitizen` is a public sealed class with these public readonly instance fields:

| Field | Type | Meaning |
| --- | --- | --- |
| `Index` | `int` | Unique index from zero to the count minus one |
| `First` | `string` | Non-empty first name |
| `Last` | `string` | Non-empty surname |
| `District` | `string` | Game district id, or null if unknown |
| `Age` | `int` | Age in years, 1 through 130 |
| `Female` | `bool` | The citizen's and avatar's sex |
| `Persona` | `string` | `Commuter`, `Foodie`, `Parent`, `Student`, `Retiree`, `Sporty`, `Grumpy` or `NightOwl`, in that order |
| `AppearanceSeed` | `int` | Nonzero seed used to draw the citizen's face and body |

## Status values

`RealLifeOverhaul.ModApi.CityApiStatus` is a public enum with `int` storage:

| Name | Value | Meaning |
| --- | --- | --- |
| `Ready` | 0 | The answer is complete, possibly empty |
| `Off` | 1 | Flare is switched off or not running |
| `NotYet` | 2 | No city, account, seed or population yet; ask later |
| `Refreshing` | 3 | Loading or between consistent states; ask next frame |
| `Shared` | 4 | Going Public is in session; this contract does not expose the shared city |
| `Failed` | 5 | A source has faulted or the read could not be completed |

`ReadCitizens` returns `Off` while Flare is switched off or not running, `NotYet` before a city, account, seed or population exists, and `Refreshing` while a city loads or the population is between consistent states.

The other three report what RLO holds right now, whatever its options say and whether or not a city is loaded. They return `Ready` (possibly empty), `Shared` or `Failed`, and `ReadReservedCitizens` may also return `Refreshing` when a cast slot names a person the social life no longer has. A consumer should treat any other answer from them as unknown and hold back, never as empty. Going Public takes precedence over everything else for valid calls.

## Compatibility and verification

Names, signatures, fields, status values and their meanings stay fixed while the contract is 1. The same account and city seed keep the same citizen fields across RLO releases with contract 1. RLO may add members, but no status values. Removing or changing a promised member or its meaning requires contract 2. Use only the members documented for the version you accept.

`EconomyCheck/ModApiChecks.cs` checks the compiled shape, all 400 citizens against Flare, a fixed population fingerprint, disagreements, list clearing, name and face matching, determinism, faults and allocation cost. Game checks still needed: an online contact appearing in the social cast, a casualty and ragdoll held through recovery, a pothole mended, a load and unload, feature options switched off, and Going Public joined and left. See [ADR 0007](../standards/decisions/0007-a-read-api-for-other-mods.md) for the design choice.
