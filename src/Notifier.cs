using System.Collections.Generic;
using HarmonyLib;

namespace RestockRegen
{
	/*
		Tells a player, on screen, when the loot chest they have open is empty and how long until it
		restocks, and who opened it before them (see History). Server-side, no client code.

		Opening a chest happens on a client, but two things reach the server. Container.RPC_RequestOpen
		hands ownership of the chest to the player opening it, and that player's Container then sets
		ZDOVars.s_inUse on the chest. Both arrive in the player's next ZDO update, which the server
		reads in ZDOMan.RPC_ZDOData -> ZDO.Deserialize. So after Deserialize, "in use and empty" means
		the chest's owner has it open and there is nothing in it: either they opened an already looted
		chest, or they just took the last item out. The owner is the player to tell.

		The same update marks a new opening, which History records under the owner's character name.

		Messages go through MessageHud's "ShowMessage" RPC, which every client registers.
	*/
	[HarmonyPatch(typeof(ZDO), nameof(ZDO.Deserialize))]
	internal static class Notifier
	{
		// Chests currently open, so one opening is handled once. Cleared when the chest closes.
		private static readonly HashSet<ZDOID> s_open = new HashSet<ZDOID>();

		// Chests whose current emptiness has already been announced to the player holding them.
		// Cleared when the chest closes or gets items again.
		private static readonly HashSet<ZDOID> s_announced = new HashSet<ZDOID>();

		// The day each chest was first seen empty by this hook, before the daily sweep has stamped it.
		// The sweep uses it as the stamp, so the countdown a player was shown is the one that runs.
		// Kept in memory only: after a restart the sweep falls back to stamping with its own day.
		internal static readonly Dictionary<ZDOID, int> FirstSeenEmpty = new Dictionary<ZDOID, int>();

		private static void Postfix(ZDO __instance)
		{
			if (!RestockRegenPlugin.Enabled.Value || LootChests.Prefabs.Count == 0)
			{
				return;
			}
			if (!LootChests.Prefabs.ContainsKey(__instance.GetPrefab()) || ZNet.instance == null || !ZNet.instance.IsServer())
			{
				return;
			}
			if (__instance.GetLong(ZDOVars.s_creator) != 0L)
			{
				return;
			}

			ZDOID id = __instance.m_uid;
			int today = EnvMan.instance != null ? EnvMan.instance.GetDay() : 0;
			int? count = LootChests.ItemCount(__instance);
			bool empty = count == 0;
			if (!empty)
			{
				s_announced.Remove(id);
				FirstSeenEmpty.Remove(id);
			}
			else if (!FirstSeenEmpty.ContainsKey(id) && __instance.GetInt(Restocker.s_emptySince) == 0)
			{
				FirstSeenEmpty[id] = today;
			}

			if (__instance.GetInt(ZDOVars.s_inUse) != 1)
			{
				s_open.Remove(id);
				s_announced.Remove(id);
				return;
			}

			// Open. The player who opened it owns it (Container.RPC_RequestOpen hands it over).
			long owner = __instance.GetOwner();
			ZNetPeer peer = owner != 0L && owner != ZDOMan.GetSessionID() ? ZNet.instance.GetPeer(owner) : null;
			if (peer == null)
			{
				return;
			}

			if (s_open.Add(id) && RestockRegenPlugin.ShowHistory.Value)
			{
				History.Record before = History.Opened(__instance, peer.m_playerName, today);
				string seen = History.Message(before, peer.m_playerName, today);
				if (seen != null)
				{
					Send(peer, MessageHud.MessageType.TopLeft, seen);
				}
			}

			if (empty && RestockRegenPlugin.Notify.Value && s_announced.Add(id))
			{
				int daysLeft = Restocker.DaysLeft(__instance, today);
				Send(peer, MessageHud.MessageType.Center, daysLeft > 0
					? RestockRegenPlugin.NotifyText.Value.Replace("{days}", daysLeft == 1 ? "1 day" : $"{daysLeft} days")
					: RestockRegenPlugin.NotifyDueText.Value);
			}
		}

		private static void Send(ZNetPeer peer, MessageHud.MessageType type, string text)
		{
			ZRoutedRpc.instance.InvokeRoutedRPC(peer.m_uid, "ShowMessage", (int)type, text);
			if (RestockRegenPlugin.Verbose.Value)
			{
				RestockRegenPlugin.Log.LogInfo($"told {peer.m_playerName}: {text}");
			}
		}
	}
}
