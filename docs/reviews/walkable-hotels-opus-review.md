# Walkable hotels: independent Opus review

Date: 23 October 2026. Baseline: RLO 0.50.0, upstream `main` at `850e2781b644cee7edea2bd97b11357c8d36bbd4`.

Outcome: Codex and Claude Opus 5.5 agree on the [implementation plan](../plans/walkable-hotels.md). The initial planning agreement is recorded below. Execution reviews and native experiments have since refined the entry-isolation design; the first production guest-room path is now implemented, with acceptance tests and execution review continuing.

The user requested an Opus second opinion and discussion until agreement. The review used one persistent local Claude Code session, ID `90cf9463-b42f-4751-a177-af4305f2134f`, over five rounds. Each response reported model `claude-opus-5-5`. Opus had read-only repository tools and did not implement or modify anything. Codex independently inspected the relevant sources, challenged proposals and authored the plan.

## Review rounds

1. **Independent source review.** Opus inspected Travel, native campus entry, furniture, Home accounting, multiplayer rules and the companion contract. It agreed with separate physical-visit state and retained booking ownership, but challenged lobby-first scope, a saved visit flag, player-owned shadow registrations and unspecified sleep/checkout semantics.
2. **Architecture negotiation.** Codex accepted room-first scope, non-player registrations and a hard checkout cap. Codex rejected silently dropping room bonuses, a menu sleep without real physical behaviour, cleanup only on normal exit and treating local scene objects as proof of multiplayer safety. Opus agreed to a Standard-only initial slice, genuine fixture/pose requirements, single-player physical visits and broader cleanup. It proposed one saved night receipt and cancellation of unfinished native sleep on load, matching Home's current tracking, with a runtime test to determine whether that policy is viable.
3. **Written-plan review.** Opus checked the actual plan and requested explicit daytime-sleep rules, handling of ownership changing during a visit and a narrow privacy bridge for Home's low-hygiene penalty. Codex verified those sources and applied the changes.
4. **Final architectural agreement.** Opus explicitly agreed that no design disagreements remained. It accepted a runtime booking revision to distinguish same-day cancel/rebook and Codex's correction to record a sleep receipt before invoking external effects that might fail.
5. **Save boundary audit.** Codex caught that stay-day 0 is valid before 06:00 on game day 1, so 0 could not mean "not credited". Opus agreed with an `int.MinValue` sentinel and pointed out that the existing generic integer writer drops 0. The plan now requires a dedicated writer preserving credited day 0 and a round-trip test.

## Decisions reached

| Question | Agreed decision |
| --- | --- |
| First playable slice | One eligible existing midrange host, Standard guest room, exterior reception retained initially. |
| Lobby | Later milestone, after proving layout/fixture replacement during a visit. Campus floor relabelling is not proof of replacement. |
| Building registration | Revised during execution: no substitution; permit only native canonical clean-floor initialization and never serialize temporary furniture. |
| Room interaction | Real movement and usable bed/bathroom/seats. Native fixtures first; a complete physical adapter must be proved if necessary. |
| Paid quality | Standard's native bed parity is measured. Other categories retain menus until their bonuses can be preserved. |
| State | Physical visit is runtime-only and separate from legacy `InRoom`/Seclude. The existing booking record remains authoritative. |
| Sleep receipt | One optional `ncred` field shared by native and legacy night settlement, preserving credited day 0. |
| Qualifying native sleep | First completed sleep of at least four hours per captured stay-day, including daytime; legacy nap distinction retained. |
| Interrupted native sleep | Cancel on load without new charges or pending hotel-night credit, matching Home; redesign before shipping if actual autosave behaviour makes this routinely lose nights. |
| Checkout | Stop the activity safely at the deadline, allow normal observation, settle once, exit; no indefinite extension. |
| Failure during settlement | Record receipt/count before guarded external effects; no replay or double credit after a failure. |
| Privacy | A narrow Home bridge suppresses public-presence hygiene penalties in private guest rooms without making the hotel the player's home. |
| Ownership changes | Recheck the real host during the visit; safely leave before allowing hotel relocation. |
| Multiplayer | Keep the existing menu path initially; gate physical host/guest support separately. |
| Unsupported hosts or fixtures | Preserve the booking and existing menu path after safe exterior recovery. |
| Cottages | Separate later work because the current booking board does not claim a cottage interior. |

Opus's fourth-round verdict was: "I agree with the final plan in `docs/plans/walkable-hotels.md`. No design disagreements remain. The native runtime gates are still unverified."

Its fifth-round response retained that agreement and requested the explicit credited-day-0 writer instruction, which is included in the final plan.

## What agreement does not establish

No build or gameplay verification occurred in this planning task. Native entry permission, non-owned fixture behaviour, save/exit isolation, sleep ending and autosave behaviour, navigation, camera/lighting, option-off and mod-removal recovery must still pass the plan's Stage 0 experiments. Those experiments decide whether the physical path can be enabled; agreement is on the implementation and verification approach.

