# RestockRegen

[![Support me on Ko-fi](https://img.shields.io/badge/Ko--fi-Support%20the%20mod-FF5E5B?logo=ko-fi&logoColor=white)](https://ko-fi.com/liekos47)

![A player opening an empty loot chest, with the message "The spirits will refill this chest in 20 days" on screen](https://raw.githubusercontent.com/liekos47/valheim-restock-regen-serverside/main/screenshots/chest-refill-notification.png)

A server-side mod for Valheim dedicated servers, made for long-running multiplayer worlds. In
Valheim some things can be taken only once per world: a loot chest stays empty, a sunken crypt stays
mined out, the Queen stays dead. Players who join the server later find those places already
stripped by the players before them. This mod brings a **chosen list** of those one-time resources
back after a while, so latecomers get their share too.

**It does not regenerate every resource.** Only the things in the list below come back. Everything
else is left exactly as the game has it, for example copper, tin and silver deposits, trees and
rocks. Everything in the list except loot chests can also be turned off on its own in the
[settings](#settings).

What comes back:

- **Loot chests** refill 30 in-game days after they are first opened.
- **Muddy scrap piles** in sunken crypts, **crystals** in frost caves, **black cores** in
  infested mines, the **gems and coin piles** in the Deep North's Morkhalla dungeon, and the **nests
  and trash piles** in the Deep North's Holes come back after the dungeon has gone 30 days without
  visitors.
- **The Queen** comes back to a Mistlands infested citadel 30 days after the last visit, once she
  has been killed, and its sealed door closes again.
- **Ancient armor** (the giant's helmets and swords in the Mistlands), **obsidian** (Mountains),
  **flametal** (the spires in the Ashlands' lava) and **ice** (the Deep North's ice ponds and shore
  ice) come back where nobody has been for 30 days.

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
- [Black cores](#black-cores)
- [Morkhalla gems and coin piles](#morkhalla-gems-and-coin-piles)
- [The Hole](#the-hole)
- [The Queen's citadel](#the-queens-citadel)
- [Ancient armor, obsidian, flametal and ice](#ancient-armor-obsidian-flametal-and-ice)
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
| Arriving at the entrance of an infested mine with taken black cores | Middle of the screen | The spirits will restore the black cores in 12 days if no one enters |
| Going inside that mine | Middle of the screen | The spirits will restore the black cores after 30 days without visitors |
| Arriving at the entrance of the Morkhalla dungeon with gems or piles missing | Middle of the screen | The spirits will restore the gems and coin piles in 12 days if no one enters |
| Going inside it | Middle of the screen | The spirits will restore the gems and coin piles after 30 days without visitors |
| Arriving at the entrance of a Hole with broken nests or piles | Middle of the screen | The spirits will restore the nests and trash piles in 12 days if no one enters |
| Going inside that Hole | Middle of the screen | The spirits will restore the nests and trash piles after 30 days without visitors |
| Standing in front of the door of a citadel whose Queen has been killed | Middle of the screen | The spirits will bring the Queen back in 12 days if no one enters |
| Going inside that citadel | Middle of the screen | The spirits will bring the Queen back after 30 days without visitors |
| Entering the Mountains | Middle of the screen | The spirits will restore obsidian after 30 days without visitors |
| Entering the Ashlands | Middle of the screen | The spirits will restore flametal after 30 days without visitors |
| Entering the Deep North | Middle of the screen | The spirits will restore ice after 30 days without visitors |

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

## Black cores

- The black cores on their stands in the Mistlands' infested mines (`Pickable_BlackCoreStand`).
  The game never brings them back: a taken core's stand stays, marked as taken, for good.
- They follow the same rule as mud piles and crystals: a mine's cores come back once nobody has
  been **inside** it for 30 in-game days, any visit starts the count again, and standing at the
  entrance does not count.
- Because the stand stays in the world, **every taken core can come back, including ones taken
  before the mod was installed.** Nothing is created: the stand is simply marked as not taken.
- A mine with a player in it, or next to it, waits until they leave.
- Players get the same two messages as at a crypt: the days left at the entrance, and the full 30
  days once inside.
- The Queen's own lair is a separate dungeon with its own rule: see
  [The Queen's citadel](#the-queens-citadel).

## Morkhalla gems and coin piles

The dungeon inside the Deep North's castle. Four things in it are restored once nobody has been
**inside** for 30 in-game days (any visit starts the count again; standing at the entrance does not
count):

| What | Objects | What the game does when you take it | How it comes back |
| --- | --- | --- | --- |
| Gems in the statue eyes | `Morkhalla_Eye1` to `Eye4` (ancient gemstones), `Morkhalla_Eye5_gemstone` to `Eye7_gemstone` (ordinary gemstones) | The eye stays, marked as taken, for good | The mark is cleared. Gems taken before the mod was installed come back too. |
| Coin piles | `Morkhalla_Rubble1` to `Rubble4`, the rubble that drops ancient coins and grausten when broken | The pile is destroyed and deleted | A new pile is put back in the same spot |
| Treasure piles | `Pickable_MorkHallaTreasure` (one random ancient gemstone) | Deleted when taken | A new one is put back in the same spot |
| Black ice block | `BlackIce_Start`, the block that starts a jotun invasion when broken | Deleted when broken, so each dungeon can start only one invasion | A new one is put back in the same spot, so the invasion can be started again |

- Piles are remembered from the first time the mod sees them. One broken or taken before the mod
  was installed left nothing behind and cannot come back.
- The dungeon's chests are loot chests and refill under the [loot chest](#loot-chests) rules.
- Only the dungeon is covered. The treasure piles lying on the surface around the castle are not.
- Players get the same two messages as at a crypt: the days left at the entrance, and the full 30
  days once inside.

## The Hole

The Deep North's underground Holes. What the normal game does with each thing in them, read from
the game's own data:

| Thing | Gives | Comes back in the normal game? | With this mod |
| --- | --- | --- | --- |
| Glow worms (`Pickable_GlowWorm`) | Glow worm | Yes, about 4 hours after being picked | Left to the game |
| Creatures (Elaking) | Their drops | Yes, while their nest stands: one every 10 seconds, up to 5 nearby | Left to the game |
| Trash piles (`elaking_trashpile`) | Frostwood, iron scrap, silver necklaces, mold weapons | No, deleted when broken | **Restored** |
| Spawner nests (`Spawner_Hole`, `Spawner_Hole_double`) | The same loot when broken | No, deleted when broken, and its creatures stop coming | **Restored**, creatures included |
| Roots (`HoleRock_root1` and others) | Frostwood, half the time | No, deleted when chopped | Not restored: many block passages |

- A Hole's trash piles and nests come back once nobody has been **inside** it for 30 in-game days.
  Any visit starts the count again, and standing at the entrance does not count.
- They are remembered from the first time the mod sees them. One broken before the mod was
  installed left nothing behind and cannot come back.
- A Hole with a player in it, or next to it, waits until they leave.
- Players get the same two messages as at a crypt: the days left at the entrance, and the full 30
  days once inside.

## The Queen's citadel

The Mistlands' infested citadel, behind the sealed door. What the normal game does with each thing
in it, read from the game's own data:

| Thing | In the normal game | With this mod |
| --- | --- | --- |
| The Queen (`SeekerQueen`) | She is placed in her room once, when the citadel is generated. Killed, she never comes back by herself. | **Put back** in her place |
| The sealed door (`dungeon_queen_door`) | Opens with a Sealbreaker, which is not used up. Stays open until someone closes it. | **Closed** again, so it takes a Sealbreaker to get in |
| Seeker eggs (`SeekerEgg`, 277 in her room) | Deleted when they hatch or are broken | **Restored**, if the mod has seen them |
| Creep blocks (`blackmarble_creep_4x2x1` and four more) | Deleted when broken | **Restored**, if the mod has seen them |
| The Queen's seeker spawners (`TriggerSpawner_Seeker`) | They stay in place after the fight | Left to the game |

- A citadel is reset **only after its Queen has been killed**. While she is alive nothing in it is
  touched, however long it stands empty.
- Once she is dead, the citadel resets after nobody has been **inside** it for 30 in-game days.
  Any visit starts the count again. Standing in front of the door does not count.
- A Queen killed before the mod was installed comes back too: her place is worked out from the
  citadel itself, not remembered. For those citadels the 30 days start when the mod first runs.
- Eggs and creep blocks are remembered from the first time the mod sees them. Ones hatched or
  broken before the mod was installed left nothing behind and cannot come back.
- A citadel with a player inside it, or in front of its door, waits until they leave.
- A player in front of the door of a citadel whose Queen is dead is told how many days are left,
  and again, with the full 30 days, on going inside.
- The Queen gives her normal drops again each time. The game's own altar in her room still works
  as before.
- `CitadelCloseDoor` set to `false` leaves the door as the players left it.

## Ancient armor, obsidian, flametal and ice

Four kinds of world object that work the same way, each with its own settings:

| Kind | Where | Objects |
| --- | --- | --- |
| Ancient armor | Mistlands | The giant's helmets and swords (`giant_helmet1`, `giant_helmet2`, `giant_sword1`, `giant_sword2`) |
| Obsidian | Mountains | Obsidian deposits (`MineRock_Obsidian`) |
| Flametal | Ashlands | The flametal spires that rise out of the lava (`LeviathanLava`) |
| Ice | Deep North | The ice ponds (`IcePond_rock`) and the ice along the shore (`IceShore_1`). The floating ice on the sea is not restored: it drifts, so it has no spot to come back to. |

- A mined or missing object comes back once no player has been within 100 m of its spot for
  30 in-game days. Any visit starts the count again for that area.
- **Nothing is put back where a player has built.** If anything a player built stands within 8 m of
  the spot, it stays gone and the spot is checked again every day.
- An object is remembered from the first time the mod sees it, whole or damaged. Objects mined out
  or taken before the mod was installed cannot come back.
- Players entering the Mistlands, the Mountains or the Ashlands are told about it, at most once
  every 10 minutes.
- In the normal game a mined flametal spire stays as an empty husk for good, and now and then one
  sinks into the lava and is gone. Here a spire with any chunk mined, or one that has sunk, comes
  back whole.

Dragon eggs are not handled by the mod because they do not need to be: the game brings taken eggs
back by itself.

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
| `/kick restock-regen-flametal` | The same for flametal spires. |
| `/kick restock-regen-ice` | The same for ice ponds and shore ice. |
| `/kick restock-regen-crystals` | Regenerates every frost cave with picked crystals **now**, like `restock-regen` does for crypts. |
| `/kick restock-regen-cores` | Regenerates every infested mine with taken black cores **now**. |
| `/kick restock-regen-morkhalla` | Regenerates the Morkhalla dungeon's gems and coin piles **now**, if nobody is inside. |
| `/kick restock-regen-hole` | Regenerates every Hole's nests and trash piles **now**, except Holes with a player inside. |
| `/kick restock-regen-citadel` | Resets every infested citadel whose Queen is dead **now**, except citadels with a player inside or at the door. |

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

All settings live on the server, in `BepInEx/config/liekos47.restockregen.cfg`. Players have nothing
to configure.

### How to configure the server

1. Install the mod and start the server once. The mod writes the config file with every setting at
   its default.
2. Open `BepInEx/config/liekos47.restockregen.cfg` in a text editor. On a hosted server, use the
   host's file manager or SFTP.
3. Change the values you want and save the file. Each setting sits under a `[Section]` heading, as
   `Name = value`:

   ```
   [General]
   Days = 30

   [MudPiles]
   MudPiles = true
   MudPileDays = 30
   ```

4. There is no need to restart. The mod reads the file again every 30 seconds. The one exception is
   `Exclude`, which needs a server restart.

Admins can also change any setting from inside the game with `/kick restock-set:Setting=value`, see
[Changing settings with restock-set](#changing-settings-with-restock-set). That writes to the same
file.

Things most servers will want to look at:

- **How long things take to come back.** Every kind has its own `...Days` setting, 30 in-game days by
  default: `Days` for chests, `MudPileDays` for sunken crypts, `IceDays` for ice and so on. Days are
  played time, about 20 minutes each with someone online.
- **Turning a kind off.** Every kind except loot chests has its own on/off setting named after it,
  such as `MudPiles`, `Obsidian` or `Ice`. Set it to `false` and the mod leaves that resource alone.
  `Enabled = false` under `[General]` switches the whole mod off, chests included.
- **Trying it safely first.** Every kind has a `...DryRun` setting. With it on, the mod only writes
  to the server log what it would bring back and changes nothing in the world.
- **The on-screen messages.** `Notify = false` turns them off, and every message text can be
  rewritten.

The tables below list every setting, section by section.

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

### InfestedMines

| Setting | Default | What it does |
| --- | --- | --- |
| `BlackCores` | `true` | Restore the black cores in infested mines. |
| `BlackCoreDays` | `30` | In-game days without anyone inside before a mine's cores come back. |
| `BlackCoreDryRun` | `false` | Log which mines would regenerate, change nothing. |
| `BlackCoreRegenNow` | `false` | One-shot: every mine with taken cores regenerates at the next daily check, then this switches itself off. `/kick restock-regen-cores` does it immediately instead. |
| `BlackCoreEntranceText` | `The spirits will restore the black cores in {days} if no one enters` | At a mine's entrance, with the days left. |
| `BlackCoreNotifyText` | `The spirits will restore the black cores after {days} without visitors` | Inside a mine. |
| `BlackCoreDueText` | `The spirits will restore the black cores at the next dawn if no one enters` | At the entrance when the time is up. |
| `MineEntranceRadius` | `50` | Metres around a mine's centre where a player on the surface gets the entrance message. |
| `MineRadius` | `200` | Metres from a mine's centre that count as inside it. |

`VisitCheckSeconds` under MudPiles sets how often infested mines are checked too.

### Morkhalla

| Setting | Default | What it does |
| --- | --- | --- |
| `Morkhalla` | `true` | Restore the gems and coin piles in the Deep North's Morkhalla dungeon. |
| `MorkhallaDays` | `30` | In-game days without anyone inside before they come back. |
| `MorkhallaDryRun` | `false` | Log what would be restored, change nothing. |
| `MorkhallaRegenNow` | `false` | One-shot: the dungeon regenerates at the next daily check, then this switches itself off. `/kick restock-regen-morkhalla` does it immediately instead. |
| `MorkhallaEntranceText` | `The spirits will restore the gems and coin piles in {days} if no one enters` | At the dungeon's entrance, with the days left. |
| `MorkhallaNotifyText` | `The spirits will restore the gems and coin piles after {days} without visitors` | Inside the dungeon. |
| `MorkhallaDueText` | `The spirits will restore the gems and coin piles at the next dawn if no one enters` | At the entrance when the time is up. |
| `MorkhallaEntranceRadius` | `50` | Metres around the dungeon's centre where a player on the surface gets the entrance message. |
| `MorkhallaRadius` | `200` | Metres from the dungeon's centre that count as inside it. |

`VisitCheckSeconds` under MudPiles sets how often this dungeon is checked too.

### TheHole

| Setting | Default | What it does |
| --- | --- | --- |
| `TheHole` | `true` | Restore the trash piles and spawner nests in the Deep North's Holes. |
| `HoleDays` | `30` | In-game days without anyone inside before they come back. |
| `HoleDryRun` | `false` | Log which Holes would regenerate, change nothing. |
| `HoleRegenNow` | `false` | One-shot: every Hole with broken piles or nests regenerates at the next daily check, then this switches itself off. `/kick restock-regen-hole` does it immediately instead. |
| `HoleEntranceText` | `The spirits will restore the nests and trash piles in {days} if no one enters` | At a Hole's entrance, with the days left. |
| `HoleNotifyText` | `The spirits will restore the nests and trash piles after {days} without visitors` | Inside a Hole. |
| `HoleDueText` | `The spirits will restore the nests and trash piles at the next dawn if no one enters` | At the entrance when the time is up. |
| `HoleEntranceRadius` | `40` | Metres around a Hole's centre where a player on the surface gets the entrance message. |
| `HoleRadius` | `150` | Metres from a Hole's centre that count as inside it. |

`VisitCheckSeconds` under MudPiles sets how often Holes are checked too.

### Citadel

| Setting | Default | What it does |
| --- | --- | --- |
| `Citadel` | `true` | Reset an infested citadel whose Queen has been killed: the Queen, the door, the eggs and the creep blocks. |
| `CitadelDays` | `30` | In-game days without anyone inside, after the Queen's death, before it resets. |
| `CitadelDryRun` | `false` | Log which citadels would reset, change nothing. |
| `CitadelRegenNow` | `false` | One-shot: every citadel with a dead Queen resets at the next daily check, then this switches itself off. `/kick restock-regen-citadel` does it immediately instead. |
| `CitadelCloseDoor` | `true` | Close the sealed door when the citadel resets. |
| `CitadelEntranceText` | `The spirits will bring the Queen back in {days} if no one enters` | In front of the door, with the days left. |
| `CitadelNotifyText` | `The spirits will bring the Queen back after {days} without visitors` | Inside the citadel. |
| `CitadelDueText` | `The spirits will bring the Queen back at the next dawn if no one enters` | In front of the door when the time is up. |
| `CitadelEntranceRadius` | `30` | Metres around the sealed door where a player on the surface gets the entrance message. |
| `CitadelRadius` | `150` | Metres from a citadel's centre that count as inside it. |

`VisitCheckSeconds` under MudPiles sets how often citadels are checked too.

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

### Flametal

| Setting | Default | What it does |
| --- | --- | --- |
| `Flametal` | `true` | Restore flametal spires in the Ashlands. |
| `FlametalDays` | `30` | In-game days with no player within `FlametalVisitRadius` before a spire comes back. |
| `FlametalDryRun` | `false` | Log which spires would come back, change nothing. |
| `FlametalVisitRadius` | `100` | Metres. A player this close counts as a visit, and nothing is restored with a player this close. |
| `FlametalBuildClearance` | `8` | Metres. No spire is restored if anything a player built is this close. `0` turns the check off. |

### Ice

| Setting | Default | What it does |
| --- | --- | --- |
| `Ice` | `true` | Restore the ice ponds and the ice along the shore in the Deep North. |
| `IceDays` | `30` | In-game days with no player within `IceVisitRadius` before mined ice comes back. |
| `IceDryRun` | `false` | Log which ice would come back, change nothing. |
| `IceVisitRadius` | `100` | Metres. A player this close counts as a visit, and nothing is restored with a player this close. |
| `IceBuildClearance` | `8` | Metres. No ice is restored if anything a player built is this close. `0` turns the check off. |

### DeepNorth

| Setting | Default | What it does |
| --- | --- | --- |
| `DeepNorthNotifyText` | `The spirits will restore ice after {days} without visitors` | Shown on entering the Deep North while `Ice` is on. `{days}` is `IceDays`. |
| `DeepNorthNotifyCooldown` | `10` | Minutes. A player is told at most once in this long. |

### Ashlands

| Setting | Default | What it does |
| --- | --- | --- |
| `AshlandsNotifyText` | `The spirits will restore flametal after {days} without visitors` | Shown on entering the Ashlands while `Flametal` is on. `{days}` is `FlametalDays`. |
| `AshlandsNotifyCooldown` | `10` | Minutes. A player is told at most once in this long. |

### Mountains

| Setting | Default | What it does |
| --- | --- | --- |
| `MountainsNotifyText` | `The spirits will restore obsidian after {days} without visitors` | Shown on entering the Mountains while `Obsidian` is on. `{days}` is `ObsidianDays`. |
| `MountainsNotifyCooldown` | `10` | Minutes. A player is told at most once in this long. |

## Hot reload

The mod has no hot reload of its own. A new version can be loaded without restarting the server
with [ScriptEngine](https://github.com/BepInEx/BepInEx.Debug#scriptengine) from the BepInEx
developers, which the mod is built to work with:

1. Put `ScriptEngine.dll` in `BepInEx/plugins/`, and `RestockRegen.dll` in `BepInEx/scripts/`
   (not in `plugins/` as well).
2. In `BepInEx/config/com.bepis.bepinex.scriptengine.cfg` set `LoadOnStart = true` and
   `EnableFileSystemWatcher = true`.
3. To load a new version, **back up the world first**, then replace `BepInEx/scripts/RestockRegen.dll`.
   ScriptEngine reloads it a few seconds later.

Settings never need a reload; they are read every 30 seconds. Each reload leaves the old copy in
memory (a few tens of kilobytes), so restart the server now and then.

## Installation

Requires [BepInEx 5](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) on the
dedicated server. Put `RestockRegen.dll` in `BepInEx/plugins/`, or in `BepInEx/scripts/` for
[hot reload](#hot-reload). Start the server once to create the config file, then see
[How to configure the server](#how-to-configure-the-server).

## What the mod stores

- On each loot chest: its clock (`restockregen_emptysince`) and history (`restockregen_firstby`,
  `restockregen_firstday`, `restockregen_lastby`, `restockregen_lastday`).
- On each sunken crypt: its pile spots (`restockregen_mudspots`) and last visit
  (`restockregen_lastvisit`).
- On each frost cave: its crystal spots (`restockregen_crystalspots`) and last visit
  (`restockregen_cavevisit`).
- On each infested mine: its black core spots (`restockregen_corespots`) and last visit
  (`restockregen_minevisit`).
- On the Morkhalla dungeon: its gem and pile spots (`restockregen_morkspots`) and last visit
  (`restockregen_morkvisit`).
- On each Hole: its nest and trash pile spots (`restockregen_holespots`) and last visit
  (`restockregen_holevisit`).
- On each infested citadel: its egg and creep block spots (`restockregen_citadelspots`) and last
  visit (`restockregen_citadelvisit`).
- Beside the world save: `worlds_local/<world>.restockregen.txt`, the ancient armor, obsidian,
  flametal and ice spots, and when each area was last visited. Keep it with the world when you back it up or move it.

Removing the mod leaves these behind, and the game ignores them.

## Credits

The idea of restocking dungeons on a timer comes from [Dvala](https://github.com/Ezomic/valheim-dvala)
by Ezomic. This is a separate, server-only implementation and contains no Dvala code.

## Licence

MIT.
