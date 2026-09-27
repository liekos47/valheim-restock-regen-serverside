## 0.3.0

- Hildir's three quest chests (`TreasureChest_forestcrypt_hildir`, `TreasureChest_mountaincave_hildir`,
  `TreasureChest_plainsfortress_hildir`) are never restocked. They each hold one of her quest items.
  The list is the new `Exclude` setting.
- The census no longer logs a line for every chest prefab with a building-piece component; nearly
  all world chests have one. A chest that deletes itself when emptied is still reported, as a warning.
- Confirmed on a live dry run: no loot chest in l-1.0.16 deletes itself when emptied, buried
  treasure included, and chests a player has loaded are skipped.

## 0.2.0

- Restocking. Once per in-game day the server sweeps every loot chest in the world:
  an empty chest is stamped with the day it was first seen empty, and once it has stayed empty for
  `Days` (30) days it gets a fresh roll from its own loot table. A chest that gets anything put back
  in loses its stamp.
- Chests a player currently has loaded are skipped and retried at the next sweep.
- `DryRun` setting: count and log only, write nothing. Its empty-since dates live in memory, so a dry run still reaches "would restock".
- The census now always names any loot chest prefab that deletes itself when emptied, or that has
  a building-piece component.

## 0.1.0

- Read-only census. Once the world is loaded, the server logs every loot chest prefab and how many
  chests of each are in the world, empty, holding items, never loaded, or player-built. Nothing is
  written to the world.
- Built against Valheim l-1.0.16.
