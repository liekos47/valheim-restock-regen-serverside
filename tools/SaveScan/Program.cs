// SaveScan: reads a Valheim 1.0 chunked world save (read-only) and lists every container ZDO.
using System.IO.Compression;
using System.Text;

string dir = args[0], namesGz = args[1], outDir = args[2];
// Optional 4th argument: comma-separated prefab names to list as well, whatever they are (objects.tsv).
var dumpNames = args.Length > 3 ? new HashSet<string>(args[3].Split(',')) : new HashSet<string>();
static int H(string s) { int a = 5381, b = a; for (int i = 0; i < s.Length && s[i] != '\0'; i += 2) { a = ((a << 5) + a) ^ s[i]; if (i == s.Length - 1 || s[i + 1] == '\0') break; b = ((b << 5) + b) ^ s[i + 1]; } return a + b * 1566083941; }

var names = new Dictionary<int, string>();
using (var r = new StreamReader(new GZipStream(File.OpenRead(namesGz), CompressionMode.Decompress)))
    for (string l; (l = r.ReadLine()) != null;) names.TryAdd(H(l), l);
string N(int h) => names.TryGetValue(h, out var n) ? n : "#" + h;

var dumpHashes = new HashSet<int>(dumpNames.Select(H)); var dumpRows = new List<string>();
int kHealth = H("health"), kPicked = H("picked");
int kItems = H("items"), kAdded = H("addedDefaultItems"), kCreator = H("creator"), kInUse = H("InUse");
var main = Directory.GetFiles(dir, "_main.*.chunks").Single();
var br0 = new BinaryReader(File.OpenRead(main));
br0.ReadUInt16(); int total = br0.ReadInt32(); int nChunks = br0.ReadInt32();
var files = new List<string>();
for (int i = 0; i < nChunks; i++) { ushort c = br0.ReadUInt16(); byte sz = br0.ReadByte(); uint ver = br0.ReadUInt32(); br0.ReadInt32();
    files.Add($"{c >> 8:x2}_{c & 0xFF:x2}__{sz}_{ver}.chunk"); }

