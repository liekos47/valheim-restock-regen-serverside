# RestockRegen (working name)

Valheim's loot chests are one-shot. Once a crypt chest is emptied it stays empty for the life of the
world. This mod refills them: a loot chest that has sat empty for 30 in-game days gets a fresh roll
from its own loot table.

**Server-side only.** Install it on the dedicated server. Players install nothing.

## Status

0.1.0 is a read-only census and does not restock anything yet. On world load it logs how many loot
chests exist and how many are empty. Restocking comes in the next version.

## Rules

- Only chests whose prefab has a loot table are touched. Player-built chests, carts, ships,
  tombstones and cargo crates have none and are never touched.
- Only **empty** chests refill. A chest holding anything at all is left alone.
- Each chest has its own clock, which starts the day the server first sees it empty. Installing on
  an old world does not refill everything at once.
- A chest that a player currently has loaded is skipped and tried again later.

## Configuration

`BepInEx/config/liekos47.restockregen.cfg`, written on first run. The file is re-read every 30 seconds.

| Setting | Default | What it does |
| --- | --- | --- |
| `Enabled` | `true` | Off leaves the plugin loaded and doing nothing. |
| `Days` | `30` | In-game days a chest must stay empty before it refills. A Valheim day is 20 real minutes. |
| `Verbose` | `false` | Log one line per chest prefab and per decision. |

## Installation

Requires [BepInEx 5](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) on the server.
Put `RestockRegen.dll` in `BepInEx/plugins/`.

## Credits

The idea of restocking dungeons on a timer comes from [Dvala](https://github.com/Ezomic/valheim-dvala)
by Ezomic. This is a separate, server-only implementation and contains no Dvala code.

## Licence

MIT.
