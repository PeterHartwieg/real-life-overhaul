# Walkable hotels implementation plan

Planning baseline: RLO 0.50.0, upstream `main` at `850e2781b644cee7edea2bd97b11357c8d36bbd4`, 23 October 2026.

Status: the first Standard L1 single-player guest-room milestone is implemented on `feature/walkable-hotels`, with 65 focused receipt/access checks. Native tests cover entry, furnishings, activities, checkout, interrupted load, ownership changes, option-off, native exits, saves and removal. Codex and actual Claude Opus agree on delivery of this bounded milestone. The [review record](../reviews/walkable-hotels-opus-review.md) records evidence and the one unrelated clean-main checker failure. Lobby, other categories, cottages and multiplayer remain separate later milestones.

The entry strategy was revised after native experiments: **no registration substitution**. The game may perform its ordinary first-entry initialization of the canonical clean floor cells. Ownership, layout, saved furniture and designs stay unchanged. A cache/global/manager shadow is not used; it can lose unrelated native writes or create unnecessary aliases.
## Intended result

A hotel guest enters through the normal vanilla transition, walks around a furnished room, sleeps in its bed, uses its bathroom and seats, requests services, and leaves freely while keeping the booking. The complete experience later adds an indoor reception/lobby and room-door or lift selection. The first technical and playable slice is the guest room with the existing reception at the street door.

The existing prices, deposits, refunds, room categories, daily service limits, checkout times and paid quality benefits remain authoritative. A failed or unsupported physical visit keeps the existing booking and the existing menu-based experience available.

## Decisions

1. **Use the hotel's current vanilla residential shell.** Preserve `Hotels.Pick` and hotel placement. Do not change the host's size/version for the first slice. Recheck the current host before every entry.
2. **Separate physical visits from simulated travel.** Keep `TravelState.InRoom` as the legacy menu-room flag. A physical visit never calls `Seclude(true)` or the legacy hotel sleep coroutine. Its runtime state belongs to `TravelDirector`.
3. **Keep one booking/accounting owner.** `HotelStay` and `TravelState` continue to book, charge, extend and check out. Physical controls call those operations; they do not reproduce the money rules.
4. **Keep the room non-player-owned.** Never set rented/owned flags to make a fixture work. Use the real native registration without substituting or borrowing its mutable collections. This also prevents the hotel being mistaken for the player's family home.
5. **Require working physical activities.** Prefer actual native fixtures and activities after proving their behaviour. An alternative adapter must provide a real approach, pose, activity and stop path. A decorative bed with a menu sleep operation does not complete this feature.
6. **Treat compatibility as eligibility.** The initial physical path is single-player only. Going Public, unsupported room templates, unsafe hosts and missing assets use the existing menu path. Physical multiplayer support has a separate verification gate.
7. **Keep the change in Travel.** Reuse existing navigation, UI, actor and asset helpers. Extract shared interior infrastructure only after a second venue needs it. No new Harmony patch, runtime dependency or general interior framework.

## Scope of the first slice

Use one existing midrange hotel's resolved host, initially the `kitchen_hotel` slot if eligible, and its `standard` room category. This is a prototype selection, not a change to city placement. Other hotels and categories continue to work through their current path until their templates pass the same checks.

The room contains a functioning bed, shower or equivalent bathroom fixture, usable seat and a phone/desk interaction for services. Keep the native exit unobstructed and reachable. The first slice adds no receptionist NPC, simulated neighbouring guests, new cottage host or new business ownership system.

Do not assume a pre-existing residential layout file is available. Choose a compatible native layout if the installed game provides one; otherwise author a small deterministic furnishing definition for the measured shell with vanilla item IDs. Ship definitions and code, not copied game assets.

## Stage 0: prove the native integration

Create a local experiment on an isolated development branch and a disposable test save. Inspect the installed game's API/assemblies as needed; keep game files and decompiled source out of the repository. Record actual observations before implementing the production path.

