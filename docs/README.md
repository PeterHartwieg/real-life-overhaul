# Real Life Overhaul developer documentation

Start at the [front door](../RealLifeOverhaul.Build/README.md) if you are new: it has the folder map and the steps to build. Every page is listed here, one line each. The pages are kept by the Real Life Overhaul maintainer; if one is wrong, say so in an issue or fix it in a pull request.

## Contributing

- [Getting started](contributing/getting-started.md): clone, build, run the checks, try your build in the game, and good first tasks.
- [The rules for new code](contributing/rules.md): the short numbered list every pull request is held to.
- [Checks](contributing/checks.md): each checker, what it proves, how to run it and how to read a failure.
- [Texts and translation](contributing/texts-and-translation.md): where texts live, how to add a key, the languages.
- [Adding things](contributing/adding-things.md): a feature with its Guide page and presets, a crime, a living city activity, a job.
- [After a game update](contributing/after-a-game-update.md): bringing the mod up to a new game build.
- [Versions and releases](contributing/releases.md): how the maintainer numbers and ships a release.

## Standards

- [The engineering standard](standards/README.md): code style, architecture, performance, multiplayer, testing, texts, comments, git and review.

## Reference

- [City reads for other mods](reference/mod-api.md): the versioned public contract for citizens, reservations and slowed lanes.
- [Logs and debugging](reference/logs-and-debugging.md): where `Player.log` is, what the mod writes there, and how to report a bug.
- [Architecture](reference/architecture.md): start-up, the director pattern, wiring, the save, the guard, per-frame rules.
- [Wiring](reference/wiring.md): every bridge between features and who answers it.
- [The game's own interface](reference/game-interface.md): how the mod builds its screens from the game's.
- [Performance](reference/performance.md): the per-frame rules, what was measured, and the measuring tools.
- [Map places](reference/map-places.md): putting a feature's places on the city map.
- [The calendar and the price level](reference/calendar-and-prices.md): dates and every fixed amount.
- [Save keys, contact ids and key bindings](reference/keys.md): what the mod puts in a save, and the keys it acts on.
- [Folder map](reference/folder-map.md): the mod's folder, `Scripts/Core`, and the folders outside the mod.
- [Glossary](reference/glossary.md): the mod's own words.

## Features

- [Every feature](features/README.md), one page each, grouped.

## Elsewhere in the repository

- [The game's interface, in depth](../RealLifeOverhaul.Build/docs/game-ui.md): read before any interface work.
- [Other mods](../RealLifeOverhaul.Build/docs/other-mods.md): what other authors' mods do and how this one stands aside for them.
- [The style sheet](../RealLifeOverhaul.Build/Workshop/STYLE.md): the rules for every text a player reads.
- [The asset pipelines](../CityAssets/README.md): clips in `CityAssets`, props in [`PropAssets`](../PropAssets/README.md), looks in [`CharacterAssets`](../CharacterAssets/README.md).
- [The mod SDK](SDK.md): Hovgaard Games' template this repository started from.
