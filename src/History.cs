using System.Collections.Generic;
using System.Linq;

namespace RestockRegen
{
	/*
		Who first opened a loot chest and who opened it last, with the in-game day, so the next
		visitor can be told who was there before them.

		Stored on the chest's own ZDO, so it lives in the world save. But never written while a
		player holds the chest: the server rejects an incoming ZDO update whose data revision is not
		above its own (ZDOMan.RPC_ZDOData), so a server-side write to a chest the player owns could
		make the player's next change to it - taking an item - get dropped. An opening is therefore
		kept here in memory and written by Flush once the chest has no player owner, which happens a
		few seconds after the player leaves its area. A restart before that loses the pending record.
	*/
	internal static class History
	{
		private static readonly int s_firstBy = "restockregen_firstby".GetStableHashCode();
		private static readonly int s_firstDay = "restockregen_firstday".GetStableHashCode();
		private static readonly int s_lastBy = "restockregen_lastby".GetStableHashCode();
		private static readonly int s_lastDay = "restockregen_lastday".GetStableHashCode();

		internal class Record
		{
			public string FirstBy = "", LastBy = "";
			public int FirstDay, LastDay;
			public bool Empty => LastBy.Length == 0;
		}

		private static readonly Dictionary<ZDOID, Record> s_pending = new Dictionary<ZDOID, Record>();

		internal static Record Get(ZDO zdo)
		{
			if (s_pending.TryGetValue(zdo.m_uid, out Record pending))
			{
				return pending;
			}
			// Days are stored as day + 1 so that 0 means "never".
			return new Record
			{
				FirstBy = zdo.GetString(s_firstBy),
				FirstDay = zdo.GetInt(s_firstDay) - 1,
				LastBy = zdo.GetString(s_lastBy),
				LastDay = zdo.GetInt(s_lastDay) - 1,
			};
		}

		// Records an opening and returns the record as it was before it, for the message.
		internal static Record Opened(ZDO zdo, string player, int today)
		{
			Record before = Get(zdo);
			var after = new Record
			{
				FirstBy = before.FirstBy.Length > 0 ? before.FirstBy : player,
				FirstDay = before.FirstBy.Length > 0 ? before.FirstDay : today,
				LastBy = player,
				LastDay = today,
			};
			s_pending[zdo.m_uid] = after;
			return before;
		}

		internal static void Flush()
		{
			if (s_pending.Count == 0)
			{
				return;
			}
			long server = ZDOMan.GetSessionID();
			foreach (ZDOID id in s_pending.Keys.ToList())
			{
				ZDO zdo = ZDOMan.instance.GetZDO(id);
				if (zdo == null)
				{
					s_pending.Remove(id); // destroyed
					continue;
				}
				if (zdo.HasOwner() && zdo.GetOwner() != server)
				{
					continue; // a player still holds it
				}
				Record r = s_pending[id];
				s_pending.Remove(id);
				if (RestockRegenPlugin.DryRun.Value)
				{
					continue;
				}
				zdo.Set(s_firstBy, r.FirstBy);
				zdo.Set(s_firstDay, r.FirstDay + 1);
				zdo.Set(s_lastBy, r.LastBy);
				zdo.Set(s_lastDay, r.LastDay + 1);
			}
		}

		// The message for the player opening the chest, from the record before their opening.
		// Null when nobody has been recorded yet.
		internal static string Message(Record before, string player, int today)
		{
			if (before.Empty)
			{
				return null;
			}
			string last = before.LastBy == player ? "you" : before.LastBy;
			string first = before.FirstBy == player ? "you" : before.FirstBy;
			bool once = before.FirstBy == before.LastBy && before.FirstDay == before.LastDay;
			string template = once ? RestockRegenPlugin.HistoryTextOnce.Value : RestockRegenPlugin.HistoryText.Value;
			return template
				.Replace("{last}", last).Replace("{lastago}", Ago(today - before.LastDay))
				.Replace("{first}", first).Replace("{firstago}", Ago(today - before.FirstDay));
		}

		private static string Ago(int days)
		{
			if (days <= 0) return "today";
			if (days == 1) return "yesterday";
			return $"{days} days ago";
		}
	}
}
