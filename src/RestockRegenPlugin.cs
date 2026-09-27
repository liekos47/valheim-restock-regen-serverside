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
		(see Restocker), which covers dawn, sleep skips and a server running with nobody online.
	*/
	[BepInPlugin(Guid, Name, Version)]
	public class RestockRegenPlugin : BaseUnityPlugin
	{
		// Working name; the final name is still to be chosen. Changing Guid renames the config file.
		public const string Guid = "liekos47.restockregen";
		public const string Name = "RestockRegen";
		public const string Version = "0.3.0";

		internal static ManualLogSource Log;

		internal static ConfigEntry<bool> Enabled;
		internal static ConfigEntry<int> Days;
		internal static ConfigEntry<bool> Verbose;
		internal static ConfigEntry<bool> DryRun;
		internal static ConfigEntry<string> Exclude;

		private Harmony harmony;
		private bool censusDone;
		private int lastSweepDay = -1;
		private float nextCheck;

		private void Awake()
		{
			Log = Logger;

			Enabled = Config.Bind("General", "Enabled", true,
				"Off leaves the plugin loaded and doing nothing.");
			Days = Config.Bind("General", "Days", 30,
				"In-game days an empty loot chest must stay empty before it refills. A Valheim day is 20 real minutes of running world, so 30 days is about 10 hours.");
			Verbose = Config.Bind("General", "Verbose", false,
				"Log one line per chest prefab and per restocked chest.");
			DryRun = Config.Bind("General", "DryRun", false,
				"Count and log what would happen, but write nothing to the world. Empty-since dates are kept in memory instead, so a dry run still comes due; they reset on restart.");

			Exclude = Config.Bind("General", "Exclude",
				"TreasureChest_forestcrypt_hildir,TreasureChest_mountaincave_hildir,TreasureChest_plainsfortress_hildir",
				"Comma-separated chest prefab names never to restock. The default is Hildir's three quest chests, " +
				"which each hold one of her quest items. Read once at world load: changing it needs a restart.");

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
				}
				int today = EnvMan.instance.GetDay();
				if (today == lastSweepDay)
				{
					return;
				}
				lastSweepDay = today;
				var watch = System.Diagnostics.Stopwatch.StartNew();
				Restocker.Result r = Restocker.Sweep(today, DryRun.Value, Verbose.Value);
				Log.LogInfo($"day {today}{(DryRun.Value ? " (dry run)" : "")}: {r.Chests} loot chests, " +
					$"{r.Restocked} restocked, {r.Stamped} newly empty, {r.Waiting} waiting, {r.Cleared} refilled by players, " +
					$"{r.Loaded} skipped as loaded by a player" + (r.RolledNothing > 0 ? $", {r.RolledNothing} rolled nothing (retry next day)" : "") +
					$", {watch.ElapsedMilliseconds} ms");
			}
			catch (Exception e)
			{
				Log.LogError($"sweep failed: {e}");
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
