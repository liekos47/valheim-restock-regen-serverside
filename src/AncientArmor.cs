using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace RestockRegen
{
	/*
		Ancient armor - the giant's helmets and swords lying in the Mistlands - comes back where
		nobody has been for AncientArmorDays in-game days.

		Mining works like the sunken crypts' mud piles: the first hit replaces "giant_helmet1" (or
		helmet2, sword1, sword2) with "<name>_destruction" at the same spot, a MineRock5 with health
		per chunk, and breaking the last chunk deletes that too. Unlike mud piles, players do finish
		these: 7 of 16 pieces mined on the owner's world between 2026-09-25 and 09-29 left nothing.
		So every piece's spot - prefab, position, rotation - is remembered from the first time it is
		seen, whole or broken.

		There is no object to keep that list on (crypts have their generator), so it lives in a text
		file beside the world save, worlds_local/<world>.restockregen.txt, which the game ignores and
		a copy of worlds_local includes. It also holds when each 64 m zone with a spot in it was last
		visited: a player within AncientArmorVisitRadius counts, checked every VisitCheckSeconds.

		A spot is refilled when its zone has gone AncientArmorDays without a visit, no player is near,
		no player holds its broken piece, and nothing a player built (a ZDO with a creator) stands
		within AncientArmorBuildClearance of it - a piece is never put back into someone's house. A
		spot blocked by a build is simply checked again each day.

		A player walking into the Mistlands is told that ancient armor comes back after the full
		count without visitors (WorldGenerator.GetBiome on their position), at most once per
		AncientArmorNotifyCooldown minutes so walking along the border does not repeat it.
	*/
	internal static class AncientArmor
	{
		private static readonly string[] Names = { "giant_helmet1", "giant_helmet2", "giant_sword1", "giant_sword2" };

		private struct Spot
		{
			public int Prefab;
			public Vector3 Pos;
			public Vector3 Rot;
		}

		private static readonly Dictionary<int, int> s_intactOf = new Dictionary<int, int>();
		private static readonly HashSet<int> s_intact = new HashSet<int>();
		private static readonly Dictionary<(int, int, int), Spot> s_spots = new Dictionary<(int, int, int), Spot>();
		private static readonly Dictionary<Vector2i, int> s_zoneVisit = new Dictionary<Vector2i, int>();
		private static readonly Dictionary<long, bool> s_inMist = new Dictionary<long, bool>();
		private static readonly Dictionary<long, float> s_lastTold = new Dictionary<long, float>();
		private static readonly List<ZDO> s_nearby = new List<ZDO>();
		private static bool s_ready, s_dirty;
		private static string s_file;

		private static (int, int, int) Key(Vector3 p) =>
			(Mathf.RoundToInt(p.x * 10f), Mathf.RoundToInt(p.y * 10f), Mathf.RoundToInt(p.z * 10f));

		// The key of the remembered spot at this position, allowing for rounding: a position read
		// back from the record file can land in the neighbouring 10 cm cell of the key the live
		// object gives. Without this a piece would be counted twice and, once due, doubled.
		private static (int, int, int)? SpotKeyAt(Vector3 p)
		{
			var k = Key(p);
			if (s_spots.ContainsKey(k))
			{
				return k;
			}
			for (int dx = -2; dx <= 2; dx++)
			{
				for (int dy = -2; dy <= 2; dy++)
				{
					for (int dz = -2; dz <= 2; dz++)
					{
						var n = (k.Item1 + dx, k.Item2 + dy, k.Item3 + dz);
						if (s_spots.TryGetValue(n, out Spot s) && (s.Pos - p).sqrMagnitude < 0.04f)
						{
							return n;
						}
					}
				}
			}
			return null;
		}

		private static Vector2i Zone(Vector3 p)
		{
			Vector2s z = ZoneSystem.GetZone(p);
			return new Vector2i(z.x, z.y);
		}

		private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

		internal static void Init(int today)
		{
			s_intactOf.Clear();
			s_intact.Clear();
			foreach (string name in Names)
			{
				if (ZNetScene.instance.GetPrefab(name) == null)
				{
					continue;
				}
				int intact = name.GetStableHashCode();
				s_intact.Add(intact);
				s_intactOf[intact] = intact;
				if (ZNetScene.instance.GetPrefab(name + "_destruction") != null)
				{
					s_intactOf[(name + "_destruction").GetStableHashCode()] = intact;
				}
			}

			s_file = Path.Combine(SaveSystem.GetWorldsSaveRootPath(ZNet.m_world.m_fileSource), ZNet.instance.GetWorldName() + ".restockregen.txt");
			Load();
			int before = s_spots.Count, intactNow = 0, brokenNow = 0;
			foreach (ZDO zdo in ZDOMan.instance.m_objectsByID.Values)
			{
				if (s_intactOf.TryGetValue(zdo.GetPrefab(), out int intact))
				{
					Remember(zdo);
					if (s_intact.Contains(zdo.GetPrefab())) intactNow++; else brokenNow++;
				}
			}
			// A zone never seen visited starts its clock now, not at day 0.
			foreach (Spot spot in s_spots.Values)
			{
				Vector2i zone = Zone(spot.Pos);
				if (!s_zoneVisit.ContainsKey(zone))
				{
					s_zoneVisit[zone] = today;
					s_dirty = true;
				}
			}
			s_ready = true;
			Save();
			RestockRegenPlugin.Log.LogInfo(
				$"ancient armor: {s_spots.Count} spots remembered ({s_spots.Count - before} new this start) in {s_zoneVisit.Count} zones, " +
				$"{intactNow} whole and {brokenNow} partly mined in the world now, {s_spots.Count - intactNow - brokenNow} mined out; records in {s_file}");
		}

		private static void Remember(ZDO zdo)
		{
			if (SpotKeyAt(zdo.GetPosition()) != null)
			{
				return;
			}
			s_spots[Key(zdo.GetPosition())] = new Spot { Prefab = s_intactOf[zdo.GetPrefab()], Pos = zdo.GetPosition(), Rot = zdo.GetRotation().eulerAngles };
			Vector2i zone = Zone(zdo.GetPosition());
			if (!s_zoneVisit.ContainsKey(zone) && EnvMan.instance != null)
			{
				s_zoneVisit[zone] = EnvMan.instance.GetDay();
			}
			s_dirty = true;
		}

		// Every VisitCheckSeconds: zones near players are visited today; players entering the
		// Mistlands are told; the record file is written if anything changed.
		internal static void Tick(int today)
		{
			if (!s_ready || !RestockRegenPlugin.AncientArmor.Value)
			{
				return;
			}
			float radius = RestockRegenPlugin.AncientArmorVisitRadius.Value;
			int reach = Mathf.CeilToInt(radius / ZoneSystem.c_ZoneSize);
			var present = new HashSet<long>();
			foreach (ZNetPeer peer in ZNet.instance.GetPeers())
			{
				present.Add(peer.m_uid);
				Vector3 p = peer.GetRefPos();
				Vector2i center = Zone(p);
				for (int dx = -reach; dx <= reach; dx++)
				{
					for (int dy = -reach; dy <= reach; dy++)
					{
						var zone = new Vector2i(center.x + dx, center.y + dy);
						if (s_zoneVisit.TryGetValue(zone, out int last) && last != today && ZoneNear(zone, p, radius))
						{
							s_zoneVisit[zone] = today;
							s_dirty = true;
						}
					}
				}
				bool inMist = p.y < 4000f && WorldGenerator.instance != null && WorldGenerator.instance.GetBiome(p) == Heightmap.Biome.Mistlands;
				bool was = s_inMist.TryGetValue(peer.m_uid, out bool w) && w;
				s_inMist[peer.m_uid] = inMist;
				if (inMist && !was)
				{
					Welcome(peer);
				}
			}
			foreach (long gone in s_inMist.Keys.Where(uid => !present.Contains(uid)).ToList())
			{
				s_inMist.Remove(gone);
			}
			Save();
		}

		// Some point of the zone's 64 m square lies within radius of p.
		private static bool ZoneNear(Vector2i zone, Vector3 p, float radius)
		{
			Vector3 c = ZoneSystem.GetZonePos(new Vector2s((short)zone.x, (short)zone.y));
			float half = ZoneSystem.c_ZoneSize / 2f;
			float dx = Mathf.Max(0f, Mathf.Abs(p.x - c.x) - half);
			float dz = Mathf.Max(0f, Mathf.Abs(p.z - c.z) - half);
			return dx * dx + dz * dz <= radius * radius;
		}

		private static void Welcome(ZNetPeer peer)
		{
			if (!RestockRegenPlugin.Notify.Value)
			{
				return;
			}
			float cooldown = RestockRegenPlugin.AncientArmorNotifyCooldown.Value * 60f;
			if (s_lastTold.TryGetValue(peer.m_uid, out float told) && Time.time - told < cooldown)
			{
				return;
			}
			s_lastTold[peer.m_uid] = Time.time;
			int days = Mathf.Max(1, RestockRegenPlugin.AncientArmorDays.Value);
			string text = RestockRegenPlugin.AncientArmorNotifyText.Value.Replace("{days}", days == 1 ? "1 day" : $"{days} days");
			ZRoutedRpc.instance.InvokeRoutedRPC(peer.m_uid, "ShowMessage", (int)MessageHud.MessageType.Center, text);
			if (RestockRegenPlugin.Verbose.Value)
			{
				RestockRegenPlugin.Log.LogInfo($"told {peer.m_playerName}: {text}");
			}
		}

		internal struct Result
		{
			public int Missing, Waiting, Near, Built, Refilled, Replaced;
		}

		// Once per in-game day. With now, the clock is ignored (admin "restock-regen").
		internal static Result Sweep(int today, bool now = false)
		{
			var result = new Result();
			if (!s_ready || !RestockRegenPlugin.AncientArmor.Value)
			{
				return result;
			}
			bool dryRun = RestockRegenPlugin.AncientArmorDryRun.Value;
			bool verbose = RestockRegenPlugin.Verbose.Value;
			int days = Mathf.Max(1, RestockRegenPlugin.AncientArmorDays.Value);
			float radius = RestockRegenPlugin.AncientArmorVisitRadius.Value;
			float clearance = RestockRegenPlugin.AncientArmorBuildClearance.Value;

			var standing = new Dictionary<(int, int, int), ZDO>();
			foreach (ZDO zdo in ZDOMan.instance.m_objectsByID.Values)
			{
				if (s_intactOf.ContainsKey(zdo.GetPrefab()))
				{
					Remember(zdo);
					var key = SpotKeyAt(zdo.GetPosition()) ?? Key(zdo.GetPosition());
					if (!standing.TryGetValue(key, out ZDO other) || !s_intact.Contains(other.GetPrefab()))
					{
						standing[key] = zdo;
					}
				}
			}
			List<Vector3> players = ZNet.instance.GetPeers().Select(p => p.GetRefPos()).ToList();

			foreach (var kv in s_spots)
			{
				standing.TryGetValue(kv.Key, out ZDO here);
				if (here != null && s_intact.Contains(here.GetPrefab()))
				{
					continue; // whole
				}
				result.Missing++;
				Spot spot = kv.Value;
				Vector2i zone = Zone(spot.Pos);
				int last = s_zoneVisit.TryGetValue(zone, out int v) ? v : today;
				if (!now && today - last < days)
				{
					result.Waiting++;
					continue;
				}
				if (players.Any(p => Flat(p, spot.Pos) < radius) || WorldObjects.HeldByPlayer(here))
				{
					result.Near++;
					continue;
				}
				if (BuiltNear(spot.Pos, clearance))
				{
					result.Built++;
					continue;
				}
				if (here != null)
				{
					result.Replaced++;
				}
				else
				{
					result.Refilled++;
				}
				if (!dryRun)
				{
					if (here != null)
					{
						WorldObjects.Remove(here);
					}
					WorldObjects.Create(spot.Prefab, spot.Pos, spot.Rot);
				}
				if (verbose)
				{
					RestockRegenPlugin.Log.LogInfo(
						$"{(dryRun ? "dry run: would restore" : "restored")} {ZNetScene.instance.GetPrefab(spot.Prefab)?.name} at ({spot.Pos.x:0}, {spot.Pos.z:0})" +
						(here != null ? " (replacing its broken remains)" : " (mined out)") +
						(now ? " (regenerate now)" : $" after {today - last} days without visitors"));
				}
			}
			Save();
			return result;
		}

		// Anything a player built (a ZDO with a creator) within clearance metres, flat, of the spot.
		private static bool BuiltNear(Vector3 pos, float clearance)
		{
			if (clearance <= 0f)
			{
				return false;
			}
			s_nearby.Clear();
			ZDOMan.instance.FindSectorObjects(ZoneSystem.GetZone(pos), new SimulationDistance(1, 0, true), s_nearby);
			foreach (ZDO zdo in s_nearby)
			{
				if (Flat(zdo.GetPosition(), pos) < clearance && Mathf.Abs(zdo.GetPosition().y - pos.y) < 30f
					&& zdo.GetLong(ZDOVars.s_creator) != 0L)
				{
					return true;
				}
			}
			return false;
		}

		internal static string Summary()
		{
			return s_ready ? $"{s_spots.Count} ancient armor spots known" : "ancient armor not started";
		}

		// The record file: "S <prefab> x y z rx ry rz" per spot, "V <zone x> <zone y> <day>" per zone.
		private static void Load()
		{
			s_spots.Clear();
			s_zoneVisit.Clear();
			if (!File.Exists(s_file))
			{
				return;
			}
			CultureInfo inv = CultureInfo.InvariantCulture;
			foreach (string line in File.ReadAllLines(s_file))
			{
				string[] f = line.Split(' ');
				try
				{
					if (f[0] == "S" && f.Length == 8)
					{
						var spot = new Spot
						{
							Prefab = int.Parse(f[1], inv),
							Pos = new Vector3(float.Parse(f[2], inv), float.Parse(f[3], inv), float.Parse(f[4], inv)),
							Rot = new Vector3(float.Parse(f[5], inv), float.Parse(f[6], inv), float.Parse(f[7], inv)),
						};
						if (SpotKeyAt(spot.Pos) == null) // drops duplicates an older build wrote
						{
							s_spots[Key(spot.Pos)] = spot;
						}
						else
						{
							s_dirty = true;
						}
					}
					else if (f[0] == "V" && f.Length == 4)
					{
						s_zoneVisit[new Vector2i(int.Parse(f[1], inv), int.Parse(f[2], inv))] = int.Parse(f[3], inv);
					}
				}
				catch (Exception)
				{
					RestockRegenPlugin.Log.LogWarning($"ancient armor: skipped a bad line in {s_file}: {line}");
				}
			}
		}

		internal static void Save()
		{
			if (!s_dirty || s_file == null)
			{
				return;
			}
			CultureInfo inv = CultureInfo.InvariantCulture;
			var lines = new List<string> { "# RestockRegen ancient armor records. S = spot (prefab x y z rx ry rz), V = zone last visited (zone x, zone y, day)." };
			lines.AddRange(s_spots.Values.Select(s => string.Format(inv, "S {0} {1:R} {2:R} {3:R} {4:R} {5:R} {6:R}", s.Prefab, s.Pos.x, s.Pos.y, s.Pos.z, s.Rot.x, s.Rot.y, s.Rot.z)));
			lines.AddRange(s_zoneVisit.Select(v => string.Format(inv, "V {0} {1} {2}", v.Key.x, v.Key.y, v.Value)));
			string tmp = s_file + ".tmp";
			File.WriteAllLines(tmp, lines);
			if (File.Exists(s_file))
			{
				File.Delete(s_file);
			}
			File.Move(tmp, s_file);
			s_dirty = false;
		}

		// Remember a piece the moment a player's update brings it in, so one generated and mined out
		// between two sweeps is not lost.
		[HarmonyPatch(typeof(ZDO), nameof(ZDO.Deserialize))]
		private static class SeePieces
		{
			private static void Postfix(ZDO __instance)
			{
				if (s_ready && s_intactOf.ContainsKey(__instance.GetPrefab()) && RestockRegenPlugin.AncientArmor.Value)
				{
					Remember(__instance);
				}
			}
		}
	}
}
