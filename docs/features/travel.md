# Travel and lodging

## In a sentence

Hotels the player can stay in, and holidays bought at the game's own travel agencies.

## Where the code is

- Folder: [`Scripts/Travel`](../../Assets/Mods/RealLifeOverhaul/Scripts/Travel/). The files are listed in the code block below.
- Read first: `TravelDirector.cs` (the one component, with its watchdog), `TravelCatalogue.cs` (every number and name, game-free) and `TravelState.cs`.

## How it talks to other features

- `TravelBridges` has doors to Home, Social, Law (Justice), Transport and Leisure, each with a working default. `Wiring/LifeLinks.cs` fills in Justice, Home and Social; see [Bridges](#bridges).

`Scripts/Travel/`: hotels the player can stay in, and holidays bought from the game's travel agencies.

```
TravelCatalogue  every number and name: rooms, the eight hotel slots, trips, events, prices (game-free)
TravelState      what a save remembers, one line in modData under "reallife:travel"; the no-repeat dealing (game-free)
HotelNightRules  a completed sleep's booking identity and once-per-stay-day night receipt (game-free)
TravelBridges    the doors to Home, Social, Law, Transport and Leisure, each with a working default
TravelEffects    money, hunger, energy and travel's own happiness modifiers, through the game's systems
TravelText       lines from en.json with their {names} filled
Hotels           which buildings are hotels; sign, awning, luggage cart, the one doorman, the city-map icons
HotelVisit       native guest-room lifecycle, temporary fixtures and click actions
HotelVisitRules  pure supported-shell and empty-host access gates
HotelStay        booking, services, the fallback room list, sleep through the game's time skip, checkout; HotelReception is the desk's list
Holidays         agencies, the agent, tickets, the trip as a list, the Hamptons weekend, burnout, the trade fair
TravelPhone      the "Trips" contact (the page) and the agency's own number
TravelDirector   the component: save and load, ticks, the game's panel at a door, the watchdog, the test buttons
```

## What the game has

- **No hotels.** Residential buildings cannot be entered unless rented (`cantenterunlessrented`); pressing the door of one opens the rent screen or says "private property".
- **Travel agencies** are an office business type (`ba:businesstype_travelagency`, skill `ba:skill_travelagent`) that the player or the AI can run.
  Its one product is `ba:itemname_hourlytravelconsultantfee`, sold to simulated customers only (`spawnCustomers` is off): the player can never buy anything there.
  The mod's agent is met at such a building's door, whoever owns it, while it is open.
- **The Hamptons** (`ba:neighborhood_thehamptons`) is part of the one city scene, reached by road and by its own subway station (`TheHamptonsStation`).
  Its houses are size `t` plots that rent for 4,768 a day; its shops are on its own Seventh Avenue; there is a theater at Ocean Crest Road 1 and a cinema at Harbor Street 1.
  There is an Airport Avenue (one warehouse) but no airport building, no flights and no terminal anywhere.
- **The casino boat** (`CasinoBoatManager`, the building at Pier 5) is in harbour on Fridays from 18:00 to 22:00.
  A ticket from the ticket house plays the game's own boarding sequence, skips to midnight and puts the player in the casino interior; at 05:00 on Saturday they are put ashore.
  So the player already rides it, on the game's terms and timetable only.

  Travel does not touch it: the mod's cruise is a different ship, played in cards.

- **Being away.** Nothing in the game needs the player present except their own two hands: businesses trade through their employees' shifts, and every tick of the time skip (`TimeMachine` runs `GameManager.RunMainGameTick`) is a full tick of the simulation.
  Stock that runs out stays out unless a delivery contract or a logistics manager refills it, to-do tasks pile up (`BuildingRegistration.Alerts`), rent and wages are charged daily.

  The daily accounts are `GameInstance.financialSummaries` (`totalBusinessProfit` and one statement per business).

- **Sleep** is `EnergyHelper.SetCurrentEnergyRegen` (bed 0.35 energy a minute, car 0.30, bench 0.25) times `energyRegenMultiplier` (the default plus a bed's quality / 100), run by the time skip.

## Walkable Standard guest rooms

A current Standard booking at an eligible empty L1 hotel uses the vanilla building transition. The guest can walk to a native bed, shower, armchair and service desk, with wardrobe, rugs, plants and other native furnishings, and leave by the native door while keeping the booking. Reception remains outside for this first slice. Other categories, shells, occupied hosts and Going Public sessions retain the existing room menu.

`HotelVisit` owns runtime presence, temporary furniture and its native activities; legacy `TravelState.InRoom` still means the simulated menu room. The desk reuses existing food, laundry, extension, late-checkout and billing operations. Closing that menu keeps the guest inside. Native sleep/shower/rest replace the corresponding instant menu actions.

The native quality-0 bed supplies Standard's normal energy rate; `HomeBridges.PhysicalBedQuality` supplies the booked Home quality of 50 only during that bed's actual sleep. The game owns ordinary sleep happiness and energy; Home owns hygiene and its sleep tracking. `PrivateGuestRoom` suppresses only the public low-hygiene penalty. The room is never a player-owned home.

Completed native sleep lasting at least 240 actual game minutes earns at most one hotel night per captured stay-day (06:00 boundary), including daytime sleep. Walking to bed does not count. The saved `ncred` receipt preserves day 0; old saves without it retain their existing count. Loading cancels an unfinished native activity and rebuilds the room at its entrance without awarding a pending night. A finished receipt is not replayed.

Checkout cancels the owned native activity and its fast-forward, observes normal activity completion, settles existing billing and exits. Ownership changes, option-off and incompatible state stop new actions and recover outside, retaining an unexpired booking. Fixtures are never inserted into saved furniture. First entry may initialize the game's normal clean-floor cells; no registration replacement or shell repaint is performed.

For runtime evidence and remaining acceptance gates, see the [implementation plan](../plans/walkable-hotels.md) and [Opus review record](../reviews/walkable-hotels-opus-review.md).

## Prices

Set against the game's own figures, read from its data files: the smallest apartment (54 m2) rents for 31 a day in Industry City, 34-39 in Lower Manhattan, 40-48 in the Garment District, 55-63 in Murray Hill, 79-95 in Hell's Kitchen and 134-138 in Midtown; 96 m2 for 54-261; 204 m2 for 115-529; 589 m2 for 364-1,455.
The cashier job pays 25 an hour; staff start at 12 to 18.

| room | a night | sleep | rough night | refund of unused prepaid nights |
|---|---|---|---|---|
| hostel: bed in a dorm | 45 | bench rate, multiplier -0.1 | 45 % | none |
| hostel: private single | 75 | car-seat rate | 25 % | none |
| mid-range: standard double | 150 | bed rate | 5 % | 50 % |
| mid-range: deluxe | 230 | bed rate, +0.2 | never | 50 % |
| luxury: king room | 480 | bed rate, +0.4, comfort modifier (+6 for 24 h) | never | 70 % |
| luxury: corner suite | 900 | bed rate, +0.6, comfort modifier | never | 70 % |
| cottages: bedroom / whole cottage | 210 / 380 | bed rate, +0.15 / +0.35 | never | 50 % |

Times the hotel's own factor (Industry City and Lower Manhattan 0.85, Murray Hill 0.95, Midtown 1.2), times 1.2 on Friday and Saturday nights, times the season (the weather's own, quarters of the year the player chose, counted from spring: city hotels 1.15 in summer and 0.9 in winter, the cottages 1.4 and 0.75), times the Leisure bridge's demand for the day.

Seven nights or more cost 0.8 of that every night, fourteen or more 0.72.
On deposit the first night is paid at the desk and the rest, 5 % dearer, at checkout.
Late checkout (16:00 instead of 11:00) is 30 % of a night, free in a luxury room.
Eight stamps (one a night) are a free night worth up to 250.

A completed night sleep of at least four game hours counts at most once per booking and stay-day. The stay-day is captured when sleep begins and changes at 06:00. Naps retain their existing behaviour. Repeated night sleeps on the same stay-day no longer add extra `NightsSlept` or repeat a rough-night or comfort outcome.

The optional `ncred` field at the end of `reallife:travel` records that stay-day. Its absent value is `int.MinValue`, because day zero is valid before 06:00 on game day one. Older saves retain their existing slept-night count without an invented receipt date. A runtime booking revision prevents an old sleep from settling after a load or cancel/rebook, even when the replacement has the same hotel, category and check-in day. Pending sleep is not saved. The receipt and count are recorded before effects; a failed effect is logged without replaying the night.

A hostel bed costs what the cheapest apartment rents for and sleeps worse, so it is a stopgap, never the better deal: a week of it (36 a night) is still dearer than renting in Industry City.
A mid-range room costs a day's rent of a mid-sized apartment in the same district, a luxury room a day's rent of a large one, and a beach week about twelve days of the cashier job.

Extras: vending sandwich 8 (+25 hunger), room service 32 (+60) or 65 in a luxury room (full), cottage hamper 28, minibar 12 or 18 (+12 hunger, +3 energy, four a day), laundry 8 / 25 / free, dinner through the concierge 85, dinner in the village 60.
Every amount on this page is at the start's prices: the rooms, the extras, the loyalty night's cap, the trips, a splurge, the suitcase and what a trip's event gives or takes are all asked for through `Prices.At`, so they move with the city's price level, and the room's list shows what it charges (`TravelCatalogue`, checked by EconomyCheck's TravelPriceChecks).
Nothing pays the player anything, and every effect is capped (hunger at full, one mood a day).

