# Walkable buildings assessment

Assessed on 23 October 2026 against upstream `main`, commit `850e2781b644cee7edea2bd97b11357c8d36bbd4`, RLO 0.50.0. This is a source-based proposal, not an implemented feature or an in-game verification.

Hotel follow-up: the [walkable hotels implementation plan](plans/walkable-hotels.md), agreed after independent Opus review, supersedes this assessment's provisional hotel sequencing and save-state suggestions.

The intended experience is the vanilla one: approach a building, enter through its door with the normal transition, walk around its interior, use objects and speak to people, then leave through the exit. The later clarification settles the scope; continuous movement between the street and indoor geometry is outside it.

**Recommendation:** extend RLO's existing native-interior approach. Keep the game's building loader, interior shell, navigation, camera and activity panels. Add venue-specific layouts, access rules, functional objects and occupants where they are missing. Prove one additional venue before extracting shared lifecycle code from the existing implementations.

## What already exists

These are capabilities visible in the current source, not a claim that every path works in a running game.

| Place | Current implementation | Work needed |
| --- | --- | --- |
| The player's houses and flats | Vanilla residential interiors. Home adds cooking and routines; Family and Roommates place people inside and let them walk and sit. | Preserve the player's furniture and ownership. Extend activities or occupants as needed; the basic walkable room already exists. |
| Hudson City University | `CampusInterior` enters through `BuildingManager.EnterBuilding`, temporarily loads the vanilla D1 school shell, then places the `GarmentDistrictSchool` layout. It has five classroom doors per logical floor, floor selection and course interactions. | First verify normal door entry, floor changes, desks and load recovery. Improve layouts and campus life after that. This is an existing implementation to extend. |
| Professional workplaces | `CareersDirector.Desk` chooses a reachable vanilla employee station, uses a desk supplied by an RLO floor, or fits a job-specific desk area in the available space. `DeskWork` provides the activity. | Check coverage by employer and profession, entry permissions and reliable setup. Add missing reception, interview and colleague interactions. |
| Tech headquarters and data centres | `TowerFloors`, `TechSites` and `ServerRoomView` supply logical floors and furnished rooms through `FitView`. | Reuse their floor selection and furnishing patterns. Keep each company's identity distinct even when its address is shared. |
| Six employers behind help-wanted boards | Careers identifies employers at the boards; the boss and some shift interactions are outside. | A board does not establish an indoor office. Resolve a suitable host and add an indoor reception or work area where appropriate. Driving and street jobs should still take place outside. |
| Job interviews | `InterviewVisit` walks to the entrance, fades the screen and runs the interview without loading the employer's interior. | Add a real room, interviewer and reachable chair; retain the interview rules and settlement. |
| Hotels and the travel cottage | `HotelStay.EnterRoom` sets `TravelState.InRoom`, secludes the player and opens a list. It does not load a room. | A genuine new interior path, including bed, bathroom, check-in and checkout behaviour. |
| Friends' and relatives' homes | The inspected Home/Family/Roommates paths centre on the player's home. Social documents outings at the street and door. I did not find a general NPC-home visit implementation in those paths. | Establish a stable person-to-home mapping, invitation/access rules and a furnished interior. Confirm the intended residents before expanding this feature. |
| Other RLO civic buildings | Police stations have `StationInterior`; the courthouse is handled by `FitOutDirector`. The latter's generic dispatcher currently handles the courthouse, not every named RLO building. | Audit each remaining service individually. A map marker or building claim is not proof of a playable interior. |

