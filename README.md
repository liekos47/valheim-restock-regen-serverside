# RestockRegen (working name)

A server-side mod for Valheim dedicated servers that brings the world's one-shot resources back
over time:

- **Loot chests** refill 30 in-game days after they are first opened.
- **Muddy scrap piles** in sunken crypts and **crystals** in frost caves come back after the
  dungeon has gone 30 days without visitors.
- **Ancient armor** (the giant's helmets and swords in the Mistlands), **obsidian** and **dragon
  eggs** (Mountains) come back where nobody has been for 30 days.

**Server-side only.** Install it on the dedicated server. Players install nothing, and everything
they see arrives through the game's own messages.

> **Days are played time.** On a dedicated server, in-game time only moves while at least one
> player is online. A Valheim day is 20 minutes, so 30 in-game days is about 10 hours with someone
> on the server, not 10 hours of the server running.

## Contents

- [What players see](#what-players-see)
- [Loot chests](#loot-chests)
- [Muddy scrap piles](#muddy-scrap-piles)
- [Frost cave crystals](#frost-cave-crystals)
- [Ancient armor, obsidian and dragon eggs](#ancient-armor-obsidian-and-dragon-eggs)
- [Admin commands](#admin-commands)
- [Settings](#settings)
- [Hot reload](#hot-reload)
- [Installation](#installation)
- [What the mod stores](#what-the-mod-stores)

## What players see

| When | Where | Message (all can be changed in the settings) |
| --- | --- | --- |
| Opening an empty loot chest, or taking the last item out | Middle of the screen | The spirits will refill this chest in 30 days |
| Opening a loot chest that still has items | Middle of the screen | The spirits will refill this chest in 30 days. Anything left inside will be lost |
| Opening a loot chest whose time is up | Middle of the screen | The spirits will refill this chest at the next dawn, once no one is near |
| Opening a loot chest someone opened before | Top left | Last opened by Bjorn 3 days ago, first by Astrid 12 days ago |
| Arriving at the entrance of a sunken crypt with mined piles | Middle of the screen | The spirits will restore muddy scrap piles in 12 days if no one enters |
| Going inside that crypt | Middle of the screen | The spirits will restore muddy scrap piles after 30 days without visitors |
| Arriving at the entrance of a frost cave with picked crystals | Middle of the screen | The spirits will restore the crystals in 12 days if no one enters |
| Going inside that cave | Middle of the screen | The spirits will restore the crystals after 30 days without visitors |
| Entering the Mistlands | Middle of the screen | The spirits will restore ancient armor after 30 days without visitors |
| Entering the Mountains | Middle of the screen | The spirits will restore obsidian and dragon eggs after 30 days without visitors |

## Loot chests

- Only chests whose kind has a loot table: crypt chests, camp chests, shipwrecks, buried treasure,
  Dvergr and Ashlands chests and so on. Player-built chests, carts, ships, tombstones and cargo
  crates have no loot table and are never touched.
- A chest's clock starts the first time a player opens it (or the server finds it empty). 30 days
  later whatever is inside, loot or junk a player dropped in, is replaced with a fresh roll from its
  own loot table. **Do not use dungeon or world chests for storage.**
- A chest nobody has opened keeps its original loot.
- A chest next to a base, which is loaded nearly all the time, still refills, as long as nobody has
  that chest open at the moment the daily check runs.
- Hildir's three quest chests are never refilled, so her quest items are not handed out twice.
- A chest that has been destroyed (smashed or burnt) is gone from the world and cannot come back.
- Installing the mod on an old world does not refill everything at once: the clocks start when the
  mod first sees each chest.

## Muddy scrap piles

- Sunken crypts only.
- A crypt's piles come back once nobody has been **inside** it for 30 in-game days. Any visit starts
  the count again, so a crypt people keep going back to stays as it is. Standing at the entrance
  does not count as a visit.
- Mining a pile replaces it with a broken pile, and breaking every chunk of that deletes it. The mod
  remembers every pile's spot from the first time it sees it, whole or broken, so both come back.
  A pile mined out completely before the mod was installed left nothing behind and cannot come
  back.
- A crypt with a player in it, or next to it, waits until they leave.

## Frost cave crystals

- The crystals growing in the Mountains' frost caves (`Pickable_MountainCaveCrystal`). The game
  never brings them back: picking one deletes it.
- They work exactly like the sunken crypts' mud piles: a cave's crystals come back once nobody has
  been **inside** it for 30 in-game days, any visit starts the count again, and standing at the
  entrance does not count.
- Every crystal is remembered from the first time the mod sees it. Crystals picked before the mod
  was installed cannot come back.
- A cave with a player in it, or next to it, waits until they leave.
- Players get the same two messages as at a crypt: the days left at the entrance, and the full 30
  days once inside.

## Ancient armor, obsidian and dragon eggs

Three kinds of world object that work the same way, each with its own settings:

| Kind | Where | Objects |
| --- | --- | --- |
| Ancient armor | Mistlands | The giant's helmets and swords (`giant_helmet1`, `giant_helmet2`, `giant_sword1`, `giant_sword2`) |
| Obsidian | Mountains | Obsidian deposits (`MineRock_Obsidian`) |
| Dragon eggs | Mountains | The eggs on the dragon nests (`Pickable_DragonEgg`) |

- A partly mined or missing object comes back once no player has been within 100 m of its spot for
  30 in-game days. Any visit starts the count again for that area.
- **Nothing is put back where a player has built.** If anything a player built stands within 8 m of
  the spot, it stays gone and the spot is checked again every day.
- An object is remembered from the first time the mod sees it, whole or damaged. Objects mined out
  or taken before the mod was installed cannot come back.
- Players entering the Mistlands or the Mountains are told about it, at most once every 10 minutes.
- **Dragon eggs make Moder repeatable:** with eggs coming back, players can summon her again. Set
  `DragonEgg` to false if you would rather she stays a one-time fight.

## Admin commands

A server-only mod cannot add its own console or chat commands: the console runs on each player's
own computer, and plain chat on Steam is sent straight to the other players. The one console
command that sends its text to the server is Valheim's `kick`, so the commands ride on it.

**How to type them**

- In the **chat box**: `/kick restock-help`
- In the **F5 console**: `kick restock-help`

**Who can use them:** players listed in the server's `adminlist.txt` (the same list that makes
someone an admin for `kick` and `ban`). Anyone else is told the commands are for server admins.
The answer appears in your F5 console and at the top left of your screen. A normal kick,
`/kick PlayerName`, still works as before.

**One word only.** Only the first word after `kick` reaches the server, so every command is a
single word with no spaces.

### Command list

| Command | What it does |
| --- | --- |
| `/kick restock-help` | Lists the commands and the name of every setting. |
| `/kick restock-status` | Mod version, the in-game day, how many chest kinds, crypts, pile spots and armor spots it tracks, and whether anything is in dry run. |
| `/kick restock-get:Setting` | Shows one setting. Example: `/kick restock-get:Days` |
| `/kick restock-set:Setting=value` | Changes a setting and saves it to the config file. It takes effect straight away. Examples below. |
| `/kick restock-regen` | Regenerates every sunken crypt with mined piles **now**, whatever its clock. A crypt with a player in or near it is done at the next daily check that finds it free. |
| `/kick restock-regen-armor` | Restores every mined ancient armor piece **now**, whatever its clock, except where a player is near or has built. |
| `/kick restock-regen-obsidian` | The same for obsidian deposits. |
| `/kick restock-regen-eggs` | The same for dragon eggs. |
| `/kick restock-regen-crystals` | Regenerates every frost cave with picked crystals **now**, like `restock-regen` does for crypts. |
| `/kick restock-reload` | Reloads the mod from `BepInEx/scripts` without restarting the server ([hot reload](#hot-reload)). `/kick reload-restock-regen` does the same. |

### Changing settings with restock-set

The setting name is not case-sensitive. The value is written the same way as in the config file:
`true` or `false`, a whole number, or a decimal number.

```
/kick restock-set:Days=20
/kick restock-set:MudPileDays=45
/kick restock-set:AncientArmorDryRun=false
/kick restock-set:Notify=false
```

For text settings, type `_` wherever you want a space, because a command cannot contain spaces:

```
/kick restock-set:NotifyText=The_chest_will_refill_in_{days}
```

Every setting is listed under [Settings](#settings). `Exclude` needs a server restart to take
effect; everything else applies within 30 seconds.

## Settings

The file is `BepInEx/config/liekos47.restockregen.cfg`. It is written on the first run, read again
every 30 seconds, and can also be changed in game with `/kick restock-set`.

### General (chests)

| Setting | Default | What it does |
| --- | --- | --- |
| `Enabled` | `true` | Off leaves the mod loaded and doing nothing. |
| `Days` | `30` | In-game days from a chest's first opening (or being found empty) until it refills. |
| `ResetOpened` | `true` | Refill a chest 30 days after it is first opened, even with items left in it. Off: only completely empty chests refill, and putting anything back in stops the clock. |
| `RestockLoaded` | `true` | Refill chests a player has loaded (near a base, for example) as long as nobody has them open. Off: wait until no player is near. |
| `DryRun` | `false` | Chests: log what would happen, change nothing. |
| `Verbose` | `false` | Log a line for every refilled chest, restored pile or piece, and message sent. |
| `Exclude` | Hildir's three quest chests | Chest kinds never to refill, comma-separated. Needs a restart. |

### Notify (chest messages)

| Setting | Default | What it does |
| --- | --- | --- |
| `Notify` | `true` | Show the on-screen messages. Off turns off every message in the table under [What players see](#what-players-see) except the chest history. |
| `NotifyText` | `The spirits will refill this chest in {days}` | Opening an empty chest. `{days}` becomes "1 day" or "N days". |
| `NotifyLeftoversText` | `The spirits will refill this chest in {days}. Anything left inside will be lost` | Opening a chest with items in it. |
| `NotifyDueText` | `The spirits will refill this chest at the next dawn, once no one is near` | Opening a chest whose time is up. |

### History

| Setting | Default | What it does |
| --- | --- | --- |
| `History` | `true` | Remember who first and last opened each loot chest, and tell the next player who opens it. |
| `HistoryText` | `Last opened by {last} {lastago}, first by {first} {firstago}` | `{last}` and `{first}` are character names ("you" for the reader); `{lastago}` and `{firstago}` are "today", "yesterday" or "N days ago". |
| `HistoryTextOnce` | `Last opened by {last} {lastago}` | Used when only one opening has been recorded. |

### MudPiles

| Setting | Default | What it does |
| --- | --- | --- |
| `MudPiles` | `true` | Regenerate muddy scrap piles in sunken crypts. |
| `MudPileDays` | `30` | In-game days without anyone inside before a crypt's piles come back. |
| `MudPileDryRun` | `false` | Log which crypts would regenerate, change nothing. |
| `MudPileRegenNow` | `false` | One-shot: every crypt with mined piles regenerates at the next daily check, then this switches itself off. `/kick restock-regen` does it immediately instead. |
| `MudPileEntranceText` | `The spirits will restore muddy scrap piles in {days} if no one enters` | At a crypt's entrance, with the days left. |
| `MudPileNotifyText` | `The spirits will restore muddy scrap piles after {days} without visitors` | Inside a crypt. |
| `MudPileDueText` | `The spirits will restore muddy scrap piles at the next dawn if no one enters` | At the entrance when the time is up. |
| `VisitCheckSeconds` | `30` | How often, in real seconds, players are checked for crypt visits, Mistlands visits and messages. A visit shorter than this can go unnoticed. |
| `EntranceRadius` | `30` | Metres around a crypt's entrance where a player gets the entrance message. |
| `CryptRadius` | `200` | Metres from a crypt's centre that count as inside it. |

### FrostCaves

| Setting | Default | What it does |
| --- | --- | --- |
| `FrostCaveCrystals` | `true` | Restore the crystals in frost caves. |
| `CrystalDays` | `30` | In-game days without anyone inside before a cave's crystals come back. |
| `CrystalDryRun` | `false` | Log which caves would regenerate, change nothing. |
| `CrystalRegenNow` | `false` | One-shot: every cave with picked crystals regenerates at the next daily check, then this switches itself off. `/kick restock-regen-crystals` does it immediately instead. |
| `CrystalEntranceText` | `The spirits will restore the crystals in {days} if no one enters` | At a cave's entrance, with the days left. |
| `CrystalNotifyText` | `The spirits will restore the crystals after {days} without visitors` | Inside a cave. |
| `CrystalDueText` | `The spirits will restore the crystals at the next dawn if no one enters` | At the entrance when the time is up. |
| `CaveEntranceRadius` | `50` | Metres around a cave's centre where a player on the surface gets the entrance message. |
| `CaveRadius` | `200` | Metres from a cave's centre that count as inside it. |

`VisitCheckSeconds` under MudPiles sets how often frost caves are checked too.

### AncientArmor

| Setting | Default | What it does |
| --- | --- | --- |
| `AncientArmor` | `true` | Restore ancient armor in the Mistlands. |
| `AncientArmorDays` | `30` | In-game days with no player within `AncientArmorVisitRadius` before a mined piece comes back. |
| `AncientArmorDryRun` | `false` | Log which pieces would come back, change nothing. Spots and visits are still recorded. |
| `AncientArmorVisitRadius` | `100` | Metres. A player this close counts as a visit, and no piece is restored with a player this close. |
| `AncientArmorBuildClearance` | `8` | Metres. No piece is restored if anything a player built is this close to its spot. `0` turns the check off. |
| `AncientArmorNotifyText` | `The spirits will restore ancient armor after {days} without visitors` | Shown on entering the Mistlands. |
| `AncientArmorNotifyCooldown` | `10` | Minutes. A player is told at most once in this long. |

### Obsidian

| Setting | Default | What it does |
| --- | --- | --- |
| `Obsidian` | `true` | Restore obsidian deposits in the Mountains. |
| `ObsidianDays` | `30` | In-game days with no player within `ObsidianVisitRadius` before a deposit comes back. |
| `ObsidianDryRun` | `false` | Log which deposits would come back, change nothing. |
| `ObsidianVisitRadius` | `100` | Metres. A player this close counts as a visit, and nothing is restored with a player this close. |
| `ObsidianBuildClearance` | `8` | Metres. No deposit is restored if anything a player built is this close. `0` turns the check off. |

### DragonEgg

| Setting | Default | What it does |
| --- | --- | --- |
| `DragonEgg` | `true` | Restore dragon eggs on the nests. This makes Moder repeatable. |
| `DragonEggDays` | `30` | In-game days with no player within `DragonEggVisitRadius` before an egg comes back. |
| `DragonEggDryRun` | `false` | Log which eggs would come back, change nothing. |
| `DragonEggVisitRadius` | `100` | Metres. A player this close counts as a visit, and nothing is restored with a player this close. |
| `DragonEggBuildClearance` | `8` | Metres. No egg is restored if anything a player built is this close. `0` turns the check off. |

### Mountains

| Setting | Default | What it does |
| --- | --- | --- |
| `MountainsNotifyText` | `The spirits will restore obsidian and dragon eggs after {days} without visitors` | Shown on entering the Mountains while `Obsidian` or `DragonEgg` is on. `{days}` is `ObsidianDays`. |
| `MountainsNotifyCooldown` | `10` | Minutes. A player is told at most once in this long. |

## Hot reload

A new version of the mod can be loaded without restarting the server, using
[ScriptEngine](https://github.com/BepInEx/BepInEx.Debug) from the BepInEx developers:

1. Put `ScriptEngine.dll` in `BepInEx/plugins/`, and `RestockRegen.dll` in `BepInEx/scripts/`
   (not in `plugins/` as well).
2. In `BepInEx/config/com.bepis.bepinex.scriptengine.cfg` set `LoadOnStart = true`, and
   `EnableFileSystemWatcher = true` if replacing the DLL should reload it automatically.
3. To load a new version, **back up the world first**, then replace `BepInEx/scripts/RestockRegen.dll`,
   or type `/kick restock-reload`.

Settings never need a reload; they are read every 30 seconds. Each reload leaves the old copy in
memory (a few tens of kilobytes), so restart the server now and then.

## Installation

Requires [BepInEx 5](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) on the
dedicated server. Put `RestockRegen.dll` in `BepInEx/plugins/`, or in `BepInEx/scripts/` for
[hot reload](#hot-reload). Start the server once to create the config file.

## What the mod stores

- On each loot chest: its clock (`restockregen_emptysince`) and history (`restockregen_firstby`,
  `restockregen_firstday`, `restockregen_lastby`, `restockregen_lastday`).
- On each sunken crypt: its pile spots (`restockregen_mudspots`) and last visit
  (`restockregen_lastvisit`).
- On each frost cave: its crystal spots (`restockregen_crystalspots`) and last visit
  (`restockregen_cavevisit`).
- Beside the world save: `worlds_local/<world>.restockregen.txt`, the ancient armor, obsidian and
  dragon egg spots, and when each area was last visited. Keep it with the world when you back it up or move it.

Removing the mod leaves these behind, and the game ignores them.

## Credits

The idea of restocking dungeons on a timer comes from [Dvala](https://github.com/Ezomic/valheim-dvala)
by Ezomic. This is a separate, server-only implementation and contains no Dvala code.

## Licence

MIT.
