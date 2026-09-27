using System.Collections.Generic;
using UnityEngine;

namespace RestockRegen
{
	/*
		The daily sweep. Once per in-game day, for every loot chest in the world:

		- holding items: clear its empty-since stamp, if it has one;
		- empty, no stamp: stamp it with today;
		- empty, stamped Days or more days ago: roll its loot table into it and clear the stamp.

		The stamp lives on the chest's own ZDO as day + 1, so 0 always means "no clock" and
		clearing is a plain Set that bumps the data revision. ZDO.RemoveInt would not bump it, so a
		client holding an older copy could hand the stale stamp back later.

		A chest that a player has loaded is skipped entirely. The server gives ownership of an
		object to the first player whose active area covers it and takes it back when they leave
		(ZDOMan.ReleaseNearbyZDOS), so an owner other than the server means a client holds a live
		copy that could overwrite our write. Skipped chests keep their state and are retried at
		the next sweep, so a chest that is due does not lose its turn, only waits for the player
		to leave.
	*/
	internal static class Restocker
	{
		internal static readonly int s_emptySince = "restockregen_emptysince".GetStableHashCode();

		// Dry run writes nothing to the world, so it keeps its stamps here instead. Without them
		// every empty chest would look newly empty every day and never come due. Lost on restart.
		private static readonly Dictionary<ZDOID, int> s_dryStamps = new Dictionary<ZDOID, int>();

		private static int GetStamp(ZDO zdo, bool dryRun)
		{
			if (dryRun)
			{
				return s_dryStamps.TryGetValue(zdo.m_uid, out int stamp) ? stamp : zdo.GetInt(s_emptySince);
			}
			return zdo.GetInt(s_emptySince);
		}

		private static void SetStamp(ZDO zdo, int value, bool dryRun)
		{
			if (dryRun)
			{
				s_dryStamps[zdo.m_uid] = value;
			}
			else
			{
				zdo.Set(s_emptySince, value);
			}
		}

		internal struct Result
		{
			public int Chests, Stamped, Cleared, Restocked, Waiting, Loaded, RolledNothing;
		}

		internal static Result Sweep(int today, bool dryRun, bool verbose)
		{
			var result = new Result();
			long server = ZDOMan.GetSessionID();
			int days = Mathf.Max(1, RestockRegenPlugin.Days.Value);

			foreach (ZDO zdo in ZDOMan.instance.m_objectsByID.Values)
			{
				if (!LootChests.Prefabs.TryGetValue(zdo.GetPrefab(), out Container prefab))
				{
					continue;
				}
				result.Chests++;
				if (zdo.GetLong(ZDOVars.s_creator) != 0L)
				{
					continue; // built by a player; never ours
				}
				int? count = LootChests.ItemCount(zdo);
				if (count == null)
				{
					continue; // never loaded, loot not rolled yet: the game fills it on first load
				}
				if (zdo.HasOwner() && zdo.GetOwner() != server)
				{
					result.Loaded++;
					continue;
				}

				int stamp = GetStamp(zdo, dryRun);
				if (count > 0)
				{
					if (stamp != 0)
					{
						result.Cleared++;
						SetStamp(zdo, 0, dryRun);
					}
					continue;
				}
				if (stamp == 0)
				{
					result.Stamped++;
					SetStamp(zdo, today + 1, dryRun);
					continue;
				}
				int emptyFor = today - (stamp - 1);
				if (emptyFor < days)
				{
					result.Waiting++;
					continue;
				}
				if (dryRun)
				{
					result.Restocked++;
					SetStamp(zdo, 0, dryRun);
					if (verbose) RestockRegenPlugin.Log.LogInfo($"dry run: would restock {prefab.name} at {Where(zdo)}, empty {emptyFor} days");
					continue;
				}
				if (Restock(zdo, prefab, out string rolled))
				{
					zdo.Set(s_emptySince, 0);
					result.Restocked++;
					if (verbose) RestockRegenPlugin.Log.LogInfo($"restocked {prefab.name} at {Where(zdo)} after {emptyFor} days: {rolled}");
				}
				else
				{
					result.RolledNothing++;
				}
			}
			return result;
		}

		// Rolls the prefab's loot table into a fresh inventory of the prefab's size and writes it to
		// the chest, the same way Container.AddDefaultItems and Container.Save do on a client.
		private static bool Restock(ZDO zdo, Container prefab, out string rolled)
		{
			rolled = "";
			var inventory = new Inventory(prefab.m_name, null, prefab.m_width, prefab.m_height);
			var names = new List<string>();
			foreach (ItemDrop.ItemData item in prefab.m_defaultItems.GetDropListItems())
			{
				names.Add($"{item.m_dropPrefab?.name}x{item.m_stack}");
				inventory.AddItem(item);
			}
			if (inventory.NrOfItems() == 0)
			{
				// Some tables have a drop chance below 1 and can roll nothing. Leave the stamp so
				// the chest tries again at the next sweep rather than waiting another Days.
				return false;
			}
			var pkg = new ZPackage();
			inventory.Save(pkg);
			zdo.Set(ZDOVars.s_items, pkg.GetArray());
			rolled = string.Join(" ", names);
			return true;
		}

		private static string Where(ZDO zdo)
		{
			Vector3 p = zdo.GetPosition();
			return $"({p.x:0}, {p.z:0})";
		}
	}
}
