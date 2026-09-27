using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace RestockRegen
{
	/*
		What counts as a loot chest, and whether one is empty.

		A loot chest is a networked prefab whose Container has a non-empty drop table
		(m_defaultItems). The decision is made on the prefab, not the object: a scan of the
		live save found cargo crates, tombstones and player chests with no creator recorded,
		all of which look like world chests if you only ask "who built it".

		A chest's loot is rolled in Container.Awake the first time a client loads it, not when it
		is opened, so every chest in the save already carries s_addedDefaultItems and an items
		array. "Untouched" is not recorded anywhere; "empty" is the only state we can read.
	*/
	internal static class LootChests
	{
		// Prefab hash -> the Container on that prefab, for every prefab with a loot table.
		internal static readonly Dictionary<int, Container> Prefabs = new Dictionary<int, Container>();

		internal static void FindPrefabs()
		{
			Prefabs.Clear();
			foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
			{
				if (prefab == null)
				{
					continue;
				}
				Container container = prefab.GetComponent<Container>();
				if (container == null || container.m_defaultItems == null || container.m_defaultItems.m_drops.Count == 0)
				{
					continue;
				}
				Prefabs[prefab.name.GetStableHashCode()] = container;
				if (RestockRegenPlugin.Verbose.Value)
				{
					RestockRegenPlugin.Log.LogInfo(
						$"loot prefab {prefab.name}: {container.m_width}x{container.m_height}, " +
						$"{container.m_defaultItems.m_drops.Count} drops, rolls {container.m_defaultItems.m_dropMin}-{container.m_defaultItems.m_dropMax}" +
						(container.m_autoDestroyEmpty ? ", autoDestroyEmpty" : "") +
						(prefab.GetComponent<Piece>() != null ? ", has Piece" : ""));
				}
			}
		}

		// Item count straight from the saved inventory header, without resolving any item prefab:
		// an int version, then a ushort count (version 108 and later) or an int count (older).
		// Null means the chest has never been loaded, so it has no items array at all.
		internal static int? ItemCount(ZDO zdo)
		{
			byte[] items = zdo.GetByteArray(ZDOVars.s_items);
			if (items == null || items.Length < 4)
			{
				return null;
			}
			using (var reader = new BinaryReader(new MemoryStream(items)))
			{
				int version = reader.ReadInt32();
				if (version >= 108)
				{
					return items.Length >= 6 ? reader.ReadUInt16() : (int?)null;
				}
				return items.Length >= 8 ? reader.ReadInt32() : (int?)null;
			}
		}

		// Read-only: count loot chests in the world by prefab and state, and log the result.
		internal static void Census()
		{
			var watch = System.Diagnostics.Stopwatch.StartNew();
			FindPrefabs();

			var perPrefab = new Dictionary<int, int[]>(); // total, empty, has items, never loaded, has creator
			foreach (ZDO zdo in ZDOMan.instance.m_objectsByID.Values)
			{
				int prefab = zdo.GetPrefab();
				if (!Prefabs.ContainsKey(prefab))
				{
					continue;
				}
				if (!perPrefab.TryGetValue(prefab, out int[] counts))
				{
					perPrefab[prefab] = counts = new int[5];
				}
				counts[0]++;
				int? n = ItemCount(zdo);
				if (n == null) counts[3]++;
				else if (n == 0) counts[1]++;
				else counts[2]++;
				if (zdo.GetLong(ZDOVars.s_creator) != 0L) counts[4]++;
			}

			int total = perPrefab.Values.Sum(c => c[0]);
			int empty = perPrefab.Values.Sum(c => c[1]);
			RestockRegenPlugin.Log.LogInfo(
				$"census: {Prefabs.Count} loot chest prefabs, {total} chests in the world, {empty} empty, " +
				$"day {EnvMan.instance.GetDay()}, {watch.ElapsedMilliseconds} ms over {ZDOMan.instance.m_objectsByID.Count} objects");
			foreach (var kv in perPrefab.OrderByDescending(kv => kv.Value[0]))
			{
				int[] c = kv.Value;
				RestockRegenPlugin.Log.LogInfo(
					$"census: {Prefabs[kv.Key].gameObject.name,-32} total {c[0],4}  empty {c[1],4}  items {c[2],4}  never loaded {c[3],3}  player-built {c[4],3}");
			}
		}
	}
}
