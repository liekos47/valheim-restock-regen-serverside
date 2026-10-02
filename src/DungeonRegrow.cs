using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace RestockRegen
{
	/*
		Things inside a dungeon that come back when nobody has visited that dungeon for a number of
		in-game days. One DungeonRegrow per kind:

		- Mud piles, sunken crypts (DG_SunkenCrypt). An untouched pile is "mudpile2"; the first
		  pickaxe hit destroys it and spawns "mudpile2_frac" in its place (Destructible
		  m_spawnWhenDestroyed), a MineRock5 whose last chunk deletes it.
		- Crystals, frost caves (DG_Cave): "Pickable_MountainCaveCrystal". It has no respawn time, so
		  picking one deletes it (Pickable.RPC_Pick). On the owner's world 32 picked between
		  2026-09-18 and 09-25 had not come back by 09-29.

		- Black cores, infested mines (DG_DvergrTown): "Pickable_BlackCoreStand". This one is not
		  deleted: the stand stays and the object is marked ZDOVars.s_picked, for good (39 of 172 on
		  the owner's world on 2026-09-30). Restoring it is clearing that flag, which Pickable.Awake
		  reads the next time a client loads it; nothing is created or removed.

		For the first two nothing in the world says the thing was ever there, so every spot - position,
		rotation and whole prefab - is remembered from the moment it is seen, whole or damaged, and
		stored on the dungeon's own generator ZDO so it survives restarts. Things taken before the mod
		saw them cannot be brought back.

		A dungeon's interior sits about 5000 m above its entrance, around its generator. A player
		whose reference position is up there and nearer this dungeon's generator than any other
		dungeon's counts as visiting it, and any visit resets its clock.

		Regenerating a dungeon: every remembered spot with no whole object gets one again. A damaged
		one there is removed first. The new one is created the way ZNetView.Awake does for a new
		object (WorldObjects.Create). It only happens when no player holds any of the dungeon's
		objects and nobody is up there.

		A player at the entrance of a dungeon with something taken is told how many days are left on
		its clock - it is still running, since standing outside is not a visit. If they then go in,
		they are told again, now with the full count, because their visit has just restarted it.
		Standing at the entrance is not a visit; only being inside is. A sunken crypt's gate stands
		1-3 m, measured flat, from its interior's generator; a frost cave's surface location about
		30 m, so each kind has its own entrance radius.

		As with chests, nothing is written to an object a player owns: the generator's records wait
		in memory until it is free.
	*/
	internal class DungeonRegrow
	{
		private const float InteriorHeight = 4000f; // dungeon interiors are generated around y = 5000

		// Everything a kind needs; the mud pile settings keep the names they had before this class.
		internal class Kind
		{
			public string Label, DungeonLabel, Generator, Singular, Plural, SpotsKey, VisitKey;
			public string[] Names;
			public ConfigEntry<bool> On, DryRun, RegenNow;
			public ConfigEntry<int> Days;
			public ConfigEntry<string> NotifyText, EntranceText, DueText;
			public ConfigEntry<float> EntranceRadius, Radius;
		}

		internal static readonly List<DungeonRegrow> All = new List<DungeonRegrow>();

		private struct Spot
		{
			public int Prefab; // the whole object's prefab
			public Vector3 Pos;
			public Vector3 Rot;
		}

		private class Dungeon
		{
			public ZDOID Generator;
			public Vector3 Pos;
			public readonly Dictionary<(int, int, int), Spot> Spots = new Dictionary<(int, int, int), Spot>();
			public int LastVisit = -1;
			public bool SpotsDirty, VisitDirty;
		}

		internal struct Result
		{
			public int Dungeons, Waiting, Occupied, Regenerated, Rebuilt, Replaced;
		}

		internal readonly Kind K;
		private readonly int m_generator, m_spotsKey, m_visitKey;
		// Tracked prefab hash (whole or damaged) -> the whole prefab hash.
		private readonly Dictionary<int, int> m_intactOf = new Dictionary<int, int>();
		private readonly HashSet<int> m_intact = new HashSet<int>();
		private readonly Dictionary<ZDOID, Dungeon> m_dungeons = new Dictionary<ZDOID, Dungeon>();
		private readonly List<Vector3> m_otherDungeons = new List<Vector3>();
		// Spots seen by the Deserialize hook between sweeps, not yet assigned to a dungeon.
		private readonly Dictionary<(int, int, int), Spot> m_seen = new Dictionary<(int, int, int), Spot>();
		// The dungeon each player was last told about and where (1 = entrance, 2 = inside), so a trip
		// gets one message at the door and one more on going in. Forgotten once they are neither
		// inside it nor within twice the entrance radius of it.
		private readonly Dictionary<long, (ZDOID Dungeon, int Stage)> m_welcomed = new Dictionary<long, (ZDOID, int)>();
		private readonly List<ZDO> m_nearby = new List<ZDO>();
		private bool m_ready;

		internal DungeonRegrow(Kind kind)
		{
			K = kind;
			m_generator = kind.Generator.GetStableHashCode();
			m_spotsKey = kind.SpotsKey.GetStableHashCode();
			m_visitKey = kind.VisitKey.GetStableHashCode();
		}

		private static (int, int, int) Key(Vector3 p) =>
			(Mathf.RoundToInt(p.x * 10f), Mathf.RoundToInt(p.y * 10f), Mathf.RoundToInt(p.z * 10f));

		private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

		// Whole: the whole prefab, and not a pickable that has been picked and left standing.
		private bool IsWhole(ZDO zdo) => m_intact.Contains(zdo.GetPrefab()) && !zdo.GetBool(ZDOVars.s_picked);

		internal void Init()
		{
			m_intactOf.Clear();
			m_intact.Clear();
			var dungeonPrefabs = new HashSet<int>();
			foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
			{
				if (prefab != null && prefab.GetComponent<DungeonGenerator>() != null)
				{
					dungeonPrefabs.Add(prefab.name.GetStableHashCode());
				}
			}
			dungeonPrefabs.Add(m_generator);
			foreach (string name in K.Names)
			{
				if (ZNetScene.instance.GetPrefab(name) == null)
				{
					continue;
				}
				int whole = name.GetStableHashCode();
				m_intact.Add(whole);
				m_intactOf[whole] = whole;
				foreach (string suffix in new[] { "_frac", "_destruction" })
				{
					if (ZNetScene.instance.GetPrefab(name + suffix) != null)
					{
						m_intactOf[(name + suffix).GetStableHashCode()] = whole;
					}
				}
			}

			m_dungeons.Clear();
			m_otherDungeons.Clear();
			var things = new List<ZDO>();
			foreach (ZDO zdo in ZDOMan.instance.m_objectsByID.Values)
			{
				int prefab = zdo.GetPrefab();
				if (prefab == m_generator)
				{
					AddDungeon(zdo);
				}
				else if (dungeonPrefabs.Contains(prefab))
				{
					m_otherDungeons.Add(zdo.GetPosition());
				}
				else if (m_intactOf.ContainsKey(prefab))
				{
					things.Add(zdo);
				}
			}
			int added = 0;
			foreach (ZDO thing in things)
			{
				if (Remember(SpotOf(thing)))
				{
					added++;
				}
			}
			m_ready = true;
			int spots = m_dungeons.Values.Sum(c => c.Spots.Count);
			int intactNow = things.Count(IsWhole);
			foreach (string name in K.Names)
			{
				// What the game itself does with a picked one decides how it is restored; logged so a
				// game update that changes it is noticed.
				Pickable pickable = ZNetScene.instance.GetPrefab(name)?.GetComponent<Pickable>();
				if (pickable != null)
				{
					RestockRegenPlugin.Log.LogInfo($"{K.Label}: {name} is a pickable, game respawn {pickable.m_respawnTimeMinutes} min, " +
						(pickable.m_hideWhenPicked != null ? "stays in the world when picked" : "deleted when picked"));
				}
			}
			RestockRegenPlugin.Log.LogInfo(
				$"{K.Label}: {m_dungeons.Count} {K.DungeonLabel}s, {spots} spots remembered ({added} new this start), " +
				$"{intactNow} whole and {things.Count - intactNow} damaged in the world now{(K.On.Value ? "" : " (off)")}{(K.DryRun.Value ? " (dry run)" : "")}");
		}

		private Dungeon AddDungeon(ZDO generator)
		{
			if (m_dungeons.TryGetValue(generator.m_uid, out Dungeon dungeon))
			{
				return dungeon;
			}
			dungeon = new Dungeon { Generator = generator.m_uid, Pos = generator.GetPosition() };
			int visit = generator.GetInt(m_visitKey);
			dungeon.LastVisit = visit > 0 ? visit - 1 : -1;
			byte[] data = generator.GetByteArray(m_spotsKey);
			if (data != null)
			{
				var pkg = new ZPackage(data);
				if (pkg.ReadInt() == 1)
				{
					int n = pkg.ReadInt();
					for (int i = 0; i < n; i++)
					{
						var spot = new Spot { Prefab = pkg.ReadInt(), Pos = pkg.ReadVector3(), Rot = pkg.ReadVector3() };
						dungeon.Spots[Key(spot.Pos)] = spot;
					}
				}
			}
			m_dungeons[generator.m_uid] = dungeon;
			return dungeon;
		}

		private Spot SpotOf(ZDO thing) =>
			new Spot { Prefab = m_intactOf[thing.GetPrefab()], Pos = thing.GetPosition(), Rot = thing.GetRotation().eulerAngles };

		// The dungeon of this kind whose interior a point is in, or null: up at interior height and
		// nearer this dungeon's generator than any other dungeon's, within the kind's radius.
		private Dungeon DungeonAt(Vector3 p)
		{
			if (p.y < InteriorHeight)
			{
				return null;
			}
			Dungeon best = null;
			float bestDist = K.Radius.Value;
			foreach (Dungeon dungeon in m_dungeons.Values)
			{
				float d = Flat(p, dungeon.Pos);
				if (d < bestDist && Mathf.Abs(p.y - dungeon.Pos.y) < 500f)
				{
					best = dungeon;
					bestDist = d;
				}
			}
			if (best != null && m_otherDungeons.Any(o => o.y > InteriorHeight && Flat(p, o) < bestDist))
			{
				return null; // nearer another kind of dungeon
			}
			return best;
		}

		// The dungeon of this kind whose entrance a point on the surface is at, or null.
		private Dungeon EntranceAt(Vector3 p, float radius)
		{
			if (p.y >= InteriorHeight)
			{
				return null;
			}
			Dungeon best = null;
			float bestDist = radius;
			foreach (Dungeon dungeon in m_dungeons.Values)
			{
				float d = Flat(p, dungeon.Pos);
				if (d < bestDist)
				{
					best = dungeon;
					bestDist = d;
				}
			}
			return best;
		}

		private bool Remember(Spot spot)
		{
			Dungeon dungeon = DungeonAt(spot.Pos);
			if (dungeon == null)
			{
				m_seen[Key(spot.Pos)] = spot; // its dungeon's generator is not known yet
				return false;
			}
			var key = Key(spot.Pos);
			if (dungeon.Spots.ContainsKey(key))
			{
				return false;
			}
			dungeon.Spots[key] = spot;
			dungeon.SpotsDirty = true;
			return true;
		}

		// Every VisitCheckSeconds: note which dungeons have a player inside, tell players at an
		// entrance or inside about taken things, and write out pending records.
		internal void Tick(int today)
		{
			if (!m_ready || !K.On.Value)
			{
				return;
			}
			float door = K.EntranceRadius.Value;
			var present = new HashSet<long>();
			foreach (ZNetPeer peer in ZNet.instance.GetPeers())
			{
				present.Add(peer.m_uid);
				Vector3 p = peer.GetRefPos();
				Dungeon inside = DungeonAt(p);
				if (inside != null && inside.LastVisit != today)
				{
					inside.LastVisit = today;
					inside.VisitDirty = true;
				}
				Dungeon here = inside ?? EntranceAt(p, door);
				if (here == null)
				{
					// Keep the record while they are still around the entrance, so walking in and
					// out of the radius does not repeat the message.
					if (m_welcomed.TryGetValue(peer.m_uid, out var last)
						&& !(m_dungeons.TryGetValue(last.Dungeon, out Dungeon c) && (p.y < InteriorHeight ? Flat(p, c.Pos) < door * 2f : DungeonAt(p) == c)))
					{
						m_welcomed.Remove(peer.m_uid);
					}
					continue;
				}
				int stage = inside != null ? 2 : 1;
				// Coming back out to the entrance after being inside is not told again.
				if (!m_welcomed.TryGetValue(peer.m_uid, out var was) || was.Dungeon != here.Generator || stage > was.Stage)
				{
					m_welcomed[peer.m_uid] = (here.Generator, stage);
					Welcome(peer, here, inside != null, today);
				}
			}
			foreach (long gone in m_welcomed.Keys.Where(uid => !present.Contains(uid)).ToList())
			{
				m_welcomed.Remove(gone);
			}
			Flush();
		}

		// Tell a player at or in a dungeon that what was taken will come back, if anything was.
		private void Welcome(ZNetPeer peer, Dungeon dungeon, bool inside, int today)
		{
			if (!RestockRegenPlugin.Notify.Value || dungeon.Spots.Count == 0)
			{
				return;
			}
			// Only the zones around the dungeon, not the whole world: 4 zones of 64 m either side.
			m_nearby.Clear();
			ZDOMan.instance.FindSectorObjects(ZoneSystem.GetZone(dungeon.Pos), new SimulationDistance(4, 0, true), m_nearby);
			var intact = new HashSet<(int, int, int)>(m_nearby.Where(IsWhole).Select(z => Key(z.GetPosition())));
			int taken = dungeon.Spots.Keys.Count(k => !intact.Contains(k));
			if (taken == 0)
			{
				return;
			}
			// Inside, the visit has just restarted the clock. At the entrance it is still running.
			int days = Mathf.Max(1, K.Days.Value);
			if (!inside && dungeon.LastVisit >= 0)
			{
				days -= today - dungeon.LastVisit;
			}
			string template = inside ? K.NotifyText.Value : days > 0 ? K.EntranceText.Value : K.DueText.Value;
			string text = template
				.Replace("{mined}", taken.ToString())
				.Replace("{piles}", taken == 1 ? K.Singular : K.Plural)
				.Replace("{days}", days == 1 ? "1 day" : $"{days} days");
			ZRoutedRpc.instance.InvokeRoutedRPC(peer.m_uid, "ShowMessage", (int)MessageHud.MessageType.Center, text);
			if (RestockRegenPlugin.Verbose.Value)
			{
				RestockRegenPlugin.Log.LogInfo($"told {peer.m_playerName}: {text}");
			}
		}

		internal void FlushPending()
		{
			if (m_ready)
			{
				Flush();
			}
		}

		internal string Summary()
		{
			if (!m_ready)
			{
				return $"{K.Label} not started";
			}
			return $"{m_dungeons.Count} {K.DungeonLabel}s, {m_dungeons.Values.Sum(c => c.Spots.Count)} {K.Label} spots known" +
				(K.On.Value ? "" : " (off)") + (K.DryRun.Value ? " (DRY RUN)" : "");
		}

		private void Flush()
		{
			if (K.DryRun.Value)
			{
				return;
			}
			long server = ZDOMan.GetSessionID();
			foreach (Dungeon dungeon in m_dungeons.Values)
			{
				if (!dungeon.SpotsDirty && !dungeon.VisitDirty)
				{
					continue;
				}
				ZDO generator = ZDOMan.instance.GetZDO(dungeon.Generator);
				if (generator == null || (generator.HasOwner() && generator.GetOwner() != server))
				{
					continue;
				}
				if (dungeon.VisitDirty)
				{
					generator.Set(m_visitKey, dungeon.LastVisit + 1);
					dungeon.VisitDirty = false;
				}
				if (dungeon.SpotsDirty)
				{
					var pkg = new ZPackage();
					pkg.Write(1);
					pkg.Write(dungeon.Spots.Count);
					foreach (Spot spot in dungeon.Spots.Values)
					{
						pkg.Write(spot.Prefab);
						pkg.Write(spot.Pos);
						pkg.Write(spot.Rot);
					}
					generator.Set(m_spotsKey, pkg.GetArray());
					dungeon.SpotsDirty = false;
				}
			}
		}

		// Once per in-game day, and for the admin regen command.
		internal Result Sweep(int today)
		{
			var result = new Result();
			if (!m_ready || !K.On.Value)
			{
				return result;
			}
			bool dryRun = K.DryRun.Value;
			// One-shot: every dungeon with something taken regenerates at this sweep, whatever its clock.
			bool now = K.RegenNow.Value;
			bool verbose = RestockRegenPlugin.Verbose.Value;
			int days = Mathf.Max(1, K.Days.Value);
			long server = ZDOMan.GetSessionID();

			// Index what is standing now, pick up dungeons and things that appeared since the last
			// sweep, and assign spots the hook saw before their dungeon was known.
			var standing = new Dictionary<(int, int, int), ZDO>();
			foreach (ZDO zdo in ZDOMan.instance.m_objectsByID.Values)
			{
				int prefab = zdo.GetPrefab();
				if (prefab == m_generator)
				{
					AddDungeon(zdo);
				}
				else if (m_intactOf.ContainsKey(prefab))
				{
					var key = Key(zdo.GetPosition());
					// A whole one wins over a damaged one at the same spot.
					if (!standing.TryGetValue(key, out ZDO other) || !IsWhole(other))
					{
						standing[key] = zdo;
					}
				}
			}
			foreach (ZDO thing in standing.Values)
			{
				Remember(SpotOf(thing));
			}
			foreach (Spot spot in m_seen.Values.ToList())
			{
				m_seen.Remove(Key(spot.Pos));
				Remember(spot);
			}

			foreach (Dungeon dungeon in m_dungeons.Values)
			{
				result.Dungeons++;
				if (dungeon.LastVisit < 0 && !now)
				{
					dungeon.LastVisit = today; // first sight: start the clock now, not at day 0
					dungeon.VisitDirty = true;
					continue;
				}
				int unvisited = dungeon.LastVisit < 0 ? days : today - dungeon.LastVisit;
				if (unvisited < days && !now)
				{
					result.Waiting++;
					continue;
				}
				var missing = dungeon.Spots.Where(kv => !(standing.TryGetValue(kv.Key, out ZDO z) && IsWhole(z))).ToList();
				if (missing.Count == 0)
				{
					continue; // nothing taken
				}
				if (Occupied(dungeon, standing, server))
				{
					result.Occupied++;
					if (now)
					{
						// Regenerate-now could not reach it: make it due, so the first sweep that
						// finds it free does it, unless someone visits in between.
						dungeon.LastVisit = today - days;
						dungeon.VisitDirty = true;
					}
					continue;
				}
				int replaced = 0, rebuilt = 0;
				foreach (var kv in missing)
				{
					if (standing.TryGetValue(kv.Key, out ZDO damaged))
					{
						replaced++;
						if (m_intact.Contains(damaged.GetPrefab()))
						{
							// The whole prefab, picked and left standing: unpick it in place.
							if (!dryRun)
							{
								damaged.Set(ZDOVars.s_picked, false);
								damaged.Set(ZDOVars.s_pickedTime, 0L);
							}
							continue;
						}
						if (!dryRun)
						{
							WorldObjects.Remove(damaged);
						}
					}
					else
					{
						rebuilt++;
					}
					if (!dryRun)
					{
						WorldObjects.Create(kv.Value.Prefab, kv.Value.Pos, kv.Value.Rot);
					}
				}
				result.Regenerated++;
				result.Replaced += replaced;
				result.Rebuilt += rebuilt;
				dungeon.LastVisit = today; // the clock starts again
				dungeon.VisitDirty = true;
				if (verbose)
				{
					RestockRegenPlugin.Log.LogInfo(
						$"{(dryRun ? "dry run: would regenerate" : "regenerated")} {K.DungeonLabel} at ({dungeon.Pos.x:0}, {dungeon.Pos.z:0}) " +
						(now ? "(regenerate now): " : $"after {unvisited} days unvisited: ") + $"{replaced} damaged {K.Plural} replaced, {rebuilt} gone {K.Plural} rebuilt, " +
						$"{dungeon.Spots.Count - missing.Count} untouched");
				}
			}
			Flush();
			if (now && !dryRun)
			{
				K.RegenNow.Value = false; // saved to the config file
				RestockRegenPlugin.Log.LogInfo($"{K.Label}: regenerate-now done, {K.RegenNow.Definition.Key} set back to false");
			}
			return result;
		}

		// A player up in the dungeon, or holding any of its objects.
		private bool Occupied(Dungeon dungeon, Dictionary<(int, int, int), ZDO> standing, long server)
		{
			if (ZNet.instance.GetPeers().Any(p => DungeonAt(p.GetRefPos()) == dungeon))
			{
				return true;
			}
			if (WorldObjects.HeldByPlayer(ZDOMan.instance.GetZDO(dungeon.Generator)))
			{
				return true;
			}
			foreach (var key in dungeon.Spots.Keys)
			{
				if (standing.TryGetValue(key, out ZDO zdo) && zdo.HasOwner() && zdo.GetOwner() != server)
				{
					return true;
				}
			}
			return false;
		}

		// Remember a thing the moment a player's update brings it in, so one generated and taken
		// between two sweeps is not lost.
		[HarmonyPatch(typeof(ZDO), nameof(ZDO.Deserialize))]
		private static class SeeThings
		{
			private static void Postfix(ZDO __instance)
			{
				foreach (DungeonRegrow kind in All)
				{
					if (kind.m_ready && kind.m_intactOf.ContainsKey(__instance.GetPrefab()) && kind.K.On.Value)
					{
						kind.Remember(kind.SpotOf(__instance));
					}
				}
			}
		}
	}
}
