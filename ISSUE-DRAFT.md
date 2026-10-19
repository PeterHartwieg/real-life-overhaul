---
name: Problem report
about: Something in the mod does not work as it should
labels: bug
---

Suggested title: Drinks-cart standing point is lost after carving; retries end the gig as Interrupted

**What happened**

On 19 October 2026, with RLO 0.40.1 and Big Ambitions Build 3682, a Gig Line vendor shift put its cart 3.5 m away and chose a vendor point 3.7 m away. Its solid check failed twice, removing and replacing the cart between checks. The shift ended as Interrupted with zero units and $0. No vendor stand was entered in this attempt.

The full log places the ending at day 2, about 12:13. The PASS at about 12:16 belongs to the next attempt. That attempt used the same offer seed, but chose a vendor point 3.9 m away and passed. Across this session there are 14 passing cart solid checks and two failing checks. The completed comparison shifts were advanced through the clock by the test bridge; they establish that placement and stand entry can work with this pack, not that ordinary customer service was tested throughout.

An earlier player observation at 5 5th Avenue was that the vendor point lay inside the cart footprint. That is a separate observation, without coordinates or an accompanying screenshot here; it is consistent with the source-level clearance gap below, but does not establish the cause of this occurrence.

**What you expected**

A cart whose vendor and customer points remain reachable after it becomes solid. If a candidate fails, another reachable candidate should be tried. If no safe location exists, the explanation should identify that, and the shift should end without a strike. The zero pay itself follows the current pay-per-customer rules: nobody was served.

**How to make it happen again**

These steps describe the recorded scenario, not a deterministic reproduction:

1. Enable Developer diagnostics. On day 2 around midday, stand on the pavement near x 195.4, z -36.1 and accept the Gig Line vendor offer. The recorded offer seed was `1580767406`, with 15 customers and Rain.
2. The bridge closes the phone and walks to the provisional vendor point, initially around x 198.9, z -33.3. It uses the game's `PlayerController.SetNewDestination`, following pedestrian NavMesh corners; it does not teleport.
3. Advance normally at speed 1, with pauses between short walks. Follow the provisional vendor point as it changes. In this attempt it later moved to around x 192.9, z -33.3.
4. Observe two failed solid checks, followed by Interrupted and zero pay. Repeat from the original pavement: the next recorded attempt passed, so the failure is intermittent.

The distinguishing control experiment is to repeat on the same save while remaining at the original position until RLO offers its verified marker, then compare with following the provisional points. Repeat both with RLO alone and with the pack. These comparisons have not been run.

**Versions**

- Game build: 3682. The log reports Unity 2022.3.62f2.
- Mod version in the failing session: 0.40.1.
- Latest source inspected: upstream `main`, `11a8be1f6b547a9be41b1a390075badaa2a3695b`, which is also the commit tagged v0.40.6. Checked 19 October 2026.
- Workshop or a copy from GitHub: provenance of the RLO binary was not established. This was an isolated Windows test executable; it reports `workshop:false`, although copied Workshop mods are loaded in the session.
- Other mods: Bakery, Internet Cafe, Meaningful Ambience, Hyperreal, Big Copilot Link and other Workshop mods; Hard Mode Patch, HART Rival Balance, CityPerformance, BAScheduler.Config, AgentOverlay, Food Trucks and DrivenRides. This list is not a complete binary inventory. Going Public was not switched on.

**Player.log**

The supplied files are `Player-agent.log` and `excerpt-day2-vendor-interrupted.log`. Full logs remain outside the repository; attach them separately if filing this draft. Relevant verbatim lines, in their original order, with unrelated lines omitted:

```text
[Mod:RealLifeOverhaul] Jobs: vendor cart put down 3.5 m from the player; the vendor's place is 3.7 m away (the zone that takes the player is 0.9 m round it); 15 customers; shown in the game's own interface: job panel copied, waiting for the solid check before the place is offered.
[Mod:RealLifeOverhaul] Jobs: 'vendor' started, level 2, rate 1.68.
[Mod:FoodTrucks] Test bridge: walking to x 198.9, z -33.3 (4 m, on the pavement: 2 legs).
Failed to create agent because it is not close enough to the NavMesh
[Mod:RealLifeOverhaul] Jobs check (cart solid): walker radius 0.25 m; cut out of the mesh: the cart's middle yes; still walkable: the vendor's place and the customer's place no, a way on foot from one to the other no, the queue reachable yes: FAIL (candidate refused; the next place is tried).
[Mod:FoodTrucks] Test bridge: walking to x 192.9, z -33.3 (6 m, on the pavement: 4 legs).
[Mod:RealLifeOverhaul] Jobs check (cart solid): walker radius 0.25 m; cut out of the mesh: the cart's middle yes; still walkable: the vendor's place and the customer's place no, a way on foot from one to the other no, the queue reachable yes: FAIL (candidate refused; the next place is tried).
[Mod:RealLifeOverhaul] Jobs: pay slip shown as one of the game's notifications: vendor, 0 ($0.00).
[Mod:RealLifeOverhaul] Jobs: 'vendor' ended (Interrupted), 0 unit(s), 0 earned this shift.
```

