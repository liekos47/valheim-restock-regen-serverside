PrefabDump - read a prefab's settings out of Valheim's asset bundles (read-only).

What the game does with an object (does a pickable respawn, is it deleted or kept when picked, what
does a pile drop) is set on its prefab, in the asset bundles, not in the game's code. Reading it
beats inferring it from the code's defaults.

Setup, once:
    python -m venv venv
    venv\Scripts\python -m pip install UnityPy

Find which bundle holds a prefab: in <Valheim>\valheim_Data\StreamingAssets\SoftRef\manifest_extended
search for "/<PrefabName>.prefab"; the line above it is "bundle: <id>".

Run:
    venv\Scripts\python prefabs.py "<Valheim>\valheim_Data\StreamingAssets\SoftRef\Bundles\<id>" "^(Prefab1|Prefab2)$"

It prints every script component on each matching prefab root with its fields. The script names
show as references to another file; the fields identify them (m_respawnTimeMinutes and
m_hideWhenPicked: Pickable; m_spawnWhenDestroyed: Destructible; m_dropWhenDestroyed:
DropOnDestroyed; m_randomItemPrefabs: PickableItem; m_hitReactionChance: Leviathan;
m_persistent: ZNetView). Most world prefabs are in one large bundle, which takes a minute to load.