Evidence: [Campus entry](../Assets/Mods/RealLifeOverhaul/Scripts/Careers/CampusInterior.Entering.cs), [campus furnishing](../Assets/Mods/RealLifeOverhaul/Scripts/Careers/CampusInterior.Floor.cs), [campus rules](../Assets/Mods/RealLifeOverhaul/Scripts/Careers/Study/CampusRules.cs), [work desks](../Assets/Mods/RealLifeOverhaul/Scripts/Careers/CareersDirector.Desk.cs), [careers behaviour](features/careers.md), [hotel room entry](../Assets/Mods/RealLifeOverhaul/Scripts/Travel/HotelStay.cs), [social outings](features/social.md), [roommate movement](../Assets/Mods/RealLifeOverhaul/Scripts/Home/Roommates/RoommateFlat.cs), [civic fit-out selection](../Assets/Mods/RealLifeOverhaul/Scripts/FitOut/FitOutDirector.cs).

## Implementation choices

| Approach | Assessment |
| --- | --- |
| Use a venue's existing vanilla interior and items | First choice whenever it already has suitable rooms. Lowest integration work; preserves native activities, exits and saved furniture. |
| Furnish a compatible vanilla shell at runtime | Recommended for additional RLO venues. Existing campus and fit-out code demonstrate two useful variants. Requires access, item lifecycle and navigation validation. |
| Author entirely new interior geometry and navigation | Reserve for rooms that existing shells cannot express. Requires proving the game's loader integration, supported agent types, camera, lighting, exits and asset pipeline. The presence of Unity's navigation package does not itself provide an RLO interior-registration API. |
| Build interiors in the outdoor world | Outside the clarified scope; unnecessary for vanilla-style building entry. |

The `Scripts/Interiors` feature concerns decor scores and business effects. It is not the room loader. The relevant reusable pieces are `CampusInterior`, `FitOut`, `RoomMeasure`, `GameFurniture`, `FitReach`, `FitDesks`, `DeskWork` and the venue-specific directors.

## How to extend it

### 1. Resolve the venue separately from the room

Give an added venue a stable identity: feature, address, optional floor/unit, layout revision and seed. Its owner decides opening hours, invitations, booking or employment requirements. Reuse `SpecialBuildings`, `CityReserves` and `MapPlaces` for claims and discoverability where applicable.

Use the existing interior unchanged when the host is a furnished business or the player's home. A private residence needs a scoped visit permission path: simply removing the rent restriction globally would change every residential door. A workplace may already belong to a business or another player, so a board nearby is insufficient authority to replace its contents.

The university already has a permanent-address and fallback policy, including fallback to the vanilla school when its own building cannot be found. Preserve that policy and its existing course records. If the university appears to be only a sign in play, inspect those conditions and the school-layout availability check before building another university system.

### 2. Keep one owner for each interior visit

For the first new venue, implement a small adapter in its feature using the existing entry/exit events and game methods. It should own this lifecycle:

```text
At door -> validate access and assets -> native entry
        -> prepare layout -> wait for navigation -> free movement
        -> object/person interactions -> native exit -> release resources
```

Cancellation, failure, load, scene unload and switching the feature off must reach the same cleanup. Use a visit generation so work started for an earlier visit cannot populate a later room. Record the exterior door for recovery and let the game handle its normal transition and camera.

After the second venue demonstrates the same needs, extract the common part into a small shared interior-session helper. Keep booking, study, work and relationship logic in their respective features, connected through bridges and `Scripts/Wiring`. Avoid moving all existing interiors into a new framework in the first change.

The campus's temporary `BuildingSize`/`BuildingVersion` override and shadow registration are useful precedents, but depend on game behaviour. Prefer a host's compatible native shell; use an override only where required, with restoration and load tests. The current shadow registration shares some references with the real registration, so it must not be assumed to be an isolated deep copy for other kinds of venue.

### 3. Distinguish scenery from functional furniture

This is the most important implementation distinction:

- `GameFurniture` deliberately strips most scripts, colliders, animators and other behaviour from copies. `FitView` supplies navigation obstacles and specific desk/seat behaviour around those copies. A bed or cooker made this way is not automatically a functioning vanilla bed or cooker.
- The campus has a different path: it creates actual game `ItemController` objects through its layout placement code and uses a temporary registration. That is a better starting point for objects that must expose native activities, subject to confirming their save and ownership behaviour.
- Existing player furniture should remain real, saved vanilla items. Added venue scenery can be ephemeral; functional venue fixtures need either audited native items attached to an isolated visit or explicit adapters to the game's activities.

