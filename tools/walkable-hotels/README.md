# Native hotel entry experiment

This separate diagnostic mod is not included in the RLO package. It tests native entry and exit of an existing midrange hotel host and observes its real registration at entry, save and exit. Entry uses the unmodified native manager. The separate `furnish` operation adds four native fixtures to an eligible empty L1 shell for activity and persistence tests. It grants no ownership and never inserts fixtures into the saved item dictionary. The production path is separate; `hotelbook`, `hotelenter`, `hoteluse` and `hotelroom` exercise that path directly.

Build RLO first with `GameManaged` set to the installed game's `Managed` directory, then:

```sh
dotnet build tools/walkable-hotels/HotelEntryProbe.csproj -c Release -p:GameManaged="$BA_MANAGED"
```

The probe refuses normal game profiles. It runs only with `-agent-tests` in one of the existing isolated Mac or Windows agent profiles, and only on a save whose name starts with `FT RLO Hotel `.

## Run when a rig is available

1. Read the shared rig instructions in the sibling `hart-self-driving/tools/agent-tests` directory. Check status and acquire a unique ownership reservation. An existing reservation, even with a stopped process, is not available. Never interrupt another test.
2. While the owned rig is stopped, back up its complete profile, preferences and installation manifest, and record hashes. Install this branch's RLO package and `HotelEntryProbe.dll` in a separate `ModsLocal/HotelEntryProbe` folder; update only the corresponding manifest entries. Keep other mods and settings intact.
3. Start the owned rig and create a new disposable city named `FT RLO Hotel Entry <unique suffix>`. Keep the clock paused. Wait for RLO to resolve its hotels.
4. Atomically write a request such as `{"id":"status-1","op":"status"}` to the probe's `request.json`, under the rig's ownership/lifecycle lock. Read `response-status-1.json`. Use a fresh ID for every request. The available operations are `status`, `enter`, and `exit`. The optional `hotel` selects an existing resolved midrange slot; the default is `kitchen_hotel`. Status lists all three hosts' occupancy facts and the game's `CanEnterBuilding` result. An empty residential host is not required to be listed for rent: the game's lazily created residential registration can have `AvailableForRent=false`.
5. Request `enter`. Entry deliberately uses the native manager directly on the resolved host, without a proximity test: this is a controlled test, not the player's entry dispatcher. Record the event history and snapshot, including the two-second settled snapshot. A missing transition event is inconclusive. After the first canonical native clean-floor initialization, all field comparisons must remain unchanged. Dirt collection identity itself may change during native loading. A change before `entered` disproves the assumption that installing a shadow at that event alone isolates entry. Ordinary vanilla door clicks, occupied-host refusals, keyboard/click walking and rendering need separate tests.
6. Use the rig's own save command to write a new disposable checkpoint while inside. Request `status` afterward. Request `exit`, then save again and inspect the real native save data. The probe's event snapshot is not proof of final serializer ordering or persistence.
7. Reload the inside checkpoint with this RLO build, with Hotels off, and with RLO absent, using the rig's stopped/owned installation workflow. Check that each produces a valid playable native state. This empty-shell experiment does not prove fixture, furniture-removal, sleep, camera or walking safety.
8. Stop the rig, retain the responses and game logs, restore and verify all pre-existing profile files/preferences/manifest entries, remove this diagnostic installation, and release ownership. Do not overwrite newer work during restoration.

If entry times out, inspect the native state before sending `exit`. The probe does not issue an asynchronous exit from a save handler or silently repair the original registration. Any observed mutation must be understood before the production adapter is enabled. Use the agreed implementation plan's remaining fixture, navigation, sleep, save and recovery gates afterward.

## Native initialization and fixture comparison

`capture` records the host's baseline without entry. After a normal rig save, `saved` with `saveName` reads that exact disposable `.hsg` checkpoint using the game's binary deserializer, without loading it into the current world. The path is confined to the current character and the disposable naming prefix. Responses include separate ownership, layout, items, dirt and design hashes and collection counts.

The first native entry initializes the shell's clean floor cells. Compare before, inside and outside checkpoints, then capture a new baseline and repeat entry. Only that first canonical dirt initialization may differ. `canonicalDirt` compares the current list with the native floor-cell generator; all other fields and subsequent entry must remain unchanged. A persistent cache substitution was reviewed and rejected because unrelated rent operations could write into the temporary copy and lose their changes. That experiment is no longer available.

`room` reports the shell, native beds and their qualities, fixtures, native activity, energy and interaction routes. `furnish` requires a settled L1 shell and a native quality-0 bed supplied as `bed`. It uses selected positions from the installed game's L1 residential design and adds a test shower; no game design files are redistributed. Its layout still requires navigation and visual verification. `bed`, `bath` and `seat` open the actual native activity interfaces. Use the bridge's normal UI controls to start and stop them. The bed operation can set a disposable initial `energy` from 20 to 90, and `correctEnergy:true` experiments with Standard's existing multiplier. These controls are diagnostic only.

Fixture ownership is watched every frame and on the native registration-change event. Losing eligibility disables the fixtures, stops only the probe's own activity and begins native exit. Fault, ownership, repeated-visit and removal tests must pass before using this approach in production. On reload, wait for `entering=false`, `exiting=false` and the expected native address before testing the exit; the bridge's scene-load completion precedes the end of the door transition.

## Production operations

`hotelbook` books Standard for two nights at the selected host using actual billing. `hotelenter` calls the production dispatcher and enforces its normal proximity/access gates. `hoteluse` with `kind` of `bed`, `bath`, `seat` or `desk` invokes the installed production click action; use fresh native UI handles to start the activity. `hotelroom` reports production readiness, privacy, Home bed quality, night receipts and native activity state.

On this L1 shell the native exit uses `Parking.UndergroundParking.ParkingElevator`, not `ExitZoneDespawner`; a bridge command limited to the latter is not proof of a broken exit. Walk to the actual native doorway or use the native manager exit for recovery tests. Keep raw saves and JSON/render evidence in ignored build output, never commit game assets or decompiled source.

`hoteloff` / `hotelon` toggle the disposable run's Hotels setting for interruption tests. `hotelclaim` performs a real native rental while a production room is ready, capturing its original host first so relocation cannot redirect the subsequent `saved` comparison. Production diagnostics include completed goals/quests, native appearance hashes and closure-aware hotel CTA/event counts. `dismiss_test_overlay` closes the isolated rig's Campaign chooser through that mod's own panel method so rendered-game checks can see the room; it neither starts a campaign nor edits that mod's settings or data.
