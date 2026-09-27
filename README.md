# RestockRegen (working name)

Valheim's loot chests are one-shot. Once a crypt chest is emptied it stays empty for the life of the
world. This mod refills them: a loot chest that has sat empty for 30 in-game days gets a fresh roll
from its own loot table.

**Server-side only.** Install it on the dedicated server. Players install nothing.

## Status

On world load the server logs a census of loot chests. After that it sweeps once per in-game day,
which covers normal dawn, sleeping through the night, and a server running with nobody online. One
sweep over a world of about 1.4 million objects takes well under a tenth of a second.

## Rules

- Hildir's three quest chests are excluded by default, so her quest items are not handed out again.
- Only chests whose prefab has a loot table are touched. Player-built chests, carts, ships,
  tombstones and cargo crates have none and are never touched.
- Only **empty** chests refill. A chest holding anything at all is left alone.
- Each chest has its own clock, which starts the day the server first sees it empty. Installing on
  an old world does not refill everything at once.
- A chest that a player currently has loaded is skipped and tried again at the next day's sweep.
- A chest that is destroyed (smashed or burnt) is gone from the world and cannot come back.

## Configuration

`BepInEx/config/liekos47.restockregen.cfg`, written on first run. The file is re-read every 30 seconds.

| Setting | Default | What it does |
| --- | --- | --- |
| `Enabled` | `true` | Off leaves the plugin loaded and doing nothing. |
| `Days` | `30` | In-game days a chest must stay empty before it refills. A Valheim day is 20 real minutes. |
| `Verbose` | `false` | Log one line per chest prefab and per restocked chest. |
| `DryRun` | `false` | Count and log what would happen, but write nothing to the world. |
| `Exclude` | Hildir's three quest chests | Comma-separated chest prefab names never to restock. Needs a restart. |

The clock is stored on each chest as `restockregen_emptysince`. Removing the mod leaves that value
behind, where the game ignores it.

## Installation

Requires [BepInEx 5](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) on the server.
Put `RestockRegen.dll` in `BepInEx/plugins/`.

## Credits

The idea of restocking dungeons on a timer comes from [Dvala](https://github.com/Ezomic/valheim-dvala)
by Ezomic. This is a separate, server-only implementation and contains no Dvala code.

## Licence

MIT.