For a first workplace, use the existing desk adapter and one reception/interview interaction. For a house or hotel room, explicitly implement and test sleeping, washing, sitting and leaving. Decoration alone does not satisfy those requirements.

Source: [GameFurniture](../Assets/Mods/RealLifeOverhaul/Scripts/Core/GameFurniture.cs), [FitView](../Assets/Mods/RealLifeOverhaul/Scripts/FitOut/FitView.cs), [campus item placement](../Assets/Mods/RealLifeOverhaul/Scripts/Careers/CampusInterior.Floor.cs), [DeskWork](../Assets/Mods/RealLifeOverhaul/Scripts/Careers/DeskWork.cs).

### 4. Reuse the existing navigation and furnishing work

Reuse `RoomMeasure` and the `FitPlanner` layout rules, which account for existing furniture, door clearance and walkways. Reuse `FitReach` and `DeskReach` for interaction approach points. Validate complete paths from the entrance to essential objects and back to an exit; a successful nearest-point sample is insufficient.

Retain `FitView`'s delayed validation after obstacle carving. Unity documents that navigation queries lag obstacle changes by a frame; path checks in the placement frame can therefore be premature. The current code already waits for updates and probes the carved floor. [Unity obstacle documentation](https://docs.unity.cn/Manual/class-NavMeshObstacle.html).