## Execution review and evidence

The same Opus session continued with read-only review of implementation and, after explicitly adding the local temporary directory to its read scope, the installed game's APIs. Those private inspection files are not part of this repository.

- The completed-night receipt was reviewed and corrected for a type-name collision, notification ordering and a guard that could disable Travel before recovery. The final helper has 42 focused checks, including pre/post-receipt fault injection, day 0, load invalidation and identical-signature rebooking.
- Native source inspection established that a quality-50 bed adds 0.5 to the native sleep multiplier. A real `SleepActivity` plus a narrowly scoped Standard-rate correction is the candidate; it still requires runtime parity measurements.
- A Windows native entry run showed that the original late-shadow proposal could not satisfy a literal zero-mutation gate: native loading initialized dirt before the entry event. Both reviewers rejected treating the campus implementation as proof of isolation.
- A persistent global registration-cache substitution was rejected: rental or other game operations could write to the copy and lose their changes on cleanup.
- A second Windows run read the actual binary save files. First entry changed dirt from 0 to 134 clean cells. Ownership/rent, layout, items and designs were identical before/inside/outside. Repeated entry and saving after that initialization changed none of those fields. Both reviewers accepted a narrow exception for exactly the native canonical clean-floor initialization, which must be disclosed as a persisted difference.
- The manager-only shadow was challenged as unnecessary for non-owned fixtures. Review then identified the real ownership-change race: rental enables pickup and native design serialization. The current fixture experiment tests the smaller alternative: no registration substitution, native registration-change handling plus a per-frame eligibility backstop, immediate fixture disablement and native exit, with no custom shell repaint. This is conditional on activity, save, ownership-change and removal tests; it is not yet a production acceptance result.
- Clean upstream main reproduces the unrelated frozen-debt UI source-check failure. With the installed game paths provided, the baseline also hit a timing-sensitive 20 ms budget check under concurrent load; neither failure came from the hotel receipt changes. The candidate's earlier full Windows run had only the frozen-debt source-check failure.

An inside save reloaded into the correct empty host. The first exit request after reload was too early, while the native transition was still settling, so that run does not count as successful reload-and-exit proof. The probe now reports transition flags explicitly. Every completed rig run restored and hash-verified the prior profile and installation manifest before releasing ownership.


## Production draft and native gates, 23 October 2026

The native bed reports `EnergyRegen.Bed`, quality 0 and multiplier 1. The production Home bridge reports booked quality 50 while that exact native sleep runs. A six-hour production sleep credited exactly one stay-day. Native showering restored Home hygiene to approximately 100; the production service menu omitted simulated sleep/nap/freshen/rest actions and closed without leaving the room.

The L1 shell uses the native `Parking.UndergroundParking.ParkingElevator` component for its exit. The test bridge searched only for `ExitZoneDespawner`, which explained its refusal. Walking to the actual door exited normally. The furnished-room checkpoint also loaded with both RLO and the probe physically removed, exited by ordinary walking, and saved outside. No hotel fixtures were serialized.

Native `RentBuilding` while idle synchronously disabled the temporary fixtures and exited. Its saved registration held the game's 14 default rental items and 294 native design records, without the hotel bed, desk, shower or chair. A deliberately eventless ownership change during native sleep was caught by the next-frame backstop, finished sleep, restored multiplier 1 and exited; the saved item dictionary remained empty.

Opus reviewed the first production draft in the same session (local transcript `round13-result.txt`, headed Round 12). It identified the old-menu resume route, activity ownership, fixture/finish order, mutation during CTA iteration, destruction while native manager still holds references, aborted entry, and checkout racing an exit. The implementation now keeps old menu saves on their menu path, adopts only native activities attached to its fixtures, finishes before disabling in one synchronous call, defers CTA faults to Update, leaves inactive fixtures for native container destruction while inside, detects an aborted entry immediately and settles deferred checkout on either exit path. A second production review and remaining runtime gates are still in progress.

## Production review agreement and regression fixes

The same actual Claude Opus session reviewed the production code in three further rounds (local artifacts `round13` through `round15`). The final verdict was **“Code and design: approved for the first Standard L1 guest-room slice.”** This is code/design agreement, conditional on the runtime acceptance matrix; it is not a release claim.

Those reviews corrected native time-machine cancellation, load-transition ordering, static CTA/event detachment, deferred CTA faults, registration-event guards, death/hospital ownership and recovery timeouts. ContractCheck independently caught missing direct guard calls and five native item-key declarations; both were fixed without weakening its checks.

Production r12 loaded the inside checkpoint into a rebuilt room with its booking, no active activity and no new night credit. A sleep requested at 05:00 until 17:00 stopped at checkout near 11:00; one night receipt was observed before checkout cleared the booking, and native exit finished at 11:06 with fast-forward off. The first r9 test exposed the missing cancellation by running to 17:00; that result is a regression finding, not passing evidence.

