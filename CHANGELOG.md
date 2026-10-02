## 0.12.0

- Black cores in infested mines come back. A taken core's stand stays in the world marked as taken
  and the game never resets it (39 of 172 on the owner's world). A mine's cores are now restored
  once nobody has been inside it for 30 in-game days, like mud piles and crystals, with the same
  entrance and inside messages. Restoring is clearing the stand's taken mark, so nothing is created
  and cores taken before the mod was installed come back too. `/kick restock-regen-cores` and
  `[InfestedMines]` settings.
- **Dragon eggs removed.** 0.10.0 added a dragon egg group on the belief that taking an egg deletes
  it. That was wrong: the egg stays, marked as taken, and the game un-takes it on its own timer
  (the owner's save holds eggs that have already come back). The group could never have done
  anything, it only acted on eggs that had vanished, and none do. The `[DragonEgg]` settings and
  `/kick restock-regen-eggs` are gone, and the Mountains message now mentions obsidian only. Egg
  records are dropped from the record file.
- The start-up log now says, for each tracked pickable, what the game itself does when it is picked
  (deleted or kept) and its respawn time, so a wrong assumption like the one above shows up at once.

## 0.11.0

- Frost cave crystals come back. The game deletes a crystal when it is picked and never restores
  it (32 picked on the owner's world between 2026-09-18 and 09-25 had not returned by 09-29). A
  frost cave's crystals now regenerate once nobody has been inside it for 30 in-game days, exactly
  like the sunken crypts' mud piles, with the same entrance and inside messages.
- `/kick restock-regen-crystals`, and `[FrostCaves]` settings.
- The mud pile code became a shared dungeon regrow engine for both. Mud pile settings, stored data
  and behaviour are unchanged.

## 0.10.0

- Obsidian deposits and dragon eggs in the Mountains come back like ancient armor: once no player
  has been within 100 m for 30 in-game days, and never where a player has built within 8 m. A
  partly mined deposit is replaced by a whole one; a mined-out deposit or a taken egg is put back.
  Each has its own settings (`[Obsidian]`, `[DragonEgg]`). Restoring eggs makes Moder repeatable.
- Entering the Mountains shows "The spirits will restore obsidian and dragon eggs after 30 days
  without visitors" (`[Mountains]` settings).
- `/kick restock-regen-obsidian` and `/kick restock-regen-eggs`.
- The ancient armor code became a shared "regrow" module for all three kinds. Settings and the
  record file are unchanged.

## 0.9.1

- Fixed ancient armor records counting a piece twice. Positions were written to the record file
  with too few decimals, so a piece on a rounding edge read back as a separate spot that looked
  mined out, and would have been doubled once due. Positions are now written in full, matched with
  a 20 cm tolerance, and duplicates an older build wrote are dropped when the file is read. Found in
  dry run on the live server, before anything was restored.

## 0.9.0

- Ancient armor in the Mistlands comes back. A mined giant helmet or sword (`giant_helmet1/2`,
  `giant_sword1/2`) is restored once no player has been within 100 m of it for 30 in-game days,
  and never where anything a player built stands within 8 m of the spot. Spots are remembered from
  the first time the mod sees a piece, and kept with the area visits in
  `worlds_local/<world>.restockregen.txt` beside the world save.
- Entering the Mistlands shows "The spirits will restore ancient armor after 30 days without
  visitors", at most once every 10 minutes per player.
- `/kick restock-regen-armor` restores every free mined piece now.
- New `[AncientArmor]` settings: `AncientArmor`, `AncientArmorDays`, `AncientArmorDryRun`,
  `AncientArmorVisitRadius`, `AncientArmorBuildClearance`, `AncientArmorNotifyText`,
  `AncientArmorNotifyCooldown`.
- README rewritten as a full guide, with a detailed admin command section.
- The DLL carries its debug symbols inside it. ScriptEngine reads symbols when it loads a plugin, and
  refused the first 0.9.0 build, which had them in a separate file.

## 0.8.0

- Admin commands, typed in the chat box as `/kick restock-<command>` (or `kick restock-<command>`
  in the F5 console): `restock-help`, `restock-status`, `restock-reload`, `restock-regen`,
  `restock-get:Setting`, `restock-set:Setting=value`. They ride on Valheim's own kick command,
  the one console command that reaches the server with its text, and are answered in the admin's
  console and top-left. The server checks adminlist.txt; a real kick is unaffected.
- Hot reload with BepInEx ScriptEngine: with RestockRegen in `BepInEx/scripts`, replacing the DLL
  or `restock-reload` loads the new version without a server restart.
- Each load patches under its own Harmony id, so an unloading copy cannot remove the new copy's
  patches, and pending records are written out on unload.

## 0.7.0

- Chests near a base restock too. A chest a player has loaded used to wait until nobody was near,
  so chests close to a base, loaded almost all the time, never came back (33-38 of them a day on
  the owner's server). Now they are restocked while loaded, as long as nobody has the chest open.
  `RestockLoaded` (on) controls it.
- `MudPileRegenNow`: a one-shot switch that regenerates every sunken crypt with mined piles at the
  next daily sweep, whatever its clock, then turns itself off. A crypt someone is in at that moment
  is done as soon as it is free.
- Mud pile regeneration went live on the owner's server with 30 days, after a dry run in which
  47-50 crypts a day came up due with a 1-day test setting.

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