static int NumItems(BinaryReader r) { int n = r.ReadByte(); if ((n & 0x80) != 0) n = ((n & 0x7F) << 8) | r.ReadByte(); return n; }
var rows = new List<string>(); var prefabCounts = new Dictionary<int, int>(); long zdoCount = 0;
var sb = new StringBuilder();
foreach (var f in files) {
    using var r = new BinaryReader(new BufferedStream(File.OpenRead(Path.Combine(dir, f)), 1 << 20));
    short ver = r.ReadInt16(); int n = r.ReadInt32();
    for (int z = 0; z < n; z++) {
        zdoCount++;
        ushort fl = r.ReadUInt16();
        bool chunked = ver >= 40;
        if (!chunked) { r.ReadInt16(); r.ReadInt16(); }
        float x, y = 0, zz;
        if ((fl & 0x2000) != 0) { x = r.ReadInt16(); zz = r.ReadInt16(); } else { x = r.ReadSingle(); y = r.ReadSingle(); zz = r.ReadSingle(); }
        int prefab = r.ReadInt32();
        if ((fl & 0x1000) != 0) { if (chunked) { ushort a = r.ReadUInt16(); if ((a & 0x8000) == 0) r.ReadUInt16(); } else { r.ReadSingle(); r.ReadSingle(); r.ReadSingle(); } }
        byte[] items = null; int? added = null; long? creator = null; int? inUse = null; string health = null; int? picked = null;
        if ((fl & 0xFF) != 0) {
            if ((fl & 1) != 0) { r.ReadByte(); r.ReadInt32(); }
            if ((fl & 2) != 0) { int c = NumItems(r); for (int i = 0; i < c; i++) { r.ReadInt32(); r.ReadSingle(); } }
            if ((fl & 4) != 0) { int c = NumItems(r); for (int i = 0; i < c; i++) { r.ReadInt32(); r.ReadBytes(12); } }
            if ((fl & 8) != 0) { int c = NumItems(r); for (int i = 0; i < c; i++) { r.ReadInt32(); r.ReadBytes(16); } }
            if ((fl & 0x10) != 0) { int c = NumItems(r); for (int i = 0; i < c; i++) { int k = r.ReadInt32(); int v = r.ReadInt32(); if (k == kAdded) added = v; else if (k == kInUse) inUse = v; else if (k == kPicked) picked = v; } }
            if ((fl & 0x20) != 0) { int c = NumItems(r); for (int i = 0; i < c; i++) { int k = r.ReadInt32(); long v = r.ReadInt64(); if (k == kCreator) creator = v; } }
            if ((fl & 0x40) != 0) { int c = NumItems(r); for (int i = 0; i < c; i++) { int k = r.ReadInt32(); string v = r.ReadString(); if (k == kHealth) health = v; } }
            if ((fl & 0x80) != 0) { int c = NumItems(r); for (int i = 0; i < c; i++) { int k = r.ReadInt32(); var v = r.ReadBytes(r.ReadInt32()); if (k == kItems) items = v; } }
        }
        if (dumpHashes.Contains(prefab)) dumpRows.Add(string.Join('	', N(prefab), x.ToString("0"), y.ToString("0"), zz.ToString("0"), creator.HasValue ? "player" : "world", health == null ? "-" : "health:" + health.Length, picked == null ? "-" : "picked:" + picked));
        if (items == null && added == null) continue;
        prefabCounts[prefab] = prefabCounts.GetValueOrDefault(prefab) + 1;
        // decode inventory
        int nItems = -1; sb.Clear();
        if (items != null) try {
            var ir = new BinaryReader(new MemoryStream(items)); int iv = ir.ReadInt32();
            if (iv >= 108) { nItems = ir.ReadUInt16();
                for (int i = 0; i < nItems; i++) { ir.ReadInt32(); ir.ReadByte(); ir.ReadByte(); ir.ReadByte(); byte b = ir.ReadByte();
                    if ((b & 4) != 0) ir.ReadUInt16(); int st = (b & 8) != 0 ? ir.ReadUInt16() : 1; if ((b & 0x10) != 0) ir.ReadInt32();
                    if ((b & 0x20) != 0) { ir.ReadInt64(); ir.ReadString(); } int ph = (b & 0x40) != 0 ? ir.ReadInt32() : 0;
                    int cd = (b & 0x80) != 0 ? NumItems(ir) : 0; for (int j = 0; j < cd; j++) { ir.ReadString(); ir.ReadString(); }
                    if (iv >= 109 || iv == 107) ir.ReadByte();
                    sb.Append(N(ph)).Append('x').Append(st).Append(' '); } }
            else { nItems = ir.ReadInt32(); for (int i = 0; i < nItems; i++) sb.Append(ir.ReadString()).Append(' '); sb.Append("(oldfmt)"); }
        } catch (Exception e) { sb.Append("DECODE-ERR " + e.GetType().Name); }
        rows.Add(string.Join('\t', N(prefab), x.ToString("0"), y.ToString("0"), zz.ToString("0"), creator.HasValue ? "player" : "world",
            added == null ? "-" : added.ToString(), items == null ? "noitems" : nItems.ToString(), inUse?.ToString() ?? "-", sb.ToString().Trim()));
    }
    if (r.BaseStream.Position != r.BaseStream.Length) Console.WriteLine($"WARN {f}: {r.BaseStream.Length - r.BaseStream.Position} bytes left over");
}
Directory.CreateDirectory(outDir);
File.WriteAllLines(Path.Combine(outDir, "containers.tsv"), new[] { "prefab\tx\ty\tz\tbuilt_by\taddedDefaultItems\titem_count\tinUse\titems" }.Concat(rows.OrderBy(s => s)));
if (dumpNames.Count > 0) File.WriteAllLines(Path.Combine(outDir, "objects.tsv"), new[] { "prefab	x	y	z	built_by	health	picked" }.Concat(dumpRows.OrderBy(s => s)));
Console.WriteLine($"ZDOs read: {zdoCount:N0} (header says {total:N0}), chunk files: {files.Count}, containers: {rows.Count:N0}");
