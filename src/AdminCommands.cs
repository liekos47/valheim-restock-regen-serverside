using System;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;

namespace RestockRegen
{
	/*
		Admin commands, carried by Valheim's own "kick" console command:

			/kick restock-help                    (in the chat box, or "kick restock-help" in the F5 console)
			/kick restock-status
			/kick restock-reload                  (also /kick reload-restock-regen)
			/kick restock-regen
			/kick restock-regen-armor
			/kick restock-regen-obsidian
			/kick restock-regen-crystals
			/kick restock-regen-cores
			/kick restock-regen-flametal
			/kick restock-get:MudPileDays
			/kick restock-set:MudPileDays=20

		A server-only mod cannot add console or chat commands of its own: the console runs on the
		player's machine, and a chat line starting with "/" is run there as a console command
		(Chat.InputText). Plain chat does not help either: on Steam it is sent to each other player
		separately (Chat.CheckPermissionsAndSendChatMessageRPCsAsync), so with nobody else online
		it never reaches the server. "kick <word>" does: ZNet.Kick sends the word to the server's
		ZNet.RPC_Kick, which checks adminlist.txt. This prefix takes words starting with
		"restock-" before anyone is kicked, and answers in the admin's console (RemotePrint) and
		top-left on screen. Only the first word after "kick" is sent, hence the one-word syntax.
	*/
	[HarmonyPatch(typeof(ZNet), "RPC_Kick")]
	internal static class AdminCommands
	{
		private const string ScriptEngineGuid = "com.bepis.bepinex.scriptengine";

		private static bool Prefix(ZNet __instance, ZRpc rpc, string user)
		{
			string word = (user ?? "").Trim();
			string command;
			if (word.Equals("reload-restock-regen", StringComparison.OrdinalIgnoreCase))
			{
				command = "reload";
			}
			else if (word.StartsWith("restock-", StringComparison.OrdinalIgnoreCase))
			{
				command = word.Substring("restock-".Length);
			}
			else
			{
				return true; // a real kick
			}

			ZNetPeer peer = __instance.GetPeer(rpc);
			if (!__instance.ListContainsId(__instance.m_adminList, rpc.GetSocket().GetHostName()))
			{
				__instance.RemotePrint(rpc, "RestockRegen commands are for server admins");
				return false;
			}
			RestockRegenPlugin.Log.LogInfo($"admin command from {peer?.m_playerName ?? "?"}: {word}");
			try
			{
				foreach (string line in Handle(command))
				{
					__instance.RemotePrint(rpc, line);
					if (peer != null)
					{
						ZRoutedRpc.instance.InvokeRoutedRPC(peer.m_uid, "ShowMessage", (int)MessageHud.MessageType.TopLeft, line);
					}
				}
			}
			catch (Exception e)
			{
				RestockRegenPlugin.Log.LogError($"admin command '{word}' failed: {e}");
				__instance.RemotePrint(rpc, "RestockRegen: command failed, see the server log");
			}
			return false; // never reaches the real kick
		}

		private static string[] Handle(string command)
		{
			string lower = command.ToLowerInvariant();
			if (lower == "reload")
			{
				if (!CanReload())
				{
					return new[] { "RestockRegen: hot reload is not set up (ScriptEngine missing, or the mod is not in BepInEx/scripts)" };
				}
				RestockRegenPlugin.Instance.Invoke(nameof(RestockRegenPlugin.HotReload), 1f);
				return new[] { $"RestockRegen {RestockRegenPlugin.Version}: reloading from BepInEx/scripts..." };
			}
			if (lower == "status")
			{
				return new[] { RestockRegenPlugin.Instance.Status() };
			}
			if (lower == "regen-cores")
			{
				RestockRegenPlugin.Instance.RegenDungeonsNow(RestockRegenPlugin.BlackCoreKind);
				return new[] { "RestockRegen: regenerating every free infested mine with taken black cores now (details in the server log)" };
			}
			if (lower == "regen-crystals")
			{
				RestockRegenPlugin.Instance.RegenDungeonsNow(RestockRegenPlugin.CrystalKind);
				return new[] { "RestockRegen: regenerating every free frost cave with picked crystals now (details in the server log)" };
			}
			if (lower.StartsWith("regen-"))
			{
				string kind = lower.Substring("regen-".Length);
				return RestockRegenPlugin.Instance.RegrowNow(kind)
					? new[] { $"RestockRegen: restoring every damaged or gone {Regrow.Find(kind).Label} spot with nobody near and no build on it, now (details in the server log)" }
					: new[] { "RestockRegen: use restock-regen (crypts), restock-regen-crystals, restock-regen-cores, restock-regen-armor, restock-regen-obsidian or restock-regen-flametal" };
			}
			if (lower == "regen")
			{
				RestockRegenPlugin.Instance.RegenDungeonsNow(RestockRegenPlugin.MudKind);
				return new[] { "RestockRegen: regenerating every free sunken crypt with mined piles now (details in the server log)" };
			}
			if (lower.StartsWith("get:"))
			{
				ConfigEntryBase entry = Find(command.Substring(4));
				return new[] { entry == null ? $"RestockRegen: no setting '{command.Substring(4)}'" : $"{entry.Definition.Key} = {entry.GetSerializedValue()}" };
			}
			if (lower.StartsWith("set:"))
			{
				string[] kv = command.Substring(4).Split(new[] { '=' }, 2);
				ConfigEntryBase entry = kv.Length == 2 ? Find(kv[0]) : null;
				if (entry == null)
				{
					return new[] { "RestockRegen: use restock-set:Setting=value (restock-help lists them)" };
				}
				// One word only reaches the server, so "_" stands for a space in text settings.
				string value = entry.SettingType == typeof(string) ? kv[1].Replace('_', ' ') : kv[1];
				entry.SetSerializedValue(value); // saved to the config file
				return new[] { $"{entry.Definition.Key} = {entry.GetSerializedValue()}" };
			}
			var help = new[]
			{
				"RestockRegen commands (in chat: /kick <command>, in the F5 console: kick <command>):",
				"  restock-status   restock-regen   restock-regen-crystals   restock-regen-cores   restock-regen-armor   restock-regen-obsidian   restock-regen-flametal   restock-reload   restock-get:Setting   restock-set:Setting=value",
				"  In text settings type _ for a space. Settings: " + string.Join(", ", RestockRegenPlugin.Instance.Config.Keys.Select(k => k.Key)),
			};
			return help;
		}

		private static ConfigEntryBase Find(string key)
		{
			ConfigFile config = RestockRegenPlugin.Instance.Config;
			ConfigDefinition def = config.Keys.FirstOrDefault(k => k.Key.Equals(key.Trim(), StringComparison.OrdinalIgnoreCase));
			return def == null ? null : config[def];
		}

		private static bool CanReload()
		{
			return Chainloader.PluginInfos.TryGetValue(ScriptEngineGuid, out var info) && info.Instance != null
				&& (RestockRegenPlugin.Instance.Info.Location ?? "").Replace('\\', '/').Contains("/scripts/");
		}

		// ScriptEngine.ReloadPlugins is private; it destroys every plugin it loaded from
		// BepInEx/scripts and loads the DLLs there again.
		internal static bool Reload()
		{
			if (!Chainloader.PluginInfos.TryGetValue(ScriptEngineGuid, out var info) || info.Instance == null)
			{
				return false;
			}
			MethodInfo reload = info.Instance.GetType().GetMethod("ReloadPlugins", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			if (reload == null)
			{
				return false;
			}
			reload.Invoke(info.Instance, null);
			return true;
		}
	}
}
