using System;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace RestockRegen
{
	/*
		Empty loot chests refill from their own loot table after a number of in-game days.

		Server-side only. The server holds every object in the world in ZDOMan, loaded by a
		player or not, and has every prefab in ZNetScene, so it can find loot chests, tell
		whether they are empty, and roll a fresh set of items without any client code.

		On world load it logs a census of loot chests. After that it sweeps once per in-game day
		(see Restocker), which covers dawn and sleep skips. A dedicated server's world time only moves
		while a player is online (ZNet.UpdateNetTime), so every count of days is in played time.
	*/
	[BepInPlugin(Guid, Name, Version)]
	public class RestockRegenPlugin : BaseUnityPlugin
	{
		// Working name; the final name is still to be chosen. Changing Guid renames the config file.
		public const string Guid = "liekos47.restockregen";
		public const string Name = "RestockRegen";
		public const string Version = "0.18.0";

		internal static ManualLogSource Log;
		internal static RestockRegenPlugin Instance;

		internal static ConfigEntry<bool> Enabled;
		internal static ConfigEntry<int> Days;
		internal static ConfigEntry<bool> Verbose;
		internal static ConfigEntry<bool> DryRun;
		internal static ConfigEntry<string> Exclude;
		internal static ConfigEntry<bool> Notify;
		internal static ConfigEntry<string> NotifyText;
		internal static ConfigEntry<string> NotifyDueText;
		internal static ConfigEntry<string> NotifyLeftoversText;
		internal static ConfigEntry<bool> ResetOpened;
		internal static ConfigEntry<bool> RestockLoaded;
		internal static ConfigEntry<bool> MudPileRegenNow;
		internal static ConfigEntry<bool> Crystals;
		internal static ConfigEntry<int> CrystalDays;
		internal static ConfigEntry<bool> CrystalDryRun;
		internal static ConfigEntry<bool> CrystalRegenNow;
		internal static ConfigEntry<string> CrystalNotifyText;
		internal static ConfigEntry<string> CrystalEntranceText;
		internal static ConfigEntry<string> CrystalDueText;
		internal static ConfigEntry<float> CaveEntranceRadius;
		internal static ConfigEntry<float> CaveRadius;
		internal static ConfigEntry<bool> BlackCores;
		internal static ConfigEntry<int> BlackCoreDays;
		internal static ConfigEntry<bool> BlackCoreDryRun;
		internal static ConfigEntry<bool> BlackCoreRegenNow;
		internal static ConfigEntry<string> BlackCoreNotifyText;
		internal static ConfigEntry<string> BlackCoreEntranceText;
		internal static ConfigEntry<string> BlackCoreDueText;
		internal static ConfigEntry<float> MineEntranceRadius;
		internal static ConfigEntry<float> MineRadius;
		internal static ConfigEntry<bool> Morkhalla;
		internal static ConfigEntry<int> MorkhallaDays;
		internal static ConfigEntry<bool> MorkhallaDryRun;
		internal static ConfigEntry<bool> MorkhallaRegenNow;
		internal static ConfigEntry<string> MorkhallaNotifyText;
		internal static ConfigEntry<string> MorkhallaEntranceText;
		internal static ConfigEntry<string> MorkhallaDueText;
		internal static ConfigEntry<float> MorkhallaEntranceRadius;
		internal static ConfigEntry<float> MorkhallaRadius;
		internal static ConfigEntry<bool> TheHole;
		internal static ConfigEntry<int> HoleDays;
		internal static ConfigEntry<bool> HoleDryRun;
		internal static ConfigEntry<bool> HoleRegenNow;
		internal static ConfigEntry<string> HoleNotifyText;
		internal static ConfigEntry<string> HoleEntranceText;
		internal static ConfigEntry<string> HoleDueText;
		internal static ConfigEntry<float> HoleEntranceRadius;
		internal static ConfigEntry<float> HoleRadius;
		internal static ConfigEntry<bool> Citadel;
		internal static ConfigEntry<int> CitadelDays;
		internal static ConfigEntry<bool> CitadelDryRun;
		internal static ConfigEntry<bool> CitadelRegenNow;
		internal static ConfigEntry<bool> CitadelCloseDoor;
		internal static ConfigEntry<string> CitadelNotifyText;
		internal static ConfigEntry<string> CitadelEntranceText;
		internal static ConfigEntry<string> CitadelDueText;
		internal static ConfigEntry<float> CitadelEntranceRadius;
		internal static ConfigEntry<float> CitadelRadius;
		internal static DungeonRegrow MudKind, CrystalKind, BlackCoreKind, MorkhallaKind, HoleKind, CitadelKind;
		internal static ConfigEntry<bool> MudPiles;
		internal static ConfigEntry<int> MudPileDays;
		internal static ConfigEntry<bool> MudPileDryRun;
		internal static ConfigEntry<float> CryptRadius;
		internal static ConfigEntry<string> MudPileNotifyText;
		internal static ConfigEntry<string> MudPileEntranceText;
		internal static ConfigEntry<string> MudPileDueText;
		internal static ConfigEntry<int> VisitCheckSeconds;
		internal static ConfigEntry<float> EntranceRadius;
		internal static ConfigEntry<bool> ShowHistory;
		internal static ConfigEntry<string> HistoryText;
		internal static ConfigEntry<string> HistoryTextOnce;

		private Harmony harmony;
		private bool censusDone;
		private bool mudFailed;
		private bool regrowFailed;
		private float nextMudTick;
		private int lastSweepDay = -1;
		private float nextCheck;

		private void Awake()
		{
			Log = Logger;

			Enabled = Config.Bind("General", "Enabled", true,
				"Off leaves the plugin loaded and doing nothing.");
			Days = Config.Bind("General", "Days", 30,
				"In-game days an empty loot chest must stay empty before it refills. A Valheim day is 20 minutes, and on a dedicated server time only moves while someone is online, so 30 days is about 10 hours of play.");
			Verbose = Config.Bind("General", "Verbose", false,
				"Log one line per chest prefab and per restocked chest.");
			DryRun = Config.Bind("General", "DryRun", false,
				"Count and log what would happen, but write nothing to the world. Empty-since dates are kept in memory instead, so a dry run still comes due; they reset on restart.");

			ResetOpened = Config.Bind("General", "ResetOpened", true,
				"On: a loot chest resets Days after a player first opens it, even if items are left in it, and whatever is inside then is replaced by a fresh roll. " +
				"Off: only chests that are completely empty are on a clock, and putting anything back in stops it.");
			RestockLoaded = Config.Bind("General", "RestockLoaded", true,
				"Restock a chest even while a player has its area loaded (a chest near a base, for example), as long as nobody has it open. " +
				"Off: wait until no player is near it.");
			Exclude = Config.Bind("General", "Exclude",
				"TreasureChest_forestcrypt_hildir,TreasureChest_mountaincave_hildir,TreasureChest_plainsfortress_hildir",
				"Comma-separated chest prefab names never to restock. The default is Hildir's three quest chests, " +
				"which each hold one of her quest items. Read once at world load: changing it needs a restart.");

			Notify = Config.Bind("Notify", "Notify", true,
				"Show a message in the middle of the screen when a player opens an empty loot chest, or takes the last item out of one, saying when it restocks.");
			NotifyText = Config.Bind("Notify", "NotifyText", "The spirits will refill this chest in {days}",
				"The message. {days} becomes \"1 day\" or \"N days\" (in-game days).");
			NotifyLeftoversText = Config.Bind("Notify", "NotifyLeftoversText", "The spirits will refill this chest in {days}. Anything left inside will be lost",
				"The message when the chest still has items in it (only with ResetOpened).");
			NotifyDueText = Config.Bind("Notify", "NotifyDueText", "The spirits will refill this chest at the next dawn, once no one is near",
				"The message for a chest whose time is already up. It restocks at the next daily sweep that finds no player holding it.");

			ShowHistory = Config.Bind("History", "History", true,
				"Remember who first opened each loot chest and who opened it last, and tell the next player who opens it (top-left message).");
			HistoryText = Config.Bind("History", "HistoryText", "Last opened by {last} {lastago}, first by {first} {firstago}",
				"{last}/{first} are character names (\"you\" for the player reading it), {lastago}/{firstago} are \"today\", \"yesterday\" or \"N days ago\" in in-game days.");
			HistoryTextOnce = Config.Bind("History", "HistoryTextOnce", "Last opened by {last} {lastago}",
				"Used instead when only one opening has been recorded so far.");

			MudPiles = Config.Bind("MudPiles", "MudPiles", true,
				"Regenerate muddy scrap piles in sunken crypts that no player has visited for MudPileDays in-game days. " +
				"Only piles the mod has seen, intact or partly mined, can come back: one mined out before it was installed left no trace.");
			MudPileDays = Config.Bind("MudPiles", "MudPileDays", 30,
				"In-game days a sunken crypt must go unvisited before its piles regenerate. Any visit starts the count again.");
			MudPileDryRun = Config.Bind("MudPiles", "MudPileDryRun", false,
				"Log which crypts would regenerate, but create and remove nothing and write nothing. Visits are then tracked in memory only.");
			MudPileNotifyText = Config.Bind("MudPiles", "MudPileNotifyText",
				"The spirits will restore muddy scrap piles after {days} without visitors",
				"Shown to a player inside a sunken crypt with mined piles (needs Notify on). Their visit has just restarted the clock, so {days} is the full MudPileDays. {mined} is how many, {piles} is " +
				"\"muddy scrap pile(s)\", {days} is MudPileDays. Every visit restarts the count, so it is always the full number of days.");
			MudPileEntranceText = Config.Bind("MudPiles", "MudPileEntranceText",
				"The spirits will restore muddy scrap piles in {days} if no one enters",
				"Shown at the entrance of a sunken crypt with mined piles. {days} is what is left of the crypt's clock; going in restarts it.");
			MudPileDueText = Config.Bind("MudPiles", "MudPileDueText",
				"The spirits will restore muddy scrap piles at the next dawn if no one enters",
				"Shown at the entrance when the crypt's time is already up. It regenerates at the next daily sweep with nobody nearby.");
			MudPileRegenNow = Config.Bind("MudPiles", "MudPileRegenNow", false,
				"One-shot: at the next daily sweep, every sunken crypt with mined piles regenerates whatever its clock says, then this sets itself back to false. " +
				"A crypt with a player in or near it then is done at the first sweep that finds it free.");
			VisitCheckSeconds = Config.Bind("MudPiles", "VisitCheckSeconds", 30,
				"Real seconds between checks for players at or inside sunken crypts. A visit shorter than this can go unnoticed, " +
				"and the message can arrive up to this long after a player gets there.");
			EntranceRadius = Config.Bind("MudPiles", "EntranceRadius", 30f,
				"Metres, measured flat, around a sunken crypt's entrance within which a player on the surface is told about its mined piles. " +
				"Standing there does not count as a visit.");
			CryptRadius = Config.Bind("MudPiles", "CryptRadius", 200f,
				"Metres, measured flat from a sunken crypt's generator, within which a player up at dungeon height counts as inside that crypt, and a pile belongs to it.");

			Instance = this;
			Regrow.Configure(Config);

			Crystals = Config.Bind("FrostCaves", "FrostCaveCrystals", true,
				"Restore the crystals in Mountain frost caves that no player has been inside for CrystalDays in-game days. " +
				"Only crystals the mod has seen can come back: one picked before it was installed left no trace.");
			CrystalDays = Config.Bind("FrostCaves", "CrystalDays", 30,
				"In-game days a frost cave must go without anyone inside before its crystals come back. Any visit starts the count again.");
			CrystalDryRun = Config.Bind("FrostCaves", "CrystalDryRun", false,
				"Log which frost caves would regenerate, but create and remove nothing and write nothing. Visits are then tracked in memory only.");
			CrystalRegenNow = Config.Bind("FrostCaves", "CrystalRegenNow", false,
				"One-shot: at the next daily sweep, every frost cave with picked crystals regenerates whatever its clock says, then this sets itself back to false.");
			CrystalNotifyText = Config.Bind("FrostCaves", "CrystalNotifyText", "The spirits will restore the crystals after {days} without visitors",
				"Shown to a player inside a frost cave with picked crystals (needs Notify on). {days} is CrystalDays; {mined} is how many were picked.");
			CrystalEntranceText = Config.Bind("FrostCaves", "CrystalEntranceText", "The spirits will restore the crystals in {days} if no one enters",
				"Shown at the entrance of a frost cave with picked crystals. {days} is what is left of the cave's clock; going in restarts it.");
			CrystalDueText = Config.Bind("FrostCaves", "CrystalDueText", "The spirits will restore the crystals at the next dawn if no one enters",
				"Shown at the entrance when the cave's time is already up.");
			CaveEntranceRadius = Config.Bind("FrostCaves", "CaveEntranceRadius", 50f,
				"Metres, measured flat, around a frost cave's centre within which a player on the surface gets the entrance message. " +
				"A frost cave's surface location stands about 30 m from its interior's centre. Standing there does not count as a visit.");
			CaveRadius = Config.Bind("FrostCaves", "CaveRadius", 200f,
				"Metres from a frost cave's centre that count as inside it.");

			BlackCores = Config.Bind("InfestedMines", "BlackCores", true,
				"Restore the black cores in Mistlands infested mines that no player has been inside for BlackCoreDays in-game days. " +
				"A taken core's stand stays in the world marked as picked, so every taken core can come back, including ones taken before the mod was installed.");
			BlackCoreDays = Config.Bind("InfestedMines", "BlackCoreDays", 30,
				"In-game days an infested mine must go without anyone inside before its black cores come back. Any visit starts the count again.");
			BlackCoreDryRun = Config.Bind("InfestedMines", "BlackCoreDryRun", false,
				"Log which infested mines would regenerate, but change nothing and write nothing. Visits are then tracked in memory only.");
			BlackCoreRegenNow = Config.Bind("InfestedMines", "BlackCoreRegenNow", false,
				"One-shot: at the next daily sweep, every infested mine with taken black cores regenerates whatever its clock says, then this sets itself back to false.");
			BlackCoreNotifyText = Config.Bind("InfestedMines", "BlackCoreNotifyText", "The spirits will restore the black cores after {days} without visitors",
				"Shown to a player inside an infested mine with taken black cores (needs Notify on). {days} is BlackCoreDays; {mined} is how many were taken.");
			BlackCoreEntranceText = Config.Bind("InfestedMines", "BlackCoreEntranceText", "The spirits will restore the black cores in {days} if no one enters",
				"Shown at the entrance of an infested mine with taken black cores. {days} is what is left of the mine's clock; going in restarts it.");
			BlackCoreDueText = Config.Bind("InfestedMines", "BlackCoreDueText", "The spirits will restore the black cores at the next dawn if no one enters",
				"Shown at the entrance when the mine's time is already up.");
			MineEntranceRadius = Config.Bind("InfestedMines", "MineEntranceRadius", 50f,
				"Metres, measured flat, around an infested mine's centre within which a player on the surface gets the entrance message. " +
				"A mine's surface location stands about 30 m from its interior's centre. Standing there does not count as a visit.");
			MineRadius = Config.Bind("InfestedMines", "MineRadius", 200f,
				"Metres from an infested mine's centre that count as inside it.");

			Morkhalla = Config.Bind("Morkhalla", "Morkhalla", true,
				"Restore the gems and coin piles in the Deep North's Morkhalla dungeon once no player has been inside it for MorkhallaDays in-game days: " +
				"the gemstones in the statue eyes, the rubble piles that drop ancient coins, the treasure piles, and the black ice block that starts a jotun invasion. " +
				"Eyes whose gem was taken before the mod was installed come back too; piles broken or taken before then cannot.");
			MorkhallaDays = Config.Bind("Morkhalla", "MorkhallaDays", 30,
				"In-game days the dungeon must go without anyone inside before its gems and coin piles come back. Any visit starts the count again.");
			MorkhallaDryRun = Config.Bind("Morkhalla", "MorkhallaDryRun", false,
				"Log what would be restored, but change nothing and write nothing. Visits are then tracked in memory only.");
			MorkhallaRegenNow = Config.Bind("Morkhalla", "MorkhallaRegenNow", false,
				"One-shot: at the next daily sweep the dungeon regenerates whatever its clock says, then this sets itself back to false.");
			MorkhallaNotifyText = Config.Bind("Morkhalla", "MorkhallaNotifyText", "The spirits will restore the gems and coin piles after {days} without visitors",
				"Shown to a player inside the dungeon when gems or piles are missing (needs Notify on). {days} is MorkhallaDays; {mined} is how many are missing.");
			MorkhallaEntranceText = Config.Bind("Morkhalla", "MorkhallaEntranceText", "The spirits will restore the gems and coin piles in {days} if no one enters",
				"Shown at the dungeon's entrance when gems or piles are missing. {days} is what is left of its clock; going in restarts it.");
			MorkhallaDueText = Config.Bind("Morkhalla", "MorkhallaDueText", "The spirits will restore the gems and coin piles at the next dawn if no one enters",
				"Shown at the entrance when the dungeon's time is already up.");
			MorkhallaEntranceRadius = Config.Bind("Morkhalla", "MorkhallaEntranceRadius", 50f,
				"Metres, measured flat, around the dungeon's centre within which a player on the surface gets the entrance message. " +
				"Its surface location stands about 30 m from its interior's centre. Standing there does not count as a visit.");
			MorkhallaRadius = Config.Bind("Morkhalla", "MorkhallaRadius", 200f,
				"Metres from the dungeon's centre that count as inside it.");

			TheHole = Config.Bind("TheHole", "TheHole", true,
				"Restore the trash piles and the spawner nests in the Deep North's Holes once no player has been inside a Hole for HoleDays in-game days. " +
				"Only ones the mod has seen can come back: one broken before it was installed left no trace. Glow worms are left to the game, which respawns them itself; roots are not restored.");
			HoleDays = Config.Bind("TheHole", "HoleDays", 30,
				"In-game days a Hole must go without anyone inside before its trash piles and nests come back. Any visit starts the count again.");
			HoleDryRun = Config.Bind("TheHole", "HoleDryRun", false,
				"Log which Holes would regenerate, but create nothing and write nothing. Visits are then tracked in memory only.");
			HoleRegenNow = Config.Bind("TheHole", "HoleRegenNow", false,
				"One-shot: at the next daily sweep, every Hole with broken piles or nests regenerates whatever its clock says, then this sets itself back to false.");
			HoleNotifyText = Config.Bind("TheHole", "HoleNotifyText", "The spirits will restore the nests and trash piles after {days} without visitors",
				"Shown to a player inside a Hole with broken piles or nests (needs Notify on). {days} is HoleDays; {mined} is how many are missing.");
			HoleEntranceText = Config.Bind("TheHole", "HoleEntranceText", "The spirits will restore the nests and trash piles in {days} if no one enters",
				"Shown at the entrance of a Hole with broken piles or nests. {days} is what is left of the Hole's clock; going in restarts it.");
			HoleDueText = Config.Bind("TheHole", "HoleDueText", "The spirits will restore the nests and trash piles at the next dawn if no one enters",
				"Shown at the entrance when the Hole's time is already up.");
			HoleEntranceRadius = Config.Bind("TheHole", "HoleEntranceRadius", 40f,
				"Metres, measured flat, around a Hole's centre within which a player on the surface gets the entrance message. " +
				"A Hole's surface location stands about 20 m from its interior's centre. Standing there does not count as a visit.");
			HoleRadius = Config.Bind("TheHole", "HoleRadius", 150f,
				"Metres from a Hole's centre that count as inside it. Its piles and nests are within about 50 m; the next Hole is over 300 m away.");

			Citadel = Config.Bind("Citadel", "Citadel", true,
				"Reset a Mistlands infested citadel whose Queen has been killed, once no player has been inside it for CitadelDays in-game days: " +
				"the Queen is put back in her place, and the seeker eggs and creep blocks the mod has seen are restored. " +
				"A citadel whose Queen is alive is never touched. Queens killed before the mod was installed come back too.");
			CitadelDays = Config.Bind("Citadel", "CitadelDays", 30,
				"In-game days a citadel with a dead Queen must go without anyone inside before it resets. Any visit starts the count again.");
			CitadelDryRun = Config.Bind("Citadel", "CitadelDryRun", false,
				"Log which citadels would reset, but create nothing and write nothing. Visits are then tracked in memory only.");
			CitadelRegenNow = Config.Bind("Citadel", "CitadelRegenNow", false,
				"One-shot: at the next daily sweep, every citadel with a dead Queen resets whatever its clock says, then this sets itself back to false.");
			CitadelCloseDoor = Config.Bind("Citadel", "CitadelCloseDoor", true,
				"Close the citadel's sealed door when it resets, so it takes a Sealbreaker to get in again. The game does not use up the Sealbreaker.");
			CitadelNotifyText = Config.Bind("Citadel", "CitadelNotifyText", "The spirits will bring the Queen back after {days} without visitors",
				"Shown to a player inside a citadel whose Queen is dead (needs Notify on). {days} is CitadelDays.");
			CitadelEntranceText = Config.Bind("Citadel", "CitadelEntranceText", "The spirits will bring the Queen back in {days} if no one enters",
				"Shown in front of the door of a citadel whose Queen is dead. {days} is what is left of the citadel's clock; going in restarts it.");
			CitadelDueText = Config.Bind("Citadel", "CitadelDueText", "The spirits will bring the Queen back at the next dawn if no one enters",
				"Shown in front of the door when the citadel's time is already up. It resets at the next daily sweep with nobody at the door or inside.");
			CitadelEntranceRadius = Config.Bind("Citadel", "CitadelEntranceRadius", 30f,
				"Metres, measured flat, around a citadel's sealed door within which a player on the surface gets the entrance message. " +
				"Standing there does not count as a visit.");
			CitadelRadius = Config.Bind("Citadel", "CitadelRadius", 150f,
				"Metres from a citadel's centre that count as inside it. The Queen stands about 22 m from it.");

			DungeonRegrow.All.Clear();
			MudKind = new DungeonRegrow(new DungeonRegrow.Kind
			{
				Label = "mud piles", DungeonLabel = "sunken crypt", Generator = "DG_SunkenCrypt",
				Names = new[] { "mudpile2", "mudpile" }, Singular = "muddy scrap pile", Plural = "muddy scrap piles",
				SpotsKey = "restockregen_mudspots", VisitKey = "restockregen_lastvisit",
				On = MudPiles, Days = MudPileDays, DryRun = MudPileDryRun, RegenNow = MudPileRegenNow,
				NotifyText = MudPileNotifyText, EntranceText = MudPileEntranceText, DueText = MudPileDueText,
				EntranceRadius = EntranceRadius, Radius = CryptRadius,
			});
			CrystalKind = new DungeonRegrow(new DungeonRegrow.Kind
			{
				Label = "frost cave crystals", DungeonLabel = "frost cave", Generator = "DG_Cave",
				Names = new[] { "Pickable_MountainCaveCrystal" }, Singular = "crystal", Plural = "crystals",
				SpotsKey = "restockregen_crystalspots", VisitKey = "restockregen_cavevisit",
				On = Crystals, Days = CrystalDays, DryRun = CrystalDryRun, RegenNow = CrystalRegenNow,
				NotifyText = CrystalNotifyText, EntranceText = CrystalEntranceText, DueText = CrystalDueText,
				EntranceRadius = CaveEntranceRadius, Radius = CaveRadius,
			});
			BlackCoreKind = new DungeonRegrow(new DungeonRegrow.Kind
			{
				Label = "black cores", DungeonLabel = "infested mine", Generator = "DG_DvergrTown",
				Names = new[] { "Pickable_BlackCoreStand" }, Singular = "black core", Plural = "black cores",
				SpotsKey = "restockregen_corespots", VisitKey = "restockregen_minevisit",
				On = BlackCores, Days = BlackCoreDays, DryRun = BlackCoreDryRun, RegenNow = BlackCoreRegenNow,
				NotifyText = BlackCoreNotifyText, EntranceText = BlackCoreEntranceText, DueText = BlackCoreDueText,
				EntranceRadius = MineEntranceRadius, Radius = MineRadius,
			});
			MorkhallaKind = new DungeonRegrow(new DungeonRegrow.Kind
			{
				Label = "Morkhalla gems and coin piles", DungeonLabel = "Morkhalla dungeon", Generator = "DG_MorkHalla",
				Names = new[]
				{
					"Morkhalla_Eye1", "Morkhalla_Eye2", "Morkhalla_Eye3", "Morkhalla_Eye4",
					"Morkhalla_Eye5_gemstone", "Morkhalla_Eye6_gemstone", "Morkhalla_Eye7_gemstone",
					"Morkhalla_Rubble1", "Morkhalla_Rubble2", "Morkhalla_Rubble3", "Morkhalla_Rubble4",
					"Pickable_MorkHallaTreasure", "BlackIce_Start",
				},
				Singular = "gem or coin pile", Plural = "gems and coin piles",
				SpotsKey = "restockregen_morkspots", VisitKey = "restockregen_morkvisit",
				On = Morkhalla, Days = MorkhallaDays, DryRun = MorkhallaDryRun, RegenNow = MorkhallaRegenNow,
				NotifyText = MorkhallaNotifyText, EntranceText = MorkhallaEntranceText, DueText = MorkhallaDueText,
				EntranceRadius = MorkhallaEntranceRadius, Radius = MorkhallaRadius,
			});
			DungeonRegrow.All.Add(MudKind);
			DungeonRegrow.All.Add(CrystalKind);
			DungeonRegrow.All.Add(BlackCoreKind);
			DungeonRegrow.All.Add(MorkhallaKind);
			HoleKind = new DungeonRegrow(new DungeonRegrow.Kind
			{
				Label = "Hole nests and trash piles", DungeonLabel = "Hole", Generator = "DG_Hole",
				Names = new[] { "elaking_trashpile", "Spawner_Hole", "Spawner_Hole_double" },
				Singular = "nest or trash pile", Plural = "nests and trash piles",
				SpotsKey = "restockregen_holespots", VisitKey = "restockregen_holevisit",
				On = TheHole, Days = HoleDays, DryRun = HoleDryRun, RegenNow = HoleRegenNow,
				NotifyText = HoleNotifyText, EntranceText = HoleEntranceText, DueText = HoleDueText,
				EntranceRadius = HoleEntranceRadius, Radius = HoleRadius,
			});
			DungeonRegrow.All.Add(HoleKind);
			CitadelKind = new DungeonRegrow(new DungeonRegrow.Kind
			{
				Label = "the Queen", DungeonLabel = "infested citadel", Generator = "DG_DvergrBoss",
				Names = new[]
				{
					"SeekerEgg", "blackmarble_creep_4x1x1", "blackmarble_creep_4x2x1", "blackmarble_creep_stair",
					"blackmarble_creep_slope_inverted_1x1x2", "blackmarble_creep_slope_inverted_2x2x1",
				},
				Singular = "egg or creep block", Plural = "eggs and creep blocks",
				SpotsKey = "restockregen_citadelspots", VisitKey = "restockregen_citadelvisit",
				On = Citadel, Days = CitadelDays, DryRun = CitadelDryRun, RegenNow = CitadelRegenNow,
				NotifyText = CitadelNotifyText, EntranceText = CitadelEntranceText, DueText = CitadelDueText,
				EntranceRadius = CitadelEntranceRadius, Radius = CitadelRadius,
				// Read from the room prefab: the room's origin is at (-300, 18.5, 400), the Queen at
				// (-298, -5.5, 400), turned 270 degrees.
				Boss = "SeekerQueen", BossRoom = "dvergr_new_bossroom_ENTRANCE02", Door = "dungeon_queen_door",
				BossOffset = new Vector3(2f, -24f, 0f), BossYaw = 270f, CloseDoor = CitadelCloseDoor,
			});
			DungeonRegrow.All.Add(CitadelKind);

			// A Harmony id of its own per load. On a hot reload (ScriptEngine) the new copy is
			// patched before the old copy is destroyed, and the old copy's UnpatchSelf would also
			// remove the new copy's patches if they shared an id.
			harmony = new Harmony($"{Guid}.{DateTime.Now.Ticks}");
			harmony.PatchAll();
			Log.LogInfo($"{Name} {Version} loaded");
			InvokeRepeating(nameof(ReloadConfig), 30f, 30f);
		}

		// Checked every 10 real seconds; the sweep itself runs once per in-game day.
		private void Update()
		{
			if (Time.time < nextCheck)
			{
				return;
			}
			nextCheck = Time.time + 10f;
			if (!Enabled.Value || !WorldReady())
			{
				return;
			}
			try
			{
				if (!censusDone)
				{
					censusDone = true;
					LootChests.Census();
					RunMud(() => DungeonRegrow.All.ForEach(k => k.Init()));
					int startDay = EnvMan.instance.GetDay();
					RunRegrow(() => Regrow.Init(startDay));
				}
				History.Flush();
				int today = EnvMan.instance.GetDay();
				if (Time.time >= nextMudTick)
				{
					nextMudTick = Time.time + Mathf.Max(5, VisitCheckSeconds.Value);
					RunMud(() => DungeonRegrow.All.ForEach(k => k.Tick(today)));
					RunRegrow(() => Regrow.Tick(today));
				}
				if (today == lastSweepDay)
				{
					return;
				}
				lastSweepDay = today;
				RunRegrow(() =>
				{
					var regrowWatch = System.Diagnostics.Stopwatch.StartNew();
					var results = Regrow.Sweep(today);
					foreach (var kv in results)
					{
						Regrow.Group g = kv.Key;
						Regrow.Result a = kv.Value;
						if (g.Enabled)
						{
							Log.LogInfo($"day {today} {g.Label}{(g.DryRun.Value ? " (dry run)" : "")}: {a.Missing} damaged or gone, " +
								$"{a.Replaced + a.Restored} restored ({a.Replaced} replacing a damaged one, {a.Restored} that were gone), {a.Waiting} waiting, " +
								$"{a.Near} skipped with a player near, {a.Built} blocked by a build");
						}
					}
					Log.LogInfo($"day {today} regrow sweep took {regrowWatch.ElapsedMilliseconds} ms");
				});
				RunMud(() =>
				{
					foreach (DungeonRegrow kind in DungeonRegrow.All)
					{
						var watch2 = System.Diagnostics.Stopwatch.StartNew();
						DungeonRegrow.Result m = kind.Sweep(today);
						if (kind.K.On.Value)
						{
							Log.LogInfo($"day {today} {kind.K.Label}{(kind.K.DryRun.Value ? " (dry run)" : "")}: {m.Dungeons} {kind.K.DungeonLabel}s, " +
								$"{m.Regenerated} regenerated ({m.Replaced} damaged replaced, {m.Rebuilt} gone rebuilt), " +
								$"{m.Waiting} waiting, {m.Occupied} skipped as occupied, {watch2.ElapsedMilliseconds} ms");
						}
					}
				});
				var watch = System.Diagnostics.Stopwatch.StartNew();
				Restocker.Result r = Restocker.Sweep(today, DryRun.Value, Verbose.Value);
				Log.LogInfo($"day {today}{(DryRun.Value ? " (dry run)" : "")}: {r.Chests} loot chests, " +
					$"{r.Restocked} restocked, {r.Stamped} clocks started, {r.Waiting} waiting, {r.Cleared} refilled by players, " +
					$"{r.Forced} checked while loaded, {r.Loaded} skipped as open or loaded" + (r.RolledNothing > 0 ? $", {r.RolledNothing} rolled nothing (retry next day)" : "") +
					$", {watch.ElapsedMilliseconds} ms");
			}
			catch (Exception e)
			{
				Log.LogError($"sweep failed: {e}");
			}
		}

		// The dungeon regrow code (mud piles, crystals) creates and removes objects; a fault in it must
		// not stop chest restocking. After one exception it switches itself off until the next restart.
		private void RunMud(Action action)
		{
			if (mudFailed)
			{
				return;
			}
			try
			{
				action();
			}
			catch (Exception e)
			{
				mudFailed = true;
				Log.LogError($"dungeon regrow (mud piles, crystals, black cores, Morkhalla, the Hole, the Queen) failed and is off until restart: {e}");
			}
		}

		private void RunRegrow(Action action)
		{
			if (regrowFailed)
			{
				return;
			}
			try
			{
				action();
			}
			catch (Exception e)
			{
				regrowFailed = true;
				Log.LogError($"regrow (ancient armor, obsidian, flametal, ice) failed and is off until restart: {e}");
			}
		}

		// "restock-regen-armor", "-obsidian", "-flametal", "-ice": every damaged or gone spot of that kind that
		// is free, now, whatever its clock. Returns false for an unknown kind.
		internal bool RegrowNow(string id)
		{
			Regrow.Group group = Regrow.Find(id);
			if (group == null || !WorldReady())
			{
				return false;
			}
			int today = EnvMan.instance.GetDay();
			RunRegrow(() =>
			{
				Regrow.Result a = Regrow.Sweep(today, group, now: true)[group];
				Log.LogInfo($"{group.Label} regen now{(group.DryRun.Value ? " (dry run)" : "")}: {a.Replaced + a.Restored} restored, " +
					$"{a.Near} with a player near, {a.Built} blocked by a build");
			});
			return true;
		}

		// Server only, and only once the world's objects and the prefab registry both exist.
		internal static bool WorldReady()
		{
			return ZNet.instance != null && ZNet.instance.IsServer()
				&& ZDOMan.instance != null && ZNetScene.instance != null && EnvMan.instance != null && Game.instance != null
				&& ZDOMan.instance.m_objectsByID.Count > 0;
		}

		private void ReloadConfig()
		{
			try { Config.Reload(); } catch (Exception e) { Log.LogWarning($"config reload failed: {e.Message}"); }
		}

		private void OnDestroy()
		{
			// Hot reload or shutdown: write what can safely be written before this copy goes.
			try
			{
				if (WorldReady())
				{
					History.Flush();
					DungeonRegrow.All.ForEach(k => k.FlushPending());
					Regrow.Save();
				}
			}
			catch (Exception e)
			{
				Log.LogWarning($"flush on unload failed: {e.Message}");
			}
			harmony?.UnpatchSelf();
			if (Instance == this)
			{
				Instance = null;
			}
		}

		// "!restock reload": ScriptEngine reloads every plugin in BepInEx/scripts, this one included.
		internal void HotReload()
		{
			Log.LogInfo($"{Name} {Version}: hot reload requested");
			if (!AdminCommands.Reload())
			{
				Log.LogWarning("hot reload failed: ScriptEngine not found");
			}
		}

		// "restock-regen" (sunken crypts) and "restock-regen-crystals" (frost caves): every free
		// dungeon of that kind with something taken, now rather than at the next sweep.
		internal void RegenDungeonsNow(DungeonRegrow kind)
		{
			if (!WorldReady())
			{
				return;
			}
			kind.K.RegenNow.Value = true;
			int today = EnvMan.instance.GetDay();
			RunMud(() =>
			{
				DungeonRegrow.Result m = kind.Sweep(today);
				Log.LogInfo($"{kind.K.Label} regen now{(kind.K.DryRun.Value ? " (dry run)" : "")}: {m.Regenerated} {kind.K.DungeonLabel}s regenerated " +
					$"({m.Replaced} damaged replaced, {m.Rebuilt} gone rebuilt), {m.Occupied} occupied, done when free");
			});
		}

		// "!restock status"
		internal string Status()
		{
			int today = EnvMan.instance != null ? EnvMan.instance.GetDay() : 0;
			return $"RestockRegen {Version}, day {today}: {LootChests.Prefabs.Count} loot chest kinds; " +
				string.Join("; ", DungeonRegrow.All.Select(k => k.Summary())) + (mudFailed ? " (dungeon regrow off after an error)" : "") + "; " +
				Regrow.Summary() + (regrowFailed ? " (off after an error)" : "") +
				(DryRun.Value ? "; chests DRY RUN" : "");
		}
	}
}