| Experiment | Required evidence | If it fails |
| --- | --- | --- |
| Entry into an eligible non-rented residential host | Normal entry/exit succeeds without changing ownership or rental data. Check both the RLO door action and ordinary vanilla click behaviour. | Keep that host on the menu path. Do not globally bypass residential access. |
| Native registration and items | Before/after saves preserve ownership, layout, items and designs. Only first-entry canonical clean-floor initialization may differ. Verify actual binary saves and event order. | Stop physical rollout until the write is understood. |
| Functional fixtures | Native bed, bathroom and seat activities work with non-player registration. Pickup, sale, inventory insertion and interior editing cannot appropriate temporary fixtures. | Prove a complete activity adapter or leave the category on the menu path. |
| Navigation and view | Click movement and keyboard movement reach each interaction and exit after carving. Camera, walls, doors and lighting work in the native shell. | Reject the layout; simplify/re-author it. |
| Sleep and quality | Measure native energy/hygiene, activity start/stop, Home's sleep reading and the room's paid quality effects. Establish a single owner for each effect. | Do not silently drop bonuses or stack legacy effects on native activities. |
| Save/load/removal | Save inside and during entry/activity/exit; load with RLO, Hotels off and RLO absent. The result is a valid playable vanilla state and an intact booking when RLO is present. | Stop physical rollout until a safe save strategy is proved. |
| Recovery | Partial entry, missing prefab, load, forced game relocation, hospital/death and option-off all release owned state without moving a player out of the game's new context. | Keep physical entry disabled until corrected. |

`BuildingHelper.CanEnterBuilding` is useful evidence but may report the normal residential rent restriction. It is not proof that a scoped native entry route is impossible; nor is calling `EnterBuilding` proof that it is safe. The experiment decides which supported route is used. The existing campus code is a precedent, not a substitute for these checks.

## Stage 1: lifecycle and one guest room

Use a small `HotelVisit` component/object owned by `TravelDirector`, with game-free access decisions in `HotelVisitRules` and native furnishing/click actions in the `HotelVisit.Room` and `HotelVisit.Offers` partials.

```text
Outside -> Validate -> Entering -> Furnishing -> Ready
Ready -> physical activity -> Ready
Ready -> Leaving -> Outside
Any active phase -> Recovering -> Outside or base-game-owned relocation
```

The visit holds the captured hotel ID/address, original registration, nearest exterior exit, generation, temporary objects and only the mutations it owns. It pins that host from the entry request through completed exit. Stale callbacks cannot affect a newer visit. A separate in-memory booking revision increments whenever a booking is created, cleared, replaced or loaded. Capture that revision with the booking signature (`HotelId`, `RoomId`, `CheckInDay`); the signature alone cannot distinguish cancel/rebook at the same hotel on the same day. Neither revision nor visit generation is saved.

Entry requires a current booking for that exact hotel/category, an unexpired stay, appropriate proximity and a compatible current host. Reject player/foreign ownership, occupied layouts, saved furniture, established businesses and conflicting claims. A hotel-business ownership discount is not permission to replace a player's flat. Revalidate before furnishing. During a visit, recheck registration identity and ownership/occupancy each frame and on registration-change events. If the host is rented, bought or otherwise becomes ineligible, stop new interactions and leave through the native exit after stopping its owned activity and disabling its fixtures; settle completed effects and any due checkout once. Preserve a booking that is not due. Allow relocation only after exit completes, never re-pin during the visit.

Wire `RoomClicked`, `GoUpSoon`, `AfterDesk`, diagnostics and reload recovery through a single context-aware entry dispatcher. Validate the selected hotel against the booking; the current global `EnterRoom()` signature does not provide that context. The dispatcher decides physical entry or legacy simulation before beginning the transition.

The native experiment proved that ordinary entry initializes the real clean-floor list. This exact native initialization is allowed; arbitrary dirt changes are not. There is no temporary registration and no cache replacement. Native saves serialize the real item dictionary, which never receives hotel fixtures. Ownership-change events disable those fixtures synchronously, with a per-frame backstop for eventless changes. Binary save comparisons cover entry, exit, reload, native rental and removal.
After native entry, confirm the address, instantiate functional items under the native indoor container and refresh the appropriate native item lists. Apply navigation obstacles, wait for their updates and validate complete routes from the entrance to required interaction points and back. Release free movement only when the layout is usable. Reuse `FitReach`/`DeskReach` logic where appropriate; `GameFurniture` copies alone have their behaviour stripped.

Normal cleanup runs on the game's exit event, including exits triggered by other features. Partial entry, timeout, option-off, fault and unload also call idempotent cleanup. Stop an owned activity before destroying its fixtures. Restore borrowed state only if this visit still owns it. Use native exit for a living player still in this room; do not warp a dead/hospitalised player, an unloaded scene or a player already relocated by the game.

