# RestockRegen (working name)

Valheim's loot chests are one-shot. Once a crypt chest is looted it stays that way for the life of
the world. This mod refills them: 30 in-game days after a player first opens a loot chest, whatever
is inside is replaced with a fresh roll from its own loot table.

**Server-side only.** Install it on the dedicated server. Players install nothing.

## Status

On world load the server logs a census of loot chests. After that it sweeps once per in-game day,
which covers normal dawn, sleeping through the night, and a server running with nobody online. One
sweep over a world of about 1.4 million objects takes well under a tenth of a second.

## Rules

- Hildir's three quest chests are excluded by default, so her quest items are not handed out again.
- Each loot chest remembers who first opened it and who opened it last. The next player to open it
  sees, top-left, "Last opened by Bjorn 3 days ago, first by Astrid 12 days ago". Recording starts
  when the mod is installed.
- Only chests whose prefab has a loot table are touched. Player-built chests, carts, ships,
  tombstones and cargo crates have none and are never touched.
- A chest's clock starts the first time a player opens it (or the server finds it empty). 30 days
  later it resets, **leftovers included**: anything still inside, loot or junk a player dropped in,
  is replaced. Do not use dungeon chests for storage. Set `ResetOpened` to false to reset only
  completely empty chests instead.
- A chest nobody has opened keeps its original loot and is never touched.
- Each chest has its own clock. A player who
  opens an empty loot chest, or empties one, is told on screen how many days are left. Installing on
  an old world does not refill everything at once.
- A chest that a player currently has loaded is skipped and tried again at the next day's sweep.
- A chest that is destroyed (smashed or burnt) is gone from the world and cannot come back.

## Configuration

`BepInEx/config/liekos47.restockregen.cfg`, written on first run. The file is re-read every 30 seconds.

| Setting | Default | What it does |
| --- | --- | --- |
| `Enabled` | `true` | Off leaves the plugin loaded and doing nothing. |
| `Days` | `30` | In-game days from a chest's first opening (or being found empty) until it resets. A Valheim day is 20 real minutes. |
| `Verbose` | `false` | Log one line per chest prefab and per restocked chest. |
| `DryRun` | `false` | Count and log what would happen, but write nothing to the world. |
| `Notify` | `true` | Tell a player on screen when the loot chest they opened is empty, and when it restocks. |
| `NotifyText` | `The spirits will refill this chest in {days}` | The message. `{days}` becomes "1 day" or "N days". |
| `NotifyDueText` | `The spirits will refill this chest at the next dawn, once no one is near` | The message when the time is already up. |
| `History` | `true` | Remember who first and last opened each loot chest, and tell the next player who opens it. |
| `HistoryText` | `Last opened by {last} {lastago}, first by {first} {firstago}` | The history message (top-left). Names are character names; "you" for the reader. |
| `HistoryTextOnce` | `Last opened by {last} {lastago}` | Used when only one opening is recorded. |
| `ResetOpened` | `true` | Reset a chest 30 days after it is first opened, even with leftovers inside. Off: only empty chests reset. |
| `NotifyLeftoversText` | `The spirits will refill this chest in {days}. Anything left inside will be lost` | The message when the chest still has items in it. |
| `Exclude` | Hildir's three quest chests | Comma-separated chest prefab names never to restock. Needs a restart. |

The clock is stored on each chest as `restockregen_emptysince`, and the history as `restockregen_firstby`,
`restockregen_firstday`, `restockregen_lastby` and `restockregen_lastday`. Removing the mod leaves these
behind, where the game ignores them.

## Installation

Requires [BepInEx 5](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) on the server.
Put `RestockRegen.dll` in `BepInEx/plugins/`.

## Credits

The idea of restocking dungeons on a timer comes from [Dvala](https://github.com/Ezomic/valheim-dvala)
by Ezomic. This is a separate, server-only implementation and contains no Dvala code.

## Licence

MIT.