Validate NPC navigation as well as the player's. Navigation surfaces are associated with an agent type, so a floor the player can use does not automatically support every actor. Reuse Family/Roommates' indoor placement and seating checks. [Unity AI Navigation 1.1.7 surface documentation](https://docs.unity3d.com/Packages/com.unity.ai.navigation@1.1/manual/NavMeshSurface.html).

Use existing frame budgets as starting targets, not measured guarantees: `FitView` allows 2.5 ms of work in a visible frame and 28 ms behind the transition screen. Build only the current interior/floor and reuse templates. Verify actual timings and avoid adding city-wide searches every frame.

### 5. Preserve feature state and cleanup

Keep pay, attendance, fees, booking, relationships and completion in their existing owners. A new desk or reception should invoke those rules. Do not settle a shift or charge for a room again merely because its interior was rebuilt.

Save only additional durable identity/progress under versioned `reallife:` keys. Reconstruct ephemeral layouts and occupants. Test saving from inside: with the feature present the visit must resume safely; with it absent the save must load into a valid vanilla state. Building assets, rental flags, item lists, camera state and navigation locks must not retain temporary changes.

For Going Public, keep cosmetic fixtures and actors local where possible, use stable floor/layout identities where clients must agree, respect the shared clock and other players' ownership, and keep mod-only data out of vanilla replicated building state. Actual host/guest tests are necessary; a local scene object does not prove the surrounding game calls are network-safe. Follow [the multiplayer standard](standards/multiplayer.md) and [architecture rules](standards/architecture.md), including guarded entry points and the existing no-new-Harmony policy.

## Hotels in detail

The catalogue defines seven city hotels/hostels plus the Hamptons cottage offering. City hotels select existing residential buildings in their district and claim their names/frontages; availability depends on finding a suitable host. The cottage uses a board anchored at another building and does not claim that building as a cottage. It therefore needs its own host-resolution design before an indoor conversion.

The proposed hotel experience is: native entrance into a lobby, reception using the existing booking flow, a room door or lift leading to the booked room, freely usable bed/bathroom/seating, and an exit back through the lobby. Leaving the room must keep the booking; checkout ends the entitlement. Load only the current lobby or room, following the existing logical-floor approach. Start with one standard room, then expand to budget, deluxe and suite layouts.

Reuse the existing prices, availability, deposits, refunds, extras and checkout rules. Bind room service and laundry to a room phone or reception. Native sleep/hygiene activities must reconcile with hotel quality bonuses and existing room effects so they are applied once.

There is a concrete lifecycle change beyond furnishing: `TravelDirector.CanSeclude` currently rejects being indoors; `Seclude` blocks movement; and `Watch` treats being indoors while secluded as a reason to end the simulated stay. A walkable hotel needs a separate physical-visit state, while the current secluded state continues to serve simulated trips. Simply calling `EnterBuilding` from `HotelStay.EnterRoom` would conflict with those guards.

Hotel locations can also move when their host becomes the player's property. The current relocation guard protects `TravelState.InRoom`; extend the definition of an active visit to cover the lobby, room and transitions, with ownership rechecked before use. Persist the booking's hotel identity and derive or version any assigned room identity, rather than tying a booking only to a transient scene object.

Hotels are a strong candidate for the first complete new interior because their booking and access entitlement already exist. Their main technical experiment is a native residential room whose functional fixtures work without changing ownership or leaking temporary items into the real building's save.

Evidence: [hotel selection and relocation](../Assets/Mods/RealLifeOverhaul/Scripts/Travel/Hotels.cs), [hotel and room catalogue](../Assets/Mods/RealLifeOverhaul/Scripts/Travel/TravelCatalogue.cs), [travel lifecycle and watchdog](../Assets/Mods/RealLifeOverhaul/Scripts/Travel/TravelDirector.cs), [booking and room entry](../Assets/Mods/RealLifeOverhaul/Scripts/Travel/HotelStay.cs).

## Suggested delivery order

| Step | Deliverable and acceptance condition | Relative effort |
| --- | --- | --- |
| 1 | Baseline audit of the university, one ordinary professional workplace and one tech floor. Enter normally, walk, interact, leave, save and reload. Record which paths currently fall back to menus and why. | Small, diagnostic |
| 2 | One help-wanted employer office in a suitable available host: native entry, a furnished reception/work area, a reachable desk, one indoor conversation and reliable exit. Keep the job's existing pay/rota rules. | Medium; recommended first new interior |
| 3 | One residential example. Player homes require activity/occupant enhancements; an NPC home requires a resident/address and visit model. A booked hotel room is another clear candidate because its access entitlement already exists. Prove native bed and bathroom interactions. | Medium to large, depending on the chosen home |
| 4 | Indoor interviews and social visits, with occupants moving to usable seats and existing outcomes settled once. Expand layouts across venue types only after the lifecycle is established. | Medium to large |
| 5 | Distinct university floor layouts and richer occupants, plus broader workplace/service coverage. Add custom geometry only for a demonstrated layout need. | Content-dependent |

These are scope comparisons, not time estimates. A reliable schedule needs the baseline runtime audit and the functional-fixture experiment.

## Verification before shipping an implementation

Extend the existing checks where relevant: `NavCheck/FitCheck.cs`, `NavCheck/DeskReachCheck.cs`, `EconomyCheck/CampusChecks.cs`, `CampusHomeChecks.cs`, `ClassPlayChecks.cs`, `DeskStartChecks.cs` and `InterviewPlaceChecks.cs`. New offline checks should cover access decisions, deterministic layout identity, floor selection, once-only settlement and save migration; they cannot prove Unity navigation or native item behaviour.

In game, exercise both click and keyboard movement; normal door entry and exit; every essential interaction; blocked/missing fixtures; cancellation while entering; rapid exit/re-entry; floor changes; closing time; a save/load inside; option-off and mod-removal recovery; and Going Public host/guest behaviour. Check that native furniture, ownership, item counts and balances remain correct. Inspect camera/wall visibility, NPC routes, frame cost and resource release over repeated visits.

For a code contribution, run the repository's full build/check gate and report the actual game testing performed. No build or gameplay checks were run for this assessment because it introduces no runtime changes. The local move and upstream revision were verified separately with Git; only this assessment document was added.
