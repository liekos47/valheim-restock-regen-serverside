using System;
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
		public const string Version = "0.9.0";

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
		internal static ConfigEntry<bool> AncientArmor;
		internal static ConfigEntry<int> AncientArmorDays;
		internal static ConfigEntry<bool> AncientArmorDryRun;
		internal static ConfigEntry<float> AncientArmorVisitRadius;
		internal static ConfigEntry<float> AncientArmorBuildClearance;
		internal static ConfigEntry<string> AncientArmorNotifyText;
		internal static ConfigEntry<float> AncientArmorNotifyCooldown;
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
		private bool armorFailed;
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
			// A Harmony id of its own per load. On a hot reload (ScriptEngine) the new copy is
			// patched before the old copy is destroyed, and the old copy's UnpatchSelf would also
			// remove the new copy's patches if they shared an id.
			AncientArmor = Config.Bind("AncientArmor", "AncientArmor", true,
				"Restore ancient armor (the giant's helmets and swords in the Mistlands) where no player has been for AncientArmorDays in-game days. " +
				"Only pieces the mod has seen, whole or partly mined, can come back.");
			AncientArmorDays = Config.Bind("AncientArmor", "AncientArmorDays", 30,
				"In-game days with no player within AncientArmorVisitRadius before a mined piece comes back. Any visit starts the count again.");
			AncientArmorDryRun = Config.Bind("AncientArmor", "AncientArmorDryRun", false,
				"Log which pieces would come back, but create and remove nothing. Spots and visits are still recorded.");
			AncientArmorVisitRadius = Config.Bind("AncientArmor", "AncientArmorVisitRadius", 100f,
				"Metres. A player this close to a piece's spot counts as a visit, and no piece is restored with a player this close.");
			AncientArmorBuildClearance = Config.Bind("AncientArmor", "AncientArmorBuildClearance", 8f,
				"Metres, measured flat. A piece is not restored if anything a player built stands this close to its spot. 0 turns the check off.");
			AncientArmorNotifyText = Config.Bind("AncientArmor", "AncientArmorNotifyText",
				"The spirits will restore ancient armor after {days} without visitors",
				"Shown to a player entering the Mistlands (needs Notify on). {days} is AncientArmorDays.");
			AncientArmorNotifyCooldown = Config.Bind("AncientArmor", "AncientArmorNotifyCooldown", 10f,
				"Minutes. A player is told at most once in this long, so walking along the Mistlands border does not repeat it.");

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
					RunMud(() => RestockRegen.MudPiles.Init());
					int startDay = EnvMan.instance.GetDay();
					RunArmor(() => RestockRegen.AncientArmor.Init(startDay));
				}
				History.Flush();
				int today = EnvMan.instance.GetDay();
				if (Time.time >= nextMudTick)
				{
					nextMudTick = Time.time + Mathf.Max(5, VisitCheckSeconds.Value);
					RunMud(() => RestockRegen.MudPiles.Tick(today));
					RunArmor(() => RestockRegen.AncientArmor.Tick(today));
				}
				if (today == lastSweepDay)
				{
					return;
				}
				lastSweepDay = today;
				RunArmor(() =>
				{
					var armorWatch = System.Diagnostics.Stopwatch.StartNew();
					RestockRegen.AncientArmor.Result a = RestockRegen.AncientArmor.Sweep(today);
					if (AncientArmor.Value)
					{
						Log.LogInfo($"day {today} ancient armor{(AncientArmorDryRun.Value ? " (dry run)" : "")}: {a.Missing} mined, " +
							$"{a.Replaced + a.Refilled} restored ({a.Replaced} replacing broken remains, {a.Refilled} mined out), {a.Waiting} waiting, " +
							$"{a.Near} skipped with a player near, {a.Built} blocked by a build, {armorWatch.ElapsedMilliseconds} ms");
					}
				});
				RunMud(() =>
				{
					var mudWatch = System.Diagnostics.Stopwatch.StartNew();
					RestockRegen.MudPiles.Result m = RestockRegen.MudPiles.Sweep(today);
					if (MudPiles.Value)
					{
						Log.LogInfo($"day {today} mud piles{(MudPileDryRun.Value ? " (dry run)" : "")}: {m.Crypts} sunken crypts, " +
							$"{m.Regenerated} regenerated ({m.Replaced} partly mined piles replaced, {m.Rebuilt} rebuilt), " +
							$"{m.Waiting} waiting, {m.Occupied} skipped as occupied, {mudWatch.ElapsedMilliseconds} ms");
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

		// The mud pile module is new and creates and removes objects; a fault in it must not stop
		// chest restocking. After one exception it switches itself off until the next restart.
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
				Log.LogError($"mud piles failed and are off until restart: {e}");
			}
		}

		private void RunArmor(Action action)
		{
			if (armorFailed)
			{
				return;
			}
			try
			{
				action();
			}
			catch (Exception e)
			{
				armorFailed = true;
				Log.LogError($"ancient armor failed and is off until restart: {e}");
			}
		}

		// "restock-regen-armor": every mined piece that is free, now, whatever its clock.
		internal void RegenArmorNow()
		{
			if (!WorldReady())
			{
				return;
			}
			int today = EnvMan.instance.GetDay();
			RunArmor(() =>
			{
				RestockRegen.AncientArmor.Result a = RestockRegen.AncientArmor.Sweep(today, now: true);
				Log.LogInfo($"armor regen now{(AncientArmorDryRun.Value ? " (dry run)" : "")}: {a.Replaced + a.Refilled} restored, " +
					$"{a.Near} with a player near, {a.Built} blocked by a build");
			});
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
					RestockRegen.MudPiles.FlushPending();
					RestockRegen.AncientArmor.Save();
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

		// "!restock regen": every free sunken crypt with mined piles, now rather than at the next sweep.
		internal void RegenCryptsNow()
		{
			if (!WorldReady())
			{
				return;
			}
			MudPileRegenNow.Value = true;
			int today = EnvMan.instance.GetDay();
			RunMud(() =>
			{
				RestockRegen.MudPiles.Result m = RestockRegen.MudPiles.Sweep(today);
				Log.LogInfo($"regen now{(MudPileDryRun.Value ? " (dry run)" : "")}: {m.Regenerated} crypts regenerated " +
					$"({m.Replaced} partly mined piles replaced, {m.Rebuilt} rebuilt), {m.Occupied} occupied, done when free");
			});
		}

		// "!restock status"
		internal string Status()
		{
			int today = EnvMan.instance != null ? EnvMan.instance.GetDay() : 0;
			return $"RestockRegen {Version}, day {today}: {LootChests.Prefabs.Count} loot chest kinds; " +
				RestockRegen.MudPiles.Summary() + (mudFailed ? " (mud piles off after an error)" : "") + "; " +
				RestockRegen.AncientArmor.Summary() + (armorFailed ? " (off after an error)" : "") +
				(AncientArmorDryRun.Value ? "; ancient armor DRY RUN" : "") +
				(DryRun.Value ? "; chests DRY RUN" : "") + (MudPileDryRun.Value ? "; mud piles DRY RUN" : "");
		}
	}
}
