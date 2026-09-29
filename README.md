# RestockRegen (working name)

Valheim's loot chests are one-shot. Once a crypt chest is looted it stays that way for the life of
the world. This mod refills them: 30 in-game days after a player first opens a loot chest, whatever
is inside is replaced with a fresh roll from its own loot table.

**Server-side only.** Install it on the dedicated server. Players install nothing.

## Status

On world load the server logs a census of loot chests. After that it sweeps once per in-game day,
which covers normal dawn and sleeping through the night. One
sweep over a world of about 1.4 million objects takes well under a tenth of a second.

In-game time on a dedicated server only moves while at least one player is online (vanilla
`ZNet.UpdateNetTime`), so every count here is in played time: 30 in-game days is about 10 hours
with someone on the server, not 10 hours of the server running.

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
- A chest near a player, even one next to a base that is loaded all the time, still restocks, as
  long as nobody has it open at that moment. An open chest is tried again at the next day's sweep.
  Set `RestockLoaded` to false to only restock chests nobody is near.
- A chest that is destroyed (smashed or burnt) is gone from the world and cannot come back.

## Muddy scrap piles

Sunken crypts get their muddy scrap piles back once nobody has visited them for 30 in-game days.
Every visit starts the count again, so a crypt people keep going back to stays as it is.

Mining a pile does not mark it, it replaces it: the first hit swaps the whole pile for a broken
one, and breaking every chunk of that deletes it. So the mod remembers each pile's spot from the
first time it sees it, whole or broken, and stores the list on the crypt. A pile that was mined
out completely before the mod was installed left nothing behind and cannot come back.

A player arriving at the entrance of a sunken crypt with mined piles is told how long is left:
"The spirits will restore muddy scrap piles in 12 days if no one enters". If they go in anyway,
their visit restarts the count and they are told again: "The spirits will restore muddy scrap
piles after 30 days without visitors". Players are
checked every 30 seconds, so the message can take up to half a minute to appear. Standing at the
entrance does not count as a visit; only going inside does.

## Configuration

`BepInEx/config/liekos47.restockregen.cfg`, written on first run. The file is re-read every 30 seconds.

| Setting | Default | What it does |
| --- | --- | --- |
| `Enabled` | `true` | Off leaves the plugin loaded and doing nothing. |
| `Days` | `30` | In-game days from a chest's first opening (or being found empty) until it resets. A Valheim day is 20 minutes of played time. |
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
| `MudPiles` | `true` | Regenerate muddy scrap piles in sunken crypts left unvisited for `MudPileDays`. |
| `MudPileDays` | `30` | In-game days without a visitor before a crypt's piles come back. |
| `MudPileDryRun` | `false` | Log which crypts would regenerate, change nothing. |
| `MudPileEntranceText` | `The spirits will restore muddy scrap piles in {days} if no one enters` | Shown at the entrance, with the days left. |
| `MudPileDueText` | `The spirits will restore muddy scrap piles at the next dawn if no one enters` | Shown at the entrance when the time is already up. |
| `VisitCheckSeconds` | `30` | How often players at or in sunken crypts are checked. |
| `EntranceRadius` | `30` | Metres around a crypt's entrance where players on the surface get the message. |
| `CryptRadius` | `200` | Metres from a crypt's generator that count as inside it. |
| `MudPileNotifyText` | `The spirits will restore muddy scrap piles after {days} without visitors` | Shown on walking into a crypt with mined piles. |
| `RestockLoaded` | `true` | Restock chests a player has loaded, if nobody has them open. |
| `MudPileRegenNow` | `false` | One-shot: regenerate every crypt with mined piles at the next sweep, then switch back off. |
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