After failure, offer legacy simulation only once the player is confirmed outside and all physical-visit state is released. A failed recovery must leave a clear refusal and a retained booking, not a movement lock or a second room underneath the first.

## Stage 2: services, sleep and checkout

Separate the service operations from `HotelStay.Room`'s list rendering. The room phone/desk opens the game's existing UI for room service, minibar, laundry, messages, extension, late checkout and checkout. Calling those operations uses existing prices, limits and settlement. Closing the service list closes that UI only; it does not invoke the legacy `RoomClosed -> LeaveRoom` behaviour.

Native bed/bathroom/seat interactions replace the corresponding simulated sleep, nap, freshen and rest buttons in the physical room. The game and Home own the native energy, hygiene and ordinary activity effects. Travel supplies only the additional hotel effects that are not already applied. Physical sleep must not set the legacy `HotelStay.SleepingBed` signal or run `SleepRoutine` alongside the native activity.

For the first slice, Standard has bed tier 2, zero bed bonus, derived Home bed quality 50 and a 5% rough-night chance. The live build-3682 catalogue has native beds of quality 0 or 20, not 50. Use a native quality-0 wooden double bed for Standard, subject to verifying that its sleep environment uses `EnergyRegen.Bed`. Its native multiplier is already the existing Standard default, so no multiplier correction or mutation of item assets is needed. The activity remains a real native `SleepActivity`.

Preserve the existing paid Home quality through one optional game-free `HomeBridges.PhysicalBedQuality : Func<int>`, defaulting to -1. Home reads it only in the native-bed branch when sleep starts. Travel returns `HotelStay.BedQuality(room)` only for this visit's exact bed, a Started/Running native sleep and the matching booking revision. It never sets the legacy `SleepingAway` signal or Home's `_awayNight`. Energy and the ordinary native sleep happiness effect belong to the game; Home quality belongs to the booking; Travel owns only its completed-night outcome. The native happiness effect is an intentional addition to the former menu simulation and must appear in validation results. Deluxe and luxury remain on the menu until their mapping and fixtures pass the same checks.

A native shower uses the actual duration and Home's normal hygiene progression; the instant menu Freshen operation is hidden in a physical room. This is an intentional change to how the guest performs the activity, and the acceptance test must confirm that normal use can fully freshen the guest without a second instant hygiene effect. Luxury morning treats, when those rooms are added, remain their own once-per-day first-entry operation; they are not conditional on a four-hour sleep.

Use one new optional field, `ncred`, in `reallife:travel`, exposed as `TravelState.NightCredited`: the stay-day of the last credited hotel night. Use `int.MinValue` as the no-credit sentinel at initialisation, on a missing field and in `ClearRoom`; omit that sentinel when writing. Do not use 0: `StayDay()` returns `CurrentDay - 1` before 06:00, so day 0 can be a real stay-day. Capture the active booking revision/signature, `TravelEffects.StayDay()` and game-clock minute when sleep actually starts. On an eligible sleep's end/stop, credit once when elapsed sleep is at least 240 minutes, the booking still matches, and its captured stay-day is later than `NightCredited`. Compute the outcome, then synchronously record the night receipt and increment `NightsSlept` before invoking external mood/notification effects. Attempt those effects once behind the guard; a fault is logged and does not retry the receipt or duplicate the night on load. The helper must make repeated completion calls harmless and include fault-injection checks before/after the receipt and external effects.

Both the physical path and legacy `SleepRoutine` use that credit gate. This intentionally fixes repeated overnight credits for the same booking/stay-day, including when a booking changes from physical to legacy mode; include this bounded behaviour change in the upstream proposal. Preserve the legacy distinction between its night sleep and nap. Native bed sleep has no legacy Night/Nap button distinction: the first completed native sleep of at least four hours earns that captured stay-day's credit, even if it happens during daylight. For example, a qualifying sleep starting at 13:00 consumes the same stay-day's credit that one starting at 23:00 would use. There is no additional night-window restriction. Test and document this deliberate rule.

Keep pending native sleep only in memory, matching Home's unsaved active-sleep tracking. Loading a save cancels the unfinished activity without a new charge, without resuming animation and without awarding its pending hotel-night outcome. Already applied energy and saved receipts remain. Even if an unfinished sleep had passed 240 minutes, it earns its hotel-night credit only when it ends through the normal observed path. The spike must check whether autosaves routinely interrupt native sleep; if this policy produces routine lost nights, stop and design a coordinated checkpoint with Home before shipping. Do not add a Travel-only checkpoint that invents a completed night Home never observed.

