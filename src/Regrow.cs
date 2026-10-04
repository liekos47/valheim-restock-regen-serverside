using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace RestockRegen
{
	/*
		World resources that come back where nobody has been for a number of in-game days. Each kind
		is a Group with its own settings:

		- Ancient armor, Mistlands: giant_helmet1/2 and giant_sword1/2. The first hit replaces the
		  piece with "<name>_destruction" (a MineRock5 with health per chunk); breaking the last
		  chunk deletes that too.
		- Obsidian, Mountains: MineRock_Obsidian. Despite the name it is a plain Destructible with
		  no m_spawnWhenDestroyed (prefab data): one object, deleted when broken, nothing left.
		- Flametal, Ashlands: LeviathanLava, the spire that rises out of the lava. It is the ocean
		  Leviathan's code: every hit has a chance to make it leave, which sets ZDOVars.s_dead, plays
		  the dive and then destroys it. For this prefab that chance (m_hitReactionChance) is 0.01,
		  not the code's default 0.25, and its MineRock has m_removeWhenDestroyed off, so the usual
		  result of mining one is a husk that stays, each mined chunk a "Health<n>" float on it,
		  and only now and then one that sinks. It floats on the lava, so its position can drift a
		  little, and its spot is matched flat, within DriftRadius, instead of to 20 cm.

		- Ice, Deep North (prefab data): IcePond_rock, the ice pond, and IceShore_1, the ice along
		  the shore. Both are a Destructible with 1 health whose first hit spawns "<name>_frac", a
		  MineRock5 giving Ice per chunk (1-3 for the pond, 2-3 for the shore, which needs tool tier
		  2); the last chunk deletes it. Neither has anything that respawns it. The owner's save of
		  2026-09-30 had 4 whole and 1 mined pond, 230 whole and 11 mined shore pieces. The floating
		  ice (ice1) is not tracked: it drifts, and its drop table gives its Ice a weight of 0.

		Dragon eggs were a group until 0.12.0 and were removed: the game respawns them itself.
		A taken Pickable_DragonEgg stays in the world marked picked and is unpicked again by the
		game's own timer (the owner's save holds eggs with s_picked back at false), so they were
		never "gone" for this module to restore.

		So every object's spot - prefab, position, rotation - is remembered from the first time it
		is seen, whole or damaged. There is no object to keep that list on, so it lives in a text
		file beside the world save, worlds_local/<world>.restockregen.txt, which the game ignores and
		a copy of worlds_local includes. It also holds when each 64 m zone with a spot in it was last
		visited: a player within a group's visit radius counts, checked every VisitCheckSeconds.

		A spot is restored - a damaged object replaced, a missing one created - when its zone has
		gone the group's days without a visit, no player is within its radius, no player holds the
		damaged object, and nothing a player built (a ZDO with a creator) stands within its build
		clearance: nothing is ever put back into someone's house. A blocked spot is simply checked
		again each day.

		Players entering the Mistlands, the Mountains, the Ashlands or the Deep North are told (WorldGenerator.GetBiome on their
		position), at most once per cooldown so walking along a border does not repeat it.
	*/
	internal static class Regrow
	{
		internal class Group
		{
			public string Id, Label;
			public string[] Names;
			public bool Drifts; // floats: match its spot flat and loosely
			public ConfigEntry<bool> On, DryRun;
			public ConfigEntry<int> Days;
			public ConfigEntry<float> Radius, Clearance;
			public bool Enabled => On.Value;
			public int DayCount => Mathf.Max(1, Days.Value);
		}

		private class BiomeNotice
		{
			public Heightmap.Biome Biome;
			public ConfigEntry<string> Text;
			public ConfigEntry<float> Cooldown;
			public Group[] Groups;
			public readonly Dictionary<long, bool> Inside = new Dictionary<long, bool>();
			public readonly Dictionary<long, float> LastTold = new Dictionary<long, float>();
		}

		private struct Spot
		{
			public int Prefab; // the whole object's prefab
			public Vector3 Pos;
			public Vector3 Rot;
		}

		internal struct Result
		{
			public int Missing, Waiting, Near, Built, Restored, Replaced;
		}

		internal static readonly List<Group> Groups = new List<Group>();
		private static readonly List<BiomeNotice> s_notices = new List<BiomeNotice>();
		private static readonly Dictionary<int, int> s_intactOf = new Dictionary<int, int>(); // any tracked prefab -> whole prefab
		private static readonly HashSet<int> s_intact = new HashSet<int>();
		private static readonly Dictionary<int, Group> s_groupOf = new Dictionary<int, Group>(); // whole prefab -> group
		private static readonly Dictionary<(int, int, int), Spot> s_spots = new Dictionary<(int, int, int), Spot>();
		private static readonly Dictionary<Vector2i, int> s_zoneVisit = new Dictionary<Vector2i, int>();
		private static readonly List<ZDO> s_nearby = new List<ZDO>();
		private static readonly int s_health = "health".GetStableHashCode();
		// MineRock keeps one "Health<n>" float per chunk, written only once the chunk has been hit.
		private static readonly int[] s_chunkHealth = Enumerable.Range(0, 64).Select(i => ("Health" + i).GetStableHashCode()).ToArray();
		private const float DriftRadius = 6f;
		private static bool s_ready, s_dirty;
		private static string s_file;

		internal static void Configure(ConfigFile config)
		{
			Groups.Clear();
			s_notices.Clear();
			Group armor = Bind(config, "AncientArmor", "armor", "ancient armor",
				new[] { "giant_helmet1", "giant_helmet2", "giant_sword1", "giant_sword2" },
				"the giant's helmets and swords in the Mistlands");
			Group obsidian = Bind(config, "Obsidian", "obsidian", "obsidian",
				new[] { "MineRock_Obsidian" }, "obsidian deposits in the Mountains");
			Group flametal = Bind(config, "Flametal", "flametal", "flametal",
				new[] { "LeviathanLava" }, "flametal spires in the Ashlands' lava");
			flametal.Drifts = true;
			Group ice = Bind(config, "Ice", "ice", "ice",
				new[] { "IcePond_rock", "IceShore_1" }, "ice ponds and the ice along the shore in the Deep North");
			Groups.AddRange(new[] { armor, obsidian, flametal, ice });

			s_notices.Add(new BiomeNotice
			{
				Biome = Heightmap.Biome.Mistlands,
				Groups = new[] { armor },
				Text = config.Bind("AncientArmor", "AncientArmorNotifyText", "The spirits will restore ancient armor after {days} without visitors",
					"Shown to a player entering the Mistlands (needs Notify on). {days} is AncientArmorDays."),
				Cooldown = config.Bind("AncientArmor", "AncientArmorNotifyCooldown", 10f,
					"Minutes. A player is told at most once in this long, so walking along the Mistlands border does not repeat it."),
			});
			s_notices.Add(new BiomeNotice
			{
				Biome = Heightmap.Biome.Mountain,
				Groups = new[] { obsidian },
				Text = config.Bind("Mountains", "MountainsNotifyText", "The spirits will restore obsidian after {days} without visitors",
					"Shown to a player entering the Mountains while Obsidian is on (needs Notify on). {days} is ObsidianDays."),
				Cooldown = config.Bind("Mountains", "MountainsNotifyCooldown", 10f,
					"Minutes. A player is told at most once in this long."),
			});
			s_notices.Add(new BiomeNotice
			{
				Biome = Heightmap.Biome.AshLands,
				Groups = new[] { flametal },
				Text = config.Bind("Ashlands", "AshlandsNotifyText", "The spirits will restore flametal after {days} without visitors",
					"Shown to a player entering the Ashlands while Flametal is on (needs Notify on). {days} is FlametalDays."),
				Cooldown = config.Bind("Ashlands", "AshlandsNotifyCooldown", 10f,
					"Minutes. A player is told at most once in this long."),
			});
			s_notices.Add(new BiomeNotice
			{
				Biome = Heightmap.Biome.DeepNorth,
				Groups = new[] { ice },
				Text = config.Bind("DeepNorth", "DeepNorthNotifyText", "The spirits will restore ice after {days} without visitors",
					"Shown to a player entering the Deep North while Ice is on (needs Notify on). {days} is IceDays."),
				Cooldown = config.Bind("DeepNorth", "DeepNorthNotifyCooldown", 10f,
					"Minutes. A player is told at most once in this long."),
			});
		}

		private static Group Bind(ConfigFile config, string section, string id, string label, string[] names, string what)
		{
			return new Group
			{
				Id = id,
				Label = label,
				Names = names,
				On = config.Bind(section, section, true, $"Restore {what} where no player has been for {section}Days in-game days. Only objects the mod has seen, whole or damaged, can come back."),
				Days = config.Bind(section, section + "Days", 30, $"In-game days with no player within {section}VisitRadius before a {label} spot is restored. Any visit starts the count again."),
				DryRun = config.Bind(section, section + "DryRun", false, "Log what would be restored, but create and remove nothing. Spots and visits are still recorded."),
				Radius = config.Bind(section, section + "VisitRadius", 100f, "Metres. A player this close to a spot counts as a visit, and nothing is restored with a player this close."),
				Clearance = config.Bind(section, section + "BuildClearance", 8f, "Metres, measured flat. Nothing is restored if anything a player built stands this close to the spot. 0 turns the check off."),
			};
		}

		private static (int, int, int) Key(Vector3 p) =>
			(Mathf.RoundToInt(p.x * 10f), Mathf.RoundToInt(p.y * 10f), Mathf.RoundToInt(p.z * 10f));

		// The key of the remembered spot at this position, allowing for rounding: a position read
		// back from the record file can land in the neighbouring 10 cm cell of the key the live
		// object gives. Without this an object would be counted twice and, once due, doubled.
		private static (int, int, int)? SpotKeyAt(Vector3 p, int wholePrefab)
		{
			if (s_groupOf.TryGetValue(wholePrefab, out Group group) && group.Drifts)
			{
				// A floating object: the same kind within DriftRadius, measured flat. There are few of
				// them, so a plain search is fine.
				foreach (var kv in s_spots)
				{
					if (kv.Value.Prefab == wholePrefab && Flat(kv.Value.Pos, p) < DriftRadius)
					{
						return kv.Key;
					}
				}
				return null;
			}
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

		// Whole: the whole prefab with no damage recorded - no MineRock5 health string (obsidian),
		// no MineRock chunk hit and not marked dead (a flametal spire that is leaving).
		private static bool IsWhole(ZDO zdo)
		{
			if (!s_intact.Contains(zdo.GetPrefab()) || !string.IsNullOrEmpty(zdo.GetString(s_health)) || zdo.GetBool(ZDOVars.s_dead))
			{
				return false;
			}
			if (s_groupOf.TryGetValue(zdo.GetPrefab(), out Group group) && group.Drifts)
			{
				foreach (int key in s_chunkHealth)
				{
					if (zdo.GetFloat(key, float.MaxValue) != float.MaxValue)
					{
						return false;
					}
				}
			}
			return true;
		}

		internal static void Init(int today)
		{
			s_intactOf.Clear();
			s_intact.Clear();
			s_groupOf.Clear();
			foreach (Group group in Groups)
			{
				foreach (string name in group.Names)
				{
					if (ZNetScene.instance.GetPrefab(name) == null)
					{
						RestockRegenPlugin.Log.LogWarning($"{group.Label}: prefab {name} not found, skipped");
						continue;
					}
					int hash = name.GetStableHashCode();
					s_intact.Add(hash);
					s_intactOf[hash] = hash;
					s_groupOf[hash] = group;
					foreach (string suffix in new[] { "_destruction", "_frac" })
					{
						if (ZNetScene.instance.GetPrefab(name + suffix) != null)
						{
							s_intactOf[(name + suffix).GetStableHashCode()] = hash;
						}
					}
				}
			}

			s_file = Path.Combine(SaveSystem.GetWorldsSaveRootPath(ZNet.m_world.m_fileSource), ZNet.instance.GetWorldName() + ".restockregen.txt");
			Load();
			// Spots of a kind this version no longer tracks (dragon eggs, before 0.12.0) are dropped.
			foreach (var stale in s_spots.Where(kv => !s_groupOf.ContainsKey(kv.Value.Prefab)).Select(kv => kv.Key).ToList())
			{
				s_spots.Remove(stale);
				s_dirty = true;
			}
			var before = Groups.ToDictionary(g => g, g => SpotsOf(g));
			var whole = Groups.ToDictionary(g => g, g => 0);
			var damaged = Groups.ToDictionary(g => g, g => 0);
			foreach (ZDO zdo in ZDOMan.instance.m_objectsByID.Values)
			{
				if (s_intactOf.TryGetValue(zdo.GetPrefab(), out int w))
				{
					Remember(zdo);
					if (IsWhole(zdo)) whole[s_groupOf[w]]++; else damaged[s_groupOf[w]]++;
				}
			}
			foreach (Spot spot in s_spots.Values)
			{
				Vector2i zone = Zone(spot.Pos);
				if (!s_zoneVisit.ContainsKey(zone))
				{
					s_zoneVisit[zone] = today; // a zone never seen visited starts its clock now
					s_dirty = true;
				}
			}
			s_ready = true;
			Save();
			foreach (Group g in Groups)
			{
				int spots = SpotsOf(g);
				RestockRegenPlugin.Log.LogInfo(
					$"{g.Label}: {spots} spots remembered ({spots - before[g]} new this start), {whole[g]} whole and {damaged[g]} damaged in the world now, " +
					$"{spots - whole[g] - damaged[g]} gone{(g.Enabled ? "" : " (off)")}{(g.DryRun.Value ? " (dry run)" : "")}");
			}
			RestockRegenPlugin.Log.LogInfo($"regrow records: {s_zoneVisit.Count} zones, in {s_file}");
		}

		private static int SpotsOf(Group g) => s_spots.Values.Count(s => s_groupOf.TryGetValue(s.Prefab, out Group x) && x == g);

		private static void Remember(ZDO zdo)
		{
			if (SpotKeyAt(zdo.GetPosition(), s_intactOf[zdo.GetPrefab()]) != null)
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

		// Every VisitCheckSeconds: zones near players are visited today; players entering a biome
		// are told; the record file is written if anything changed.
		internal static void Tick(int today)
		{
			if (!s_ready || !Groups.Any(g => g.Enabled))
			{
				return;
			}
			float radius = Groups.Where(g => g.Enabled).Max(g => g.Radius.Value);
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
				Heightmap.Biome biome = p.y < 4000f && WorldGenerator.instance != null ? WorldGenerator.instance.GetBiome(p) : Heightmap.Biome.None;
				foreach (BiomeNotice notice in s_notices)
				{
					bool inside = biome == notice.Biome;
					bool was = notice.Inside.TryGetValue(peer.m_uid, out bool w) && w;
					notice.Inside[peer.m_uid] = inside;
					if (inside && !was)
					{
						Welcome(peer, notice);
					}
				}
			}
			foreach (BiomeNotice notice in s_notices)
			{
				foreach (long gone in notice.Inside.Keys.Where(uid => !present.Contains(uid)).ToList())
				{
					notice.Inside.Remove(gone);
				}
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

		private static void Welcome(ZNetPeer peer, BiomeNotice notice)
		{
			Group first = notice.Groups.FirstOrDefault(g => g.Enabled);
			if (first == null || !RestockRegenPlugin.Notify.Value)
			{
				return;
			}
			if (notice.LastTold.TryGetValue(peer.m_uid, out float told) && Time.time - told < notice.Cooldown.Value * 60f)
			{
				return;
			}
			notice.LastTold[peer.m_uid] = Time.time;
			int days = first.DayCount;
			string text = notice.Text.Value.Replace("{days}", days == 1 ? "1 day" : $"{days} days");
			ZRoutedRpc.instance.InvokeRoutedRPC(peer.m_uid, "ShowMessage", (int)MessageHud.MessageType.Center, text);
			if (RestockRegenPlugin.Verbose.Value)
			{
				RestockRegenPlugin.Log.LogInfo($"told {peer.m_playerName}: {text}");
			}
		}

		// Once per in-game day. With only, just that group; with now, its clock is ignored (admin commands).
		internal static Dictionary<Group, Result> Sweep(int today, Group only = null, bool now = false)
		{
			var results = Groups.ToDictionary(g => g, g => new Result());
			if (!s_ready)
			{
				return results;
			}
			bool verbose = RestockRegenPlugin.Verbose.Value;

			var standing = new Dictionary<(int, int, int), ZDO>();
			foreach (ZDO zdo in ZDOMan.instance.m_objectsByID.Values)
			{
				if (s_intactOf.ContainsKey(zdo.GetPrefab()))
				{
					Remember(zdo);
					var key = SpotKeyAt(zdo.GetPosition(), s_intactOf[zdo.GetPrefab()]) ?? Key(zdo.GetPosition());
					if (!standing.TryGetValue(key, out ZDO other) || !IsWhole(other))
					{
						standing[key] = zdo;
					}
				}
			}
			List<Vector3> players = ZNet.instance.GetPeers().Select(p => p.GetRefPos()).ToList();

			foreach (var kv in s_spots)
			{
				if (!s_groupOf.TryGetValue(kv.Value.Prefab, out Group group) || !group.Enabled || (only != null && group != only))
				{
					continue;
				}
				standing.TryGetValue(kv.Key, out ZDO here);
				if (here != null && IsWhole(here))
				{
					continue;
				}
				Result r = results[group];
				r.Missing++;
				Spot spot = kv.Value;
				int last = s_zoneVisit.TryGetValue(Zone(spot.Pos), out int v) ? v : today;
				if (!now && today - last < group.DayCount)
				{
					r.Waiting++;
				}
				else if (players.Any(p => Flat(p, spot.Pos) < group.Radius.Value) || WorldObjects.HeldByPlayer(here))
				{
					r.Near++;
				}
				else if (BuiltNear(spot.Pos, group.Clearance.Value))
				{
					r.Built++;
				}
				else
				{
					if (here != null) r.Replaced++; else r.Restored++;
					bool dryRun = group.DryRun.Value;
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
							(here != null ? " (replacing a damaged one)" : " (was gone)") +
							(now ? " (regenerate now)" : $" after {today - last} days without visitors"));
					}
				}
				results[group] = r;
			}
			Save();
			return results;
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
			return s_ready ? string.Join(", ", Groups.Select(g => $"{SpotsOf(g)} {g.Label} spots{(g.Enabled ? "" : " (off)")}{(g.DryRun.Value ? " (DRY RUN)" : "")}")) : "regrow not started";
		}

		internal static Group Find(string id) => Groups.FirstOrDefault(g => g.Id == id);

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
						if (SpotKeyAt(spot.Pos, spot.Prefab) == null) // drops duplicates an older build wrote
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
					RestockRegenPlugin.Log.LogWarning($"regrow: skipped a bad line in {s_file}: {line}");
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
			var lines = new List<string> { "# RestockRegen regrow records. S = spot (prefab x y z rx ry rz), V = zone last visited (zone x, zone y, day)." };
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

		// Remember an object the moment a player's update brings it in, so one generated and taken
		// between two sweeps is not lost.
		[HarmonyPatch(typeof(ZDO), nameof(ZDO.Deserialize))]
		private static class SeeObjects
		{
			private static void Postfix(ZDO __instance)
			{
				if (s_ready && s_intactOf.ContainsKey(__instance.GetPrefab()))
				{
					Remember(__instance);
				}
			}
		}
	}
}