These correspond to full-log lines 2414–2460. The next attempt is at lines 2476–2521; its solid check passes at 2489. The Unity agent warning also occurs before this failed shift. It has no stack trace or actor identity, so it cannot be assigned to the bridge, this cart or another mod from these messages alone.

**Screenshot**

None supplied for this occurrence.

**Source investigation: verified**

The links below are pinned to the inspected upstream commit.

1. [VendorJob.Placement.cs](https://github.com/AdultCudi/real-life-overhaul/blob/11a8be1f6b547a9be41b1a390075badaa2a3695b/Assets/Mods/RealLifeOverhaul/Scripts/Jobs/Kinds/VendorJob.Placement.cs#L39) searches at most 12 candidate indices: an initial pavement point and five points 3.5 m away at 60-degree steps, each facing either side of the pavement. The initial anchor is retained across retries. Candidates must pass physical clearance, pavement-corner samples, crossing exclusion, vendor/counter/queue standing tests and complete NPC pavement paths. An index is consumed even when rejected before a cart is instantiated. Two logged solid failures therefore do not mean there were only two candidate indices.

2. The requested vendor, counter and queue offsets are 0.68 m behind, 0.72 m in front and 2.1 m in front. [CitySpots.Stand/OnPavement](https://github.com/AdultCudi/real-life-overhaul/blob/11a8be1f6b547a9be41b1a390075badaa2a3695b/Assets/Mods/RealLifeOverhaul/Scripts/CityLife/CitySpots.cs#L80) can snap each point by less than 0.35 m. The stored points are the snapped positions. No subsequent check requires them to stay on the correct side of the cart or outside its footprint. Before the prop exists, the physical tests cannot detect overlap with that future prop.

3. `PutPitch` nevertheless passes the nominal `Min(VendorBack, CustomerOut)` to [SolidFootprint.Fit](https://github.com/AdultCudi/real-life-overhaul/blob/11a8be1f6b547a9be41b1a390075badaa2a3695b/Assets/Mods/RealLifeOverhaul/Scripts/Core/SolidFootprint.cs#L83), rather than measured clearance to the snapped points. With the logged 0.25 m radius, obstacle half-depth is `min(0.40, 0.68 - 0.25 - 0.05) = 0.38 m`. An allowed 0.33 m inward vendor snap puts the vendor 0.35 m from the middle, inside both this box and the 0.40 m physical half-depth. This arithmetic demonstrates a missing invariant in RLO alone; it is not a reconstruction of the recorded coordinates.

4. [SolidFootprint.Verify](https://github.com/AdultCudi/real-life-overhaul/blob/11a8be1f6b547a9be41b1a390075badaa2a3695b/Assets/Mods/RealLifeOverhaul/Scripts/Core/SolidFootprint.cs#L138) waits 1.6 seconds of `Time.time`, then samples the middle and standing points. `OnMesh` samples within 0.3 m on `AllAreas` and demands less than 0.06 m horizontal displacement. `placesKept` means **both** points pass; `no` means at least one failed, not necessarily both. `wayRound` short-circuits to false when `placesKept` is false, so that `no` is not independent proof of a disconnected route. The queue check separately uses NPC pavement sampling and a complete path to the counter.

5. On failure `Verify` disables carving. `PitchReady` then returns the prop, waits another 1.6 seconds, and resumes the remaining candidate search. This explains the apparent cart jump. It does not offer the vendor marker or call `Stand.Place` until the pitch passes. If `PutPitch` returns false on retry, it shows `reallife_job_vendor_no_room` (“There is no room for a cart on this pavement. Try a wider one.”) and deliberately calls `Finish(JobEnd.Interrupted)`. It does not widen or restart the search or retain an unverified pitch. This sequence matches the log, although there is no final diagnostic distinguishing exhausted indices from a failed prop acquisition.

6. [VendorJob.Owed/End](https://github.com/AdultCudi/real-life-overhaul/blob/11a8be1f6b547a9be41b1a390075badaa2a3695b/Assets/Mods/RealLifeOverhaul/Scripts/Jobs/Kinds/VendorJob.cs#L296) pays completed hand-served customers; clock work is paid through its own settlement. Neither occurred before this failure. [JobDirector.EndJob](https://github.com/AdultCudi/real-life-overhaul/blob/11a8be1f6b547a9be41b1a390075badaa2a3695b/Assets/Mods/RealLifeOverhaul/Scripts/Jobs/JobDirector.Ending.cs#L72) treats Failed, or qualifying Quit, as a strike; Interrupted is not one. A zero slip is shown even when nothing was earned. There is no payment for time spent seeking a pitch on this freelance gig.

**Player movement and other mods**

The solid verification queries mesh geometry and paths, not `Physics` body overlaps. A normal NavMeshAgent's presence is not itself a carving obstacle. RLO has not entered its job stand or begun its stand-settling coroutine before this check. Given the described click-to-move bridge, simply standing on the vendor point does not directly explain `placesKept == false`.

Movement can affect **later candidates**. Each retry passes the player's current position as `near`, and rejects cart centres within 1.6 m of it. A player at a provisional vendor point is within that distance of the same cart centre, even with the full allowed snap. Its opposite-facing candidate can therefore be skipped. `CitySpots.Stand` also performs physical clearance, which may reject occupied places if the player's collider is included. Following unverified points can contribute to exhausting the search; this was not tested against staying put. There is insufficient evidence to blame or exonerate the harness for the final interruption, but it does not explain the initial solid failure by occupancy alone.

The identified nominal-versus-snapped clearance gap needs no other mod. The recorded Hard Mode Patch and HART Rival Balance changes concern prices, needs and rivals; no direct cart/NavMesh override is shown. CityPerformance explicitly disables pedestrian refill and midnight cleanup changes in this build and logs timing hooks instead. AgentOverlay logs reading its status file for display. Food Trucks and DrivenRides patch shared game methods, and other mods can affect vehicles, scenery, timing or loaded mesh state; those effects could change available candidates. The other mods' source/binaries and BAScheduler.Config were not audited here. Fourteen passes with the same pack weaken a permanent incompatibility explanation, but cannot rule out an intermittent interaction. No RLO-only reproduction has been performed.

**Current status and related work**

- [PR #9](https://github.com/AdultCudi/real-life-overhaul/pull/9) was merged on 19 October at 04:23:59 UTC. It added crossing exclusion, rejecting failed solid placements, bounded retries and customer recovery. [v0.40.1 release notes](https://github.com/AdultCudi/real-life-overhaul/releases/tag/v0.40.1) already contain that fix. This report concerns failed placement after that change, rather than claiming those safeguards are absent.
- The six relevant source files (`VendorJob`, its placement partial, `SolidFootprint`, `CitySpots`, `JobProps`, `JobStand`) have identical Git blobs in v0.40.1, newest release [v0.40.6](https://github.com/AdultCudi/real-life-overhaul/releases/tag/v0.40.6) and inspected `main`. No cart fix appears in the later release notes; all nine `reallife_changes_next` rows are placeholders. Thus the source-level gap and terminal behavior remain; reproducing this exact failure on the newer binary is unverified.
- All four upstream issues and 11 PRs, open and closed, plus the available issue comments and PR #9 review, were checked. No separate report or later fix for this placement failure was found. The fork has no issue/PR entries and its `main` is older; the diagnosis branch starts from upstream `main`.

**Unconfirmed cause and next verification**

The leading explanation is that a standing point accepted before carving is too close to the resulting cut, due to inward snapping or insufficient allowance for the baked mesh. The log does not identify which standing point failed or its sample delta. Another concern is that fitting reads the player's agent-type radius, pavement/path queries use `NpcAgentTypeId`, and `OnMesh` uses the unfiltered area-mask overload. Their equivalence is not established by the logged 0.25 m radius. Unity documents that [carved shape depends on bake settings](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AI.NavMeshObstacle-carving.html) and that [query filters select agent types](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AI.NavMesh.SamplePosition.html).

A small, certain runtime correction cannot be selected from this evidence alone. Capture each candidate index, cart transform, requested and snapped standing points in cart-local coordinates, each sample's result/delta and agent type, the relevant bake radii/voxel sizes, and each refusal reason. Compare the movement controls above, including a crowded and a narrow pavement. A fix should preserve physical standing clearance and the counter clips' alignment, use consistent mesh settings, and have a behavioral offline regression check plus an in-game carving check. Merely widening the 6 cm sample tolerance, disabling carving, or paying for an unstarted shift would not establish that the pitch is usable.

No runtime code, version or locale was changed. This draft is local and has not been filed.
