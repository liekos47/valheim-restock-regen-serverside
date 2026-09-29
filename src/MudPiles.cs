using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace RestockRegen
{
	/*
		Muddy scrap piles in sunken crypts come back when nobody has visited the crypt for
		MudPileDays in-game days.

		A pile is not marked as mined, it is replaced. An untouched pile is "mudpile2". The first
		pickaxe hit destroys it and spawns "mudpile2_frac" in its place (Destructible
		m_spawnWhenDestroyed), a MineRock5 with a health value per chunk. Breaking the last chunk
		deletes that too, and then nothing in the world says a pile was ever there. So this module
		remembers every pile's spot - position, rotation and intact prefab - from the moment it sees
		the pile, intact or fractured, and stores the list on the crypt's own generator ZDO
		(DG_SunkenCrypt) so it survives restarts. Piles mined out before the mod saw them cannot be
		brought back.

		A crypt's interior sits about 5000 m above its entrance, around its generator. A player
		whose reference position is up there and nearer this crypt's generator than any other
		dungeon's counts as visiting it, and any visit resets its clock.

		Regenerating a crypt: every remembered spot with no intact pile gets one again. A fractured
		pile there is destroyed first (ZDOMan.DestroyZDO, which the server handles itself and sends
		to every client). The new pile is created the way ZNetView.Awake does for a new object:
		CreateNewZDO, then Persistent/Type/Distant from the prefab's ZNetView, prefab and rotation.
		It only happens when no player holds any of the crypt's objects and nobody is up there.

		A player at the entrance of a crypt with mined piles is told how many days are left on its
		clock - it is still running, since standing outside is not a visit. If they then go in, they
		are told again, now with the full MudPileDays, because their visit has just restarted it. The
		entrance (sunken_crypt_gate) stands on the surface 1-3 m, measured flat, from the interior's
		generator, so "at the entrance" is on the ground within EntranceRadius of it. Standing there
		is not a visit; only being inside is. Since every visit restarts the
		count, that is always the full MudPileDays from when the last visitor leaves.

		As with chests, nothing is written to an object a player owns: the generator's records wait
		in memory until it is free.
	*/
	internal static class MudPiles
	{
		private const float InteriorHeight = 4000f; // dungeon interiors are generated around y = 5000
		private static readonly int s_spotsKey = "restockregen_mudspots".GetStableHashCode();
		private static readonly int s_visitKey = "restockregen_lastvisit".GetStableHashCode();
		private static readonly int s_sunkenCrypt = "DG_SunkenCrypt".GetStableHashCode();

		private struct Spot
		{
			public int Prefab; // the intact pile's prefab
			public Vector3 Pos;
			public Vector3 Rot;
		}

		private class Crypt
		{
			public ZDOID Generator;
			public Vector3 Pos;
			public readonly Dictionary<(int, int, int), Spot> Spots = new Dictionary<(int, int, int), Spot>();
			public int LastVisit = -1;
			public bool SpotsDirty, VisitDirty;
		}

		// Pile prefab hash (intact or fractured) -> the intact prefab hash.
		private static readonly Dictionary<int, int> s_intactOf = new Dictionary<int, int>();
		private static readonly HashSet<int> s_intact = new HashSet<int>();
		private static readonly HashSet<int> s_dungeonPrefabs = new HashSet<int>();
		private static readonly Dictionary<ZDOID, Crypt> s_crypts = new Dictionary<ZDOID, Crypt>();
		private static readonly List<Vector3> s_otherDungeons = new List<Vector3>();
		// Spots seen by the Deserialize hook between sweeps, not yet assigned to a crypt.
		private static readonly Dictionary<(int, int, int), Spot> s_seen = new Dictionary<(int, int, int), Spot>();
		private static bool s_ready;
		// The crypt each player was last told about and where (1 = entrance, 2 = inside), so a trip
		// gets one message at the door and one more on going in. Forgotten once they are neither
		// inside it nor within twice EntranceRadius of its entrance.
		private static readonly Dictionary<long, (ZDOID Crypt, int Stage)> s_welcomed = new Dictionary<long, (ZDOID, int)>();
		private static readonly List<ZDO> s_nearby = new List<ZDO>();

		private static (int, int, int) Key(Vector3 p) =>
			(Mathf.RoundToInt(p.x * 10f), Mathf.RoundToInt(p.y * 10f), Mathf.RoundToInt(p.z * 10f));

		private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

		internal static void Init()
		{
			s_intactOf.Clear();
			s_intact.Clear();
			s_dungeonPrefabs.Clear();
			foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
			{
				if (prefab != null && prefab.GetComponent<DungeonGenerator>() != null)
				{
					s_dungeonPrefabs.Add(prefab.name.GetStableHashCode());
				}
			}
			s_dungeonPrefabs.Add(s_sunkenCrypt);
			foreach (string name in new[] { "mudpile2", "mudpile" })
			{
				if (ZNetScene.instance.GetPrefab(name) == null)
				{
					continue;
				}
				int intact = name.GetStableHashCode();
				s_intact.Add(intact);
				s_intactOf[intact] = intact;
				if (ZNetScene.instance.GetPrefab(name + "_frac") != null)
				{
					s_intactOf[(name + "_frac").GetStableHashCode()] = intact;
				}
			}

			s_crypts.Clear();
			s_otherDungeons.Clear();
			var piles = new List<ZDO>();
			foreach (ZDO zdo in ZDOMan.instance.m_objectsByID.Values)
			{
				int prefab = zdo.GetPrefab();
				if (prefab == s_sunkenCrypt)
				{
					AddCrypt(zdo);
				}
				else if (s_dungeonPrefabs.Contains(prefab))
				{
					s_otherDungeons.Add(zdo.GetPosition());
				}
				else if (s_intactOf.ContainsKey(prefab))
				{
					piles.Add(zdo);
				}
			}
			int added = 0;
			foreach (ZDO pile in piles)
			{
				if (Remember(SpotOf(pile)))
				{
					added++;
				}
			}
			s_ready = true;
			int spots = s_crypts.Values.Sum(c => c.Spots.Count);
			int intactNow = piles.Count(p => s_intact.Contains(p.GetPrefab()));
			RestockRegenPlugin.Log.LogInfo(
				$"mud piles: {s_crypts.Count} sunken crypts, {spots} pile spots remembered ({added} new this start), " +
				$"{intactNow} intact and {piles.Count - intactNow} partly mined in the world now");
		}

		private static Crypt AddCrypt(ZDO generator)
		{
			if (s_crypts.TryGetValue(generator.m_uid, out Crypt crypt))
			{
				return crypt;
			}
			crypt = new Crypt { Generator = generator.m_uid, Pos = generator.GetPosition() };
			int visit = generator.GetInt(s_visitKey);
			crypt.LastVisit = visit > 0 ? visit - 1 : -1;
			byte[] data = generator.GetByteArray(s_spotsKey);
			if (data != null)
			{
				var pkg = new ZPackage(data);
				if (pkg.ReadInt() == 1)
				{
					int n = pkg.ReadInt();
					for (int i = 0; i < n; i++)
					{
						var spot = new Spot { Prefab = pkg.ReadInt(), Pos = pkg.ReadVector3(), Rot = pkg.ReadVector3() };
						crypt.Spots[Key(spot.Pos)] = spot;
					}
				}
			}
			s_crypts[generator.m_uid] = crypt;
			return crypt;
		}

		private static Spot SpotOf(ZDO pile) =>
			new Spot { Prefab = s_intactOf[pile.GetPrefab()], Pos = pile.GetPosition(), Rot = pile.GetRotation().eulerAngles };

		// The sunken crypt whose interior a point is in, or null: up at interior height and nearer
		// this crypt's generator than any other dungeon's, within CryptRadius.
		private static Crypt CryptAt(Vector3 p)
		{
			if (p.y < InteriorHeight)
			{
				return null;
			}
			float radius = RestockRegenPlugin.CryptRadius.Value;
			Crypt best = null;
			float bestDist = radius;
			foreach (Crypt crypt in s_crypts.Values)
			{
				float d = Flat(p, crypt.Pos);
				if (d < bestDist && Mathf.Abs(p.y - crypt.Pos.y) < 500f)
				{
					best = crypt;
					bestDist = d;
				}
			}
			if (best != null && s_otherDungeons.Any(o => o.y > InteriorHeight && Flat(p, o) < bestDist))
			{
				return null; // nearer another kind of dungeon
			}
			return best;
		}

		// The sunken crypt whose entrance a point on the surface is at, or null.
		private static Crypt EntranceAt(Vector3 p, float radius)
		{
			if (p.y >= InteriorHeight)
			{
				return null;
			}
			Crypt best = null;
			float bestDist = radius;
			foreach (Crypt crypt in s_crypts.Values)
			{
				float d = Flat(p, crypt.Pos);
				if (d < bestDist)
				{
					best = crypt;
					bestDist = d;
				}
			}
			return best;
		}

		private static bool Remember(Spot spot)
		{
			Crypt crypt = CryptAt(spot.Pos);
			if (crypt == null)
			{
				s_seen[Key(spot.Pos)] = spot; // its crypt's generator is not known yet
				return false;
			}
			var key = Key(spot.Pos);
			if (crypt.Spots.ContainsKey(key))
			{
				return false;
			}
			crypt.Spots[key] = spot;
			crypt.SpotsDirty = true;
			return true;
		}

		// Every VisitCheckSeconds: note which crypts have a player inside, tell players at an
		// entrance or inside about mined piles, and write out pending records.
		internal static void Tick(int today)
		{
			if (!s_ready || !RestockRegenPlugin.MudPiles.Value)
			{
				return;
			}
			float door = RestockRegenPlugin.EntranceRadius.Value;
			var present = new HashSet<long>();
			foreach (ZNetPeer peer in ZNet.instance.GetPeers())
			{
				present.Add(peer.m_uid);
				Vector3 p = peer.GetRefPos();
				Crypt inside = CryptAt(p);
				if (inside != null && inside.LastVisit != today)
				{
					inside.LastVisit = today;
					inside.VisitDirty = true;
				}
				Crypt here = inside ?? EntranceAt(p, door);
				if (here == null)
				{
					// Keep the record while they are still around the entrance, so walking in and
					// out of the radius does not repeat the message.
					if (s_welcomed.TryGetValue(peer.m_uid, out var last)
						&& !(s_crypts.TryGetValue(last.Crypt, out Crypt c) && (p.y < InteriorHeight ? Flat(p, c.Pos) < door * 2f : CryptAt(p) == c)))
					{
						s_welcomed.Remove(peer.m_uid);
					}
					continue;
				}
				int stage = inside != null ? 2 : 1;
				// Coming back out to the entrance after being inside is not told again.
				if (!s_welcomed.TryGetValue(peer.m_uid, out var was) || was.Crypt != here.Generator || stage > was.Stage)
				{
					s_welcomed[peer.m_uid] = (here.Generator, stage);
					Welcome(peer, here, inside != null, today);
				}
			}
			foreach (long gone in s_welcomed.Keys.Where(uid => !present.Contains(uid)).ToList())
			{
				s_welcomed.Remove(gone);
			}
			Flush();
		}

		// Tell a player at or in a crypt that its mined piles will come back, if any are mined.
		private static void Welcome(ZNetPeer peer, Crypt crypt, bool inside, int today)
		{
			if (!RestockRegenPlugin.Notify.Value || crypt.Spots.Count == 0)
			{
				return;
			}
			// Only the zones around the crypt, not the whole world: 4 zones of 64 m either side.
			s_nearby.Clear();
			ZDOMan.instance.FindSectorObjects(ZoneSystem.GetZone(crypt.Pos), new SimulationDistance(4, 0, true), s_nearby);
			var intact = new HashSet<(int, int, int)>(s_nearby.Where(z => s_intact.Contains(z.GetPrefab())).Select(z => Key(z.GetPosition())));
			int mined = crypt.Spots.Keys.Count(k => !intact.Contains(k));
			if (mined == 0)
			{
				return;
			}
			// Inside, the visit has just restarted the clock. At the entrance it is still running.
			int days = Mathf.Max(1, RestockRegenPlugin.MudPileDays.Value);
			if (!inside && crypt.LastVisit >= 0)
			{
				days -= today - crypt.LastVisit;
			}
			string template = inside ? RestockRegenPlugin.MudPileNotifyText.Value
				: days > 0 ? RestockRegenPlugin.MudPileEntranceText.Value
				: RestockRegenPlugin.MudPileDueText.Value;
			string text = template
				.Replace("{mined}", mined.ToString())
				.Replace("{piles}", mined == 1 ? "muddy scrap pile" : "muddy scrap piles")
				.Replace("{days}", days == 1 ? "1 day" : $"{days} days");
			ZRoutedRpc.instance.InvokeRoutedRPC(peer.m_uid, "ShowMessage", (int)MessageHud.MessageType.Center, text);
			if (RestockRegenPlugin.Verbose.Value)
			{
				RestockRegenPlugin.Log.LogInfo($"told {peer.m_playerName}: {text}");
			}
		}

		private static void Flush()
		{
			if (RestockRegenPlugin.MudPileDryRun.Value)
			{
				return;
			}
			long server = ZDOMan.GetSessionID();
			foreach (Crypt crypt in s_crypts.Values)
			{
				if (!crypt.SpotsDirty && !crypt.VisitDirty)
				{
					continue;
				}
				ZDO generator = ZDOMan.instance.GetZDO(crypt.Generator);
				if (generator == null || (generator.HasOwner() && generator.GetOwner() != server))
				{
					continue;
				}
				if (crypt.VisitDirty)
				{
					generator.Set(s_visitKey, crypt.LastVisit + 1);
					crypt.VisitDirty = false;
				}
				if (crypt.SpotsDirty)
				{
					var pkg = new ZPackage();
					pkg.Write(1);
					pkg.Write(crypt.Spots.Count);
					foreach (Spot spot in crypt.Spots.Values)
					{
						pkg.Write(spot.Prefab);
						pkg.Write(spot.Pos);
						pkg.Write(spot.Rot);
					}
					generator.Set(s_spotsKey, pkg.GetArray());
					crypt.SpotsDirty = false;
				}
			}
		}

		internal struct Result
		{
			public int Crypts, Waiting, Occupied, Regenerated, Rebuilt, Replaced;
		}

		// Once per in-game day.
		internal static Result Sweep(int today)
		{
			var result = new Result();
			if (!s_ready || !RestockRegenPlugin.MudPiles.Value)
			{
				return result;
			}
			bool dryRun = RestockRegenPlugin.MudPileDryRun.Value;
			// One-shot: every crypt with mined piles regenerates at this sweep, whatever its clock.
			bool now = RestockRegenPlugin.MudPileRegenNow.Value;
			bool verbose = RestockRegenPlugin.Verbose.Value;
			int days = Mathf.Max(1, RestockRegenPlugin.MudPileDays.Value);
			long server = ZDOMan.GetSessionID();

			// Index what is standing now, pick up crypts and piles that appeared since the last
			// sweep, and assign spots the hook saw before their crypt was known.
			var standing = new Dictionary<(int, int, int), ZDO>();
			foreach (ZDO zdo in ZDOMan.instance.m_objectsByID.Values)
			{
				int prefab = zdo.GetPrefab();
				if (prefab == s_sunkenCrypt)
				{
					AddCrypt(zdo);
				}
				else if (s_intactOf.ContainsKey(prefab))
				{
					var key = Key(zdo.GetPosition());
					// An intact pile wins over a fractured one at the same spot.
					if (!standing.TryGetValue(key, out ZDO other) || !s_intact.Contains(other.GetPrefab()))
					{
						standing[key] = zdo;
					}
				}
			}
			foreach (ZDO pile in standing.Values)
			{
				Remember(SpotOf(pile));
			}
			foreach (Spot spot in s_seen.Values.ToList())
			{
				s_seen.Remove(Key(spot.Pos));
				Remember(spot);
			}

			foreach (Crypt crypt in s_crypts.Values)
			{
				result.Crypts++;
				if (crypt.LastVisit < 0 && !now)
				{
					crypt.LastVisit = today; // first sight: start the clock now, not at day 0
					crypt.VisitDirty = true;
					continue;
				}
				int unvisited = crypt.LastVisit < 0 ? days : today - crypt.LastVisit;
				if (unvisited < days && !now)
				{
					result.Waiting++;
					continue;
				}
				var missing = crypt.Spots.Where(kv => !(standing.TryGetValue(kv.Key, out ZDO z) && s_intact.Contains(z.GetPrefab()))).ToList();
				if (missing.Count == 0)
				{
					continue; // nothing mined
				}
				if (Occupied(crypt, standing, server))
				{
					result.Occupied++;
					if (now)
					{
						// Regenerate-now could not reach it: make it due, so the first sweep that
						// finds it free does it, unless someone visits in between.
						crypt.LastVisit = today - days;
						crypt.VisitDirty = true;
					}
					continue;
				}
				int replaced = 0, rebuilt = 0;
				foreach (var kv in missing)
				{
					if (standing.TryGetValue(kv.Key, out ZDO frac))
					{
						replaced++;
						if (!dryRun)
						{
							frac.SetOwner(server);
							ZDOMan.instance.DestroyZDO(frac);
						}
					}
					else
					{
						rebuilt++;
					}
					if (!dryRun)
					{
						Create(kv.Value);
					}
				}
				result.Regenerated++;
				result.Replaced += replaced;
				result.Rebuilt += rebuilt;
				crypt.LastVisit = today; // the clock starts again
				crypt.VisitDirty = true;
				if (verbose)
				{
					RestockRegenPlugin.Log.LogInfo(
						$"{(dryRun ? "dry run: would regenerate" : "regenerated")} sunken crypt at ({crypt.Pos.x:0}, {crypt.Pos.z:0}) " +
						(now ? "(regenerate now): " : $"after {unvisited} days unvisited: ") + $"{replaced} partly mined piles replaced, {rebuilt} mined-out piles rebuilt, " +
						$"{crypt.Spots.Count - missing.Count} untouched");
				}
			}
			Flush();
			if (now && !dryRun)
			{
				RestockRegenPlugin.MudPileRegenNow.Value = false; // saved to the config file
				RestockRegenPlugin.Log.LogInfo("mud piles: regenerate-now done, MudPileRegenNow set back to false");
			}
			return result;
		}

		// A player up in the crypt, or holding any of its objects.
		private static bool Occupied(Crypt crypt, Dictionary<(int, int, int), ZDO> standing, long server)
		{
			if (ZNet.instance.GetPeers().Any(p => CryptAt(p.GetRefPos()) == crypt))
			{
				return true;
			}
			ZDO generator = ZDOMan.instance.GetZDO(crypt.Generator);
			if (generator != null && generator.HasOwner() && generator.GetOwner() != server)
			{
				return true;
			}
			foreach (var key in crypt.Spots.Keys)
			{
				if (standing.TryGetValue(key, out ZDO zdo) && zdo.HasOwner() && zdo.GetOwner() != server)
				{
					return true;
				}
			}
			return false;
		}

		// The same steps ZNetView.Awake takes for an object that has no ZDO yet.
		private static void Create(Spot spot)
		{
			GameObject prefab = ZNetScene.instance.GetPrefab(spot.Prefab);
			ZNetView view = prefab != null ? prefab.GetComponent<ZNetView>() : null;
			if (view == null)
			{
				return;
			}
			ZDO zdo = ZDOMan.instance.CreateNewZDO(spot.Pos, spot.Prefab);
			zdo.Persistent = view.m_persistent;
			zdo.Type = view.m_type;
			zdo.Distant = view.m_distant;
			zdo.SetPrefab(spot.Prefab);
			zdo.SetRotation(Quaternion.Euler(spot.Rot));
		}

		// Remember a pile the moment a player's update brings it in, so a pile generated and mined
		// out between two sweeps is not lost.
		[HarmonyPatch(typeof(ZDO), nameof(ZDO.Deserialize))]
		private static class SeePiles
		{
			private static void Postfix(ZDO __instance)
			{
				if (s_ready && s_intactOf.ContainsKey(__instance.GetPrefab()) && RestockRegenPlugin.MudPiles.Value)
				{
					Remember(SpotOf(__instance));
				}
			}
		}
	}
}
