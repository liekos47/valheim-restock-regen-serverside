using UnityEngine;

namespace RestockRegen
{
	// Creating and removing world objects from the server, shared by DungeonRegrow and Regrow.
	internal static class WorldObjects
	{
		// The same steps ZNetView.Awake takes for an object that has no ZDO yet: CreateNewZDO,
		// then Persistent/Type/Distant from the prefab's ZNetView, prefab and rotation.
		internal static bool Create(int prefabHash, Vector3 pos, Vector3 rot)
		{
			GameObject prefab = ZNetScene.instance.GetPrefab(prefabHash);
			ZNetView view = prefab != null ? prefab.GetComponent<ZNetView>() : null;
			if (view == null)
			{
				return false;
			}
			ZDO zdo = ZDOMan.instance.CreateNewZDO(pos, prefabHash);
			zdo.Persistent = view.m_persistent;
			zdo.Type = view.m_type;
			zdo.Distant = view.m_distant;
			zdo.SetPrefab(prefabHash);
			zdo.SetRotation(Quaternion.Euler(rot));
			return true;
		}

		// ZDOMan.DestroyZDO only acts for the owner; the server takes ownership first. The destroy is
		// handled on the server itself and sent to every client.
		internal static void Remove(ZDO zdo)
		{
			zdo.SetOwner(ZDOMan.GetSessionID());
			ZDOMan.instance.DestroyZDO(zdo);
		}

		internal static bool HeldByPlayer(ZDO zdo) =>
			zdo != null && zdo.HasOwner() && zdo.GetOwner() != ZDOMan.GetSessionID();
	}
}