| trip | days | economy | first class | splurge a day | dear / cheap season |
|---|---|---|---|---|---|
| Weekend in the Hamptons (real) | 2 nights | 520 | 910 | - | summer / winter |
| Coast cruise | 3 | 1,500 | 2,850 | 160 | summer / autumn |
| Ski trip | 4 | 2,900 | 5,220 | 220 | winter / summer |
| Miami beach week | 5 | 2,400 | 4,320 | 180 | winter / summer |
| Europe tour (jet lag) | 7 | 5,600 | 9,800 | 300 | summer / winter |
| Trade fair (by invitation) | 2 | 900 | 1,440 | 140 | autumn / summer |

Dear season x1.25, cheap x0.85.
A companion adds 70 %, insurance 8 %.
A suitcase is 120.
A trip falls through at the gate 6 % of the time: all of it back with insurance, 40 % without.
A ticket is good for seven days; handed back or expired it returns everything with insurance and half without.

## Hotels

Eight places (`TravelCatalogue.Hotels`).
A city hotel is the residential building of its district, of the sizes its slot names, with the highest traffic index (ties by street name, then number) that the player neither rents nor owns: static data only, so it is the same building in every session, and a booking remembers the slot, not the address.

With the game's data as shipped: the Bobbin Hostel at Fourth Avenue 1 (Garment District), Dockside Bunks at Twenty-first Street 23 (Industry City), Hudson Court Hotel at Second Avenue 8 (Hell's Kitchen), the Battery Row at Fourth Avenue 45 (Lower Manhattan), Murray Hill Arms at Sixth Avenue 18, the Broadway Grand at Broadway 13 (Midtown), the Meridian Tower at Second Avenue 14 (Hell's Kitchen).

Seabreeze Cottages is a board by the Hamptons theater (Ocean Crest Road 1; failing that the cinema, then Seventh Avenue 5).
The log line "Travel: hotels:" lists what was picked.

At a hotel's door the game's own panel over a place (`GameUi.WorldPanel`, through `GameUi.DoorPanel`) names the hotel and says what is on offer: "Reception" on the interact key (off while a room is held), and "Go up to your room" on the secondary key when the room is booked there.
The mod's own door key stands aside while it is up.

Reception is a `GameUi.TravelList` (`HotelReception`): page one has an entry per room type with tonight's rate, the week's rate and how many are left; a room goes to page two (Back, an entry that steps the nights, Pay now, Leave a deposit).
The result is a notification, and a booked room is gone up to at once.

The room is a list as well (`HotelStay`): an entry per choice with its cost or effect under the name and the button off, with the reason, when it cannot be done (sleep, nap, wake-up call, shower, room service or the vending machine, minibar, laundry, rest, messages, the concierge (luxury), a beach walk and dinner in the village (cottages), extend, late checkout, check out, go out).

Sleep, nap and rest keep the game's own time skip; that hides the HUD, so the list is put away while it runs and the skip's own Cancel button is the way to get up.
What a choice did is a notification.
The player never moves: they stand at the door, held by a navigation block of the game's own.

The list is one window shared by reception, the room and a trip (`TravelDirector.Window`, a `DeskList`).
The mark that points to a hotel is the game's destination marker (`GameUi.GuideMark`).

If the game's list cannot be copied (one "Game UI:" line says so): reception is the game's dialogue panel as before, the room sleeps the night at once and puts the player outside, and a trip is played through taking it easy each day.

Log lines (with Developer diagnostics on): "Travel check (door panel): ...: PASS", "Travel check (reception list): the game's list is open: ...: PASS", "Travel check (room list): ...", "Travel check (trip list): ...", "Travel check (choice): ...".

Not checked in the game: any of the above.
The wording of the panel and the list, how the game's list looks with 20 entries of two lines, whether its tall entries (a trip's text) fit, whether Cancel closes the room list as meant, and that the marker takes the guider when no job has it.