Production r14 repeated an idle load in the same process: the rebuilt room had one hotel CTA and no credited pending night. Switching Hotels off during native sleep cancelled the activity, removed the CTA, exited and retained the booking. The first event-count diagnostic did not unwrap `Guard.Wrap`'s closure and therefore reported zero even during a visit; it is not valid evidence for event-detachment counts. The probe was corrected before the final lifecycle run.

Current verification: warning-free build; StandardCheck 98,777, NavCheck 7,712, PlannerCheck 225, ContractCheck 1,478; CatalogueCheck and all nine locales pass. The full EconomyCheck is still being collected against the independently reproduced clean-main banking failure. No release version was changed.

The r6 Windows session restored and hash-verified all 4,427 pre-existing profile files, preferences and the installation manifest, removed its probe and released the rig before the next run. Test evidence remains in ignored build output and the owned rig's rollback directory.

## Final lifecycle run

The corrected closure-aware diagnostic reports exactly one hotel CTA and one registration listener during a visit, and zero after exit. Same-process reload of a save made during a running native sleep rebuilt the room with `NightsSlept=0`, the no-credit sentinel, no active activity and one of each handler. Already saved booking and energy remained; the pending night did not replay.

Switching Hotels off during a running native shower completed the exit, ended the activity, retained the booking and removed both handlers. The native `HygieneItemController.EndUse` is the cleanup path, including restoration of appearance.

A real native rental inside the production room left quest/side-quest lists unchanged. After exit, the only new personal goals were `apartment-min-90sqm` and `apartment-min-200sqm`, appropriate to legitimately renting that L1 apartment. Mood changes were the ordinary first-apartment and completed-goal effects, with No home removed. Native source inspection confirms apartment-size goals read registrations/area and apartment-value goals read the saved item dictionary, never the temporary controllers. No furniture-value goal appeared. An attempted post-exit save comparison reselected the relocated hotel and therefore compared the wrong host; it is excluded from rental-save evidence. The corrected probe captures the original registration before rental.

The full final EconomyCheck reports **one failure out of 16,576**, the same frozen-debt button source check reproduced on clean upstream main. The paused-entry source regression check was updated to locate the new method signature; its assertion remains unchanged. Production entry while paused was also exercised. The final built DLL passes ContractCheck 1,478; all other gates listed above pass.

## Final independent agreement

Opus reviewed the actual runtime JSON, checker logs and native goal readers in its final round (`round16-result.txt`). Its verdict: **“No code or design blocker remains for the first milestone. I agree with delivering it within the limits below.”** Both reviewers agree on the delivered scope: Standard L1 single-player guest rooms, exterior reception, native activities, shared billing and night receipt, other rooms on the menu path. This is not approval of a public release or the later lobby/category expansion.

The final ownership test rented the original Second Avenue 8 host during a running shower. The appearance hash changed during showering and returned exactly to its pre-shower value after rental interrupted it. The activity and both handlers ended, native exit completed and the booking remained. The binary save contains exactly 14 ordinary rental items and 294 native design records, with no hotel bed, shower, armchair or desk. The source and test-only probe now retain the original host for this comparison.

A normal windowed-game frame was inspected after walking from the entrance to the bed. The room, bed, shower, floor, native wall cutaway and lighting rendered correctly; the separate offscreen camera's darker output was not representative of the game view. The final furnishing pass selects wardrobe, cabinet, rugs, coffee tables, plants and wall pictures from the same installed native L1 design. It adds no new activities or saved items and is rechecked for navigation and persistence before delivery.

The final furnished candidate has **31 temporary native items**. All required routes and return paths pass. Its inside binary save still has zero saved items/designs and unchanged ownership, layout and initialized dirt. Renting during native seating stops the activity, removes the CTA and registration listener, retains the booking and exits; the original host's saved rental again contains only the 14 vanilla rental items and 294 native designs. The final furnished DLL builds with zero warnings and passes StandardCheck 98,777 and ContractCheck **1,520** (additional native furnishing keys included). The final game-camera room frame is retained with the runtime evidence.

Remaining release work is the broader manual matrix (additional hosts/game versions, forced death/hospital/fault injection and long-session behaviour), together with the separately planned lobby, other room categories, cottages and multiplayer. None is represented as completed by this first milestone. The inherited banking source-check failure is also explicitly retained, rather than changing unrelated economy behaviour.

The final r16/r18 rig session restored and hash-verified all **4,591** pre-existing profile files, preferences and installation-manifest entries, removed the diagnostic mod and released its ownership lock. The normal game installation/profile was not used for these tests. The completed build remains under `RealLifeOverhaul.Build/out/RealLifeOverhaul`; source changes remain uncommitted on the feature branch for review.