Test day 1 before 06:00 (a valid stay-day 0), the 06:00 boundary, same-day repeat sleeps, cancel/rebook, load and transition to the menu path. Old saves without `ncred` use the no-credit sentinel; retain their existing `NightsSlept` value rather than inventing a past credit date. Use a dedicated `ncred` write that omits only `int.MinValue` and preserves 0: the current generic integer `Put` helper omits 0. Read using `Int(data, "ncred", int.MinValue)` and verify the credited-day-0 round trip.

Checkout is a hard deadline, including during native sleep. Prevent new interactions when it is due. End the owned activity through a verified native stop/finish path, allow the normal activity-end/Home observation tick, settle earned hotel effects once, then call the existing checkout accounting and recover through native exit. Waiting is bounded; a stuck native activity is a recovery fault, never an indefinite checkout extension. `HourTick` currently only knows legacy `_sleeping`; it must explicitly cooperate with the physical visit. The legacy `StopSleep` method is not assumed to be a native-activity cancellation API.

Never add temporary fixtures to the real saved item dictionary. Stop the exact owned activity and its native time machine, disable fixtures before ownership-sensitive native exit, then let the native indoor container destroy them. While still inside, fault cleanup disables references rather than destroying controllers still cached by the manager. Cleanup removes only this visit's CTA and navigation block and is idempotent.
Voluntary exit keeps the booking. Voluntary checkout, automatic checkout and closing a menu are distinct operations. Extension and late checkout immediately change the live access deadline. Failure or feature disable does not implicitly refund, rebill or end the booking.

## Saves and other systems

Derive physical presence from the native address, matching booking and active layout generation. Do not persist Unity references or a second booking record. On load, rebuild only if the resolved hotel, current building and booking still match and pass eligibility; otherwise recover safely while retaining the booking. Keep legacy `inroom` saves on their existing resume path.

Append only `ncred` for this slice to `reallife:travel`, with a default for older saves and preservation of unknown/unreadable future records. No saved visit flag, pending animation or duplicate booking record is added. The save experiment must establish whether native position and item capture can remain safe without rewriting them. Do not assume an asynchronous exit inside a save callback finishes before serialisation.

`HomeDirector` already recognises being inside a native building and recognises native sleep/hygiene activities. Audit `LifeLinks` and `SleepingAway` so those are not counted as simulated hotel sleep too. Its hourly check also classifies a non-owned interior as somebody else's place, which can apply the low-hygiene "people notice" penalty even when the guest is alone. Add one optional `HomeBridges.PrivateGuestRoom` predicate, default false and wired through `LifeLinks`, to suppress only that public-presence check while in the guest's physical private room. It must not classify the hotel as home, change ownership, affect tidiness, spawn roommates/family or apply to the later public lobby.

The current Companion API v1 does not expose travel state, so this work requires no contract change. Audit any new callers before implementation rather than widening the API speculatively.

`Hotels.Keep` must protect the full physical visit, including transitions; `TravelState.InRoom` no longer describes every occupied hotel. Preserve current hotel selection and fall back at the current host when it is ineligible. Register physical cleanup and any save-state changes with `FeatureList.Life`, preserving the existing travel cleanup/reset behaviour.

Going Public initially routes new hotel visits to legacy simulation. If a session begins during a physical visit, stop new interactions and safely unwind without skipping the shared clock or modifying other players' state. Enabling physical multiplayer later requires host/guest tests for entry, fixture replication, ownership, host-address agreement, save handling and the native sleep-vote path.

## Stage 3: indoor reception and broader room coverage

After the guest room passes, separately prove safe replacement of a layout during one building visit, including stopping interactions, releasing item controllers and navigation, rebuilding and restoring the correct exit. Campus floor changes only relabel a shared school layout; they do not prove this replacement path.

Then add a lobby with a reception interaction, a room door/lift, clear access to the booked category and a return to the lobby. Booking at reception should leave the player free to walk to the room entrance. Keep physical lobby/room location distinct from booking and menu-open state. Load one active layout; revalidate both directions and save/load from each. An address alone cannot identify which of two layouts was active: default load recovery to the safe lobby entrance, preserving the booking, unless a later reviewed requirement justifies a saved zone identifier.

Add a receptionist actor after the lobby's routes and interactions work. Expand to other supported midrange rooms, then budget/luxury rooms using authored templates and the same fixture/accounting checks. Hamptons cottages remain a separate host-resolution feature because their current board does not identify a claimed residential interior.

