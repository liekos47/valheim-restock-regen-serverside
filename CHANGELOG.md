## 0.6.0

- Muddy scrap piles in sunken crypts regenerate. A sunken crypt that no player has visited for
  `MudPileDays` (30) in-game days gets every mined pile back: partly mined piles are replaced by
  whole ones and mined-out spots get a pile again. Any visit restarts the count.
- The mod remembers each pile's spot from the first time it sees the pile, whole or partly mined,
  and keeps the list on the crypt itself. A pile mined out completely before the mod saw it cannot
  come back. On the owner's world none had been: every mined pile still left its broken remains.
- Arriving at the entrance of a crypt with mined piles (within 30 m) shows the days left: "The
  spirits will restore muddy scrap piles in 12 days if no one enters". Going in anyway restarts
  the count and shows it again with the full 30 days: "The spirits will restore muddy scrap piles after
  30 days without visitors" (`MudPileNotifyText`).
- New settings in `[MudPiles]`: `MudPiles`, `MudPileDays`, `MudPileDryRun`, `CryptRadius`,
  `MudPileNotifyText`, `MudPileEntranceText`, `MudPileDueText`, `VisitCheckSeconds` (30),
  `EntranceRadius` (30). Chests are unchanged.
- Documentation: in-game days only pass on a dedicated server while a player is online, so all
  the 30-day counts, chests included, are about 10 hours of play, not of server uptime.
- This is the first feature that creates and removes world objects. A fault in it switches it off
  until the next restart and leaves chest restocking running.

## 0.5.0

- Leftovers reset too. A loot chest now resets 30 days after a player first opens it, whether it
  was emptied or not: whatever is inside at that point, loot or junk, is replaced by a fresh roll.
  Agreed with the players. `ResetOpened` (on) controls it; off gives the old rule, where only
  completely empty chests restock and putting anything back in stops the clock.
- The on-screen notice now shows on every opening of a loot chest. With items inside it warns:
  "The spirits will refill this chest in 30 days. Anything left inside will be lost"
  (`NotifyLeftoversText`).
- Chests opened before this version still hold their leftovers until someone opens them again;
  that opening starts their clock.
- The daily log line says "clocks started" instead of "newly empty", and each restock line says
  how many stacks it replaced.

## 0.4.0

- On-screen notice. When a player opens an empty loot chest, or takes the last item out of one,
  the middle of their screen says when it restocks: "The spirits will refill this chest in 30 days". Still
  server-side only: the server sees the chest's in-use flag and new owner in the player's normal
  update and sends the message through the game's own ShowMessage.
- The countdown starts from the moment a player was seen emptying the chest, not from the next
  daily sweep, so the number on screen is the one that runs. That moment is kept in memory until
  the sweep stamps the chest; after a restart the sweep's own day is used.
- Settings `Notify`, `NotifyText` (with `{days}`) and `NotifyDueText`.
- Chest history. Each loot chest remembers who first opened it and who opened it last, with the
  in-game day, and the next player to open it is told in the top-left: "Last opened by Bjorn
  3 days ago, first by Astrid 12 days ago". Settings `History`, `HistoryText`, `HistoryTextOnce`.
  Recording starts with this version; earlier openings are not known.
- History is written to the chest only once no player holds it: the server drops a player's update
  to an object whose revision it has already moved past, so writing to a chest in use could make
  the player's next change to it, such as taking an item, get lost.

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