## Holidays

At an open travel agency's door, or by phone once one has been visited (from the start in a city with no agency).
The Hamptons weekend is real: setting off books the cottage from that night and the player travels there themselves; a night slept there makes it a holiday.

The others are played in the same list: departure, a page a day (take it easy, explore, splurge; each with an event from a pool of 72 that does not repeat), a day of game time skipped after each, and a return page that reads what the businesses made from the game's accounts.

Before booking the agent says what is being left: how many businesses, what they made a day over the last week, open problems, the rent that carries on.

Burnout: every day with a business to run or a job taken counts; after 18 a friend says so, after 24 a mild modifier (-6) stays until a holiday is taken (an afternoon's rest in a hotel room takes three days off the count).

## Bridges

Everything other features do for travel goes through `TravelBridges`; each is taken over with one line and handed back by setting null:

```csharp
TravelBridges.Home = new MyHome();            // ITravelHome: Freshen, Laundry, Rest, Holiday, HighStressDays, HasBed
TravelBridges.Social = new MySocial();        // ITravelSocial: Companions, TripTogether, Souvenirs, TableBooked, MissedWhileAway
TravelBridges.Justice = new MyJustice();      // ITravelJustice: MayLeaveTown, MissedWhileAway
TravelBridges.Transport = new MyTransport();  // ITravelTransport: CoachToHamptons, RideCoachToHamptons, CallCar
TravelBridges.Leisure = new MyLeisure();      // ITravelLeisure: PlayGolf, DinnerOut, DemandToday
TravelDirector.SuggestHotel("reallife_travel_suggest_locked_out");   // locked out, evicted: where the nearest bed is
```

Without them: freshening up and laundry are a small mood once a day, rest is the relaxed modifier, the one companion is the mod's Friend contact, souvenirs go to Friend and Mom (a thank-you text and a mood each), nobody is refused travel, there is no coach, and the concierge's car and the golf button are not shown.

## Wish-list for the asset pipeline

Plain shapes stand in until these ids are in `Bundles/reallife_props`; the code asks `PropLibrary` first, so no code changes when they arrive.

| id | size (w x h x d) | pivot | notes |
|---|---|---|---|
| `hotel_sign` | 0.70 x 1.90 x 0.16 | ground centre, face to +Z | pavement sign against a wall; leave a plain plate 0.60 x 0.34 centred 1.50 m up on the +Z face (0.085 m in front of the pivot plane): the hotel's name is lettered there in code. As built the plate's face is at z 0.080 with nothing in front of it, so the lettering at 0.085 is 5 mm proud and does not flicker; its box is 0.56 x 0.30, 2 cm inside the plate all round |
| `hotel_awning` | 2.40 x 0.45 x 1.40 | the middle of its wall edge, at its underside | reaches out along +Z; hung 2.75 m up on a wall measured flat; one neutral colour (four tiers share it) or light canvas |
| `luggage_cart` | 1.10 x 1.75 x 0.60 | ground centre, long side along X | brass birdcage trolley with two cases and a bag on it |
| `suitcase` | 0.45 x 0.65 x 0.25 | ground centre, handle up | hard-shell case; and a grip for `carry_bag_walk` (right hand) so it can be carried: not used by code yet |

## Not done

- The suitcase is bought and changes what trips bring home, but is never seen: no prop in the hand on the way to the cab (`HeldTool` with `carry_bag_walk` needs the modelled case and its grip).
- No postcards or photographs are saved.
- The trade fair gives a mood only (+4 for 48 h): no supplier discount or marketing boost, because no mechanism of the game's was found that could be given one safely.
- Golf in the Hamptons and a coach there exist only through the Leisure and Transport bridges.
- The Trips page is a phone contact with pages, not an app icon on the home screen.
