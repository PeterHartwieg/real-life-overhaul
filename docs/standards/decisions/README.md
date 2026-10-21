# Architecture decision records

An architecture decision record (ADR) says why the code is built the way it is: the situation, the decision, and what follows from it. A rule in the standard says what to do; its ADR says why, so that a later change can weigh the same reasons.

| ADR | Decision | Rules |
| --- | --- | --- |
| [0001](0001-no-harmony.md) | No Harmony patches | RLO-ARC-011, RLO-ARC-012 |
| [0002](0002-game-ui-only.md) | The game's own interface only | RLO-ARC-013 |
| [0003](0003-save-under-reallife-keys.md) | Per-save state under `reallife:` keys | RLO-ARC-014, RLO-ARC-015 |
| [0004](0004-game-free-rules.md) | Game-free rules classes, checked offline | RLO-ARC-002, RLO-TST-001 |
| [0005](0005-rlo-texts-local-model.md) | RLO Texts runs a small text model on the player's own PC, offline | RLO-PERF-001, RLO-PERF-011, RLO-MP-004, RLO-ARC-009 |
| [0006](0006-your-own-ai.md) | Your own AI: RLO Texts' cards may go to an AI service the player chooses | RLO-PERF-001, RLO-MP-004, RLO-MP-007, RLO-ARC-009, RLO-ARC-012 |
| [0007](0007-a-read-api-for-other-mods.md) | A read API for other mods | RLO-ARC-002, RLO-ARC-005, RLO-ARC-007, RLO-ARC-009, RLO-PERF-003, RLO-MP-004, RLO-TST-001 |

## Adding a record

1. Copy [0000-template.md](0000-template.md) to the next number, `NNNN-short-title.md`.
2. Fill it in and keep it to one screen.
3. Open a pull request labelled `standard`, with the rules it adds or changes.

A record is never deleted. When a decision is replaced, set the old record's status to "Replaced by NNNN" and write the new one.