## Planned code areas

| Area | Planned change |
| --- | --- |
| `Travel/HotelVisit*` (new) | Runtime lifecycle, pure access rules, native furnishing and click actions; no registration adapter. |
| `Travel/TravelDirector*` | Own visit lifecycle; entry dispatch, native events, load, failure/option-off paths. Keep simulated travel's watchdog scoped to simulation. |
| `Travel/HotelStay*` | Extract reusable service operations, physical checkout coordination and once-only hotel activity settlement. Preserve booking economics. |
| `Travel/TravelState` | Append the optional `ncred` receipt field; legacy `InRoom` remains legacy. |
| `Travel/Hotels` | Pin active host and expose strict physical eligibility without changing placement. |
| `Travel/HotelReception` and `HotelDeskDialog` | Route post-booking actions with hotel context; later support indoor reception. |
| `Wiring/LifeLinks`, `Home` | Audit native versus simulated effect ownership; use existing bridges for any required integration. |
| `Meta/FeatureList.Life`, settings, texts and Guide | Register physical cleanup/option behaviour and required locale/Guide changes. Follow the repository's option-default policy. |
| Offline checks and docs | Pure decision/receipt tests, integration diagnostics and documented behaviour/limits. |

## Acceptance and release

Each implementation change follows the repository's branch, contribution and review rules. Discuss the feature scope with the maintainer before proposing a large upstream contribution. No issue, PR or external message is part of this planning task.

Run focused pure checks during development for access, state transitions, expiry/extension, fallback, receipt idempotence and old saves. Extend existing Travel/Economy checks and relevant navigation checks. Once ready for contribution, run the full documented build/check gate and locales checks with the game's assemblies; update texts in the required locales and do not change the release version.

The in-game acceptance matrix must include:

- Entry, keyboard/click walking, every fixture, exit, return, booking at another hotel and unsupported-category fallback.
- Free fixture use without pickup/sale or home/family ownership side effects; correct native and hotel-specific energy/hygiene/night effects.
- Repeated sleeps, interrupted sleep, manual/autosave, reload, checkout while asleep, expiry during entry, extension and late checkout.
- Closing service UI, option-off, failure at each phase, forced exit, death/hospital, rapid re-entry and scene unload.
- Saves loaded with RLO, with Hotels off, with older data and with RLO removed; unchanged real building registration and item state.
- Going Public fallback and a session starting mid-visit; later host/guest tests before enabling physical multiplayer.
- Repeated-visit object/event/navigation cleanup, visible-frame and transition timing, and camera/wall/lighting behaviour.

The first completed feature milestone is one usable guest room that passes these gates. The final hotel milestone adds the lobby and supported category templates. Agreement on the plan does not replace the native runtime experiments.

## Source anchors

- [Existing assessment](../walkable-buildings-assessment.md)
- [Travel state and saves](../../Assets/Mods/RealLifeOverhaul/Scripts/Travel/TravelState.cs)
- [Travel lifecycle and watchdog](../../Assets/Mods/RealLifeOverhaul/Scripts/Travel/TravelDirector.cs)
- [Hotel booking, entry and checkout](../../Assets/Mods/RealLifeOverhaul/Scripts/Travel/HotelStay.cs)
- [Simulated room controls](../../Assets/Mods/RealLifeOverhaul/Scripts/Travel/HotelStay.Room.cs)
- [Simulated sleep and effects](../../Assets/Mods/RealLifeOverhaul/Scripts/Travel/HotelStay.Sleep.cs)
- [Hotel selection and relocation](../../Assets/Mods/RealLifeOverhaul/Scripts/Travel/Hotels.cs)
- [Campus entry and shadow registration](../../Assets/Mods/RealLifeOverhaul/Scripts/Careers/CampusInterior.Entering.cs)
- [Campus native item placement](../../Assets/Mods/RealLifeOverhaul/Scripts/Careers/CampusInterior.Floor.cs)
- [Furniture stripping](../../Assets/Mods/RealLifeOverhaul/Scripts/Core/GameFurniture.cs)
- [Save event relay](../../Assets/Mods/RealLifeOverhaul/Scripts/Core/SaveHook.cs)
- [Feature ownership and cleanup](../../Assets/Mods/RealLifeOverhaul/Scripts/Meta/FeatureList.Life.cs)
