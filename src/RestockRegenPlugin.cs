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
		public const string Version = "0.6.0";

		internal static ManualLogSource Log;

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
			VisitCheckSeconds = Config.Bind("MudPiles", "VisitCheckSeconds", 30,
				"Real seconds between checks for players at or inside sunken crypts. A visit shorter than this can go unnoticed, " +
				"and the message can arrive up to this long after a player gets there.");
			EntranceRadius = Config.Bind("MudPiles", "EntranceRadius", 30f,
				"Metres, measured flat, around a sunken crypt's entrance within which a player on the surface is told about its mined piles. " +
				"Standing there does not count as a visit.");
			CryptRadius = Config.Bind("MudPiles", "CryptRadius", 200f,
				"Metres, measured flat from a sunken crypt's generator, within which a player up at dungeon height counts as inside that crypt, and a pile belongs to it.");

			harmony = new Harmony(Guid);
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
				}
				History.Flush();
				int today = EnvMan.instance.GetDay();
				if (Time.time >= nextMudTick)
				{
					nextMudTick = Time.time + Mathf.Max(5, VisitCheckSeconds.Value);
					RunMud(() => RestockRegen.MudPiles.Tick(today));
				}
				if (today == lastSweepDay)
				{
					return;
				}
				lastSweepDay = today;
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
					$"{r.Loaded} skipped as loaded by a player" + (r.RolledNothing > 0 ? $", {r.RolledNothing} rolled nothing (retry next day)" : "") +
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
			harmony?.UnpatchSelf();
		}
	}
}
