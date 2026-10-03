# Reads prefab settings out of a Valheim asset bundle (read-only).
# usage: prefabs.py <bundle file> <name regex>
import sys, re, json
import UnityPy

bundle, pattern = sys.argv[1], re.compile(sys.argv[2])
env = UnityPy.load(bundle)

def name_of(pptr):
    """Name of the object a reference points at, or a short description."""
    try:
        if not pptr or getattr(pptr, 'path_id', 0) == 0:
            return None
        o = pptr.read()
        n = getattr(o, 'm_Name', None)
        if n:
            return n
        go = getattr(o, 'm_GameObject', None)
        return name_of(go) if go else f"<{type(o).__name__}>"
    except Exception as e:
        return f"<unresolved {getattr(pptr, 'path_id', '?')}>"

def simplify(v, depth=0):
    if isinstance(v, dict):
        if set(v.keys()) == {'m_FileID', 'm_PathID'}:
            return None if v['m_PathID'] == 0 else f"ref:{v['m_FileID']}:{v['m_PathID']}"
        if depth > 3:
            return '{...}'
        return {k: simplify(x, depth + 1) for k, x in v.items()}
    if isinstance(v, list):
        return [simplify(x, depth + 1) for x in v[:12]] + (['...'] if len(v) > 12 else [])
    return v

# path_id -> object, to resolve references inside this bundle
by_id = {}
for obj in env.objects:
    by_id[obj.path_id] = obj

def ref_name(ref):
    if not isinstance(ref, str) or not ref.startswith('ref:'):
        return ref
    _, fid, pid = ref.split(':')
    o = by_id.get(int(pid)) if fid == '0' else None
    if o is None:
        return ref + ' (other file)'
    try:
        d = o.read()
        n = getattr(d, 'm_Name', '') or name_of(getattr(d, 'm_GameObject', None))
        return f"{n} [{o.type.name}]"
    except Exception:
        return ref

def resolve(v):
    if isinstance(v, dict):
        return {k: resolve(x) for k, x in v.items()}
    if isinstance(v, list):
        return [resolve(x) for x in v]
    return ref_name(v)

SKIP = {'m_ObjectHideFlags', 'm_CorrespondingSourceObject', 'm_PrefabInstance', 'm_PrefabAsset', 'm_GameObject',
        'm_Enabled', 'm_EditorHideFlags', 'm_EditorClassIdentifier', 'm_Name'}

found = 0
for obj in env.objects:
    if obj.type.name != 'GameObject':
        continue
    go = obj.read()
    if not pattern.search(go.m_Name):
        continue
    # only prefab roots: transform without a parent
    comps = [c.component if hasattr(c, 'component') else c for c in go.m_Component]
    root = True
    for c in comps:
        try:
            co = c.read()
            if type(co).__name__ in ('Transform', 'RectTransform'):
                f = getattr(co, 'm_Father', None)
                if f is not None and getattr(f, 'path_id', 0) != 0:
                    root = False
        except Exception:
            pass
    if not root:
        continue
    found += 1
    print(f"\n=== {go.m_Name}")
    for c in comps:
        try:
            o = by_id.get(c.path_id)
            if o is None or o.type.name != 'MonoBehaviour':
                continue
            tree = o.read_typetree()
            script = ref_name(simplify(tree.get('m_Script')))
            fields = {k: resolve(simplify(v)) for k, v in tree.items() if k not in SKIP and k != 'm_Script'}
            print(f"  [{script}]")
            for k, v in fields.items():
                s = json.dumps(v, default=str)
                if len(s) > 2400:
                    s = s[:2400] + ' ...'
                print(f"      {k} = {s}")
        except Exception as e:
            print(f"  <component unreadable: {type(e).__name__}: {str(e)[:100]}>")
print(f"\n{found} prefab roots matched")
