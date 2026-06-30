using System.Collections.Generic;
using System.IO;

namespace Lpc.Editor
{
    /// <summary>
    /// Builds a source-path → zPos map from an LPC clone's <c>sheet_definitions/</c>, so the
    /// importer can assign each part its real draw order (especially multi-layer parts like a
    /// cape, whose foreground and background layers have very different zPos). Falls back to the
    /// category default only when a part has no definition.
    /// </summary>
    public static class LpcSheetDefIndex
    {
        public static Dictionary<string, int> BuildZIndex(string lpcSourcePath)
        {
            var map = new Dictionary<string, int>();
            if (string.IsNullOrEmpty(lpcSourcePath)) return map;
            string dir = lpcSourcePath.Replace('\\', '/').TrimEnd('/') + "/sheet_definitions";
            if (!Directory.Exists(dir)) return map;

            foreach (var f in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories))
            {
                LpcSheetDef def;
                try { def = LpcSheetDefParser.Parse(File.ReadAllText(f)); }
                catch { continue; }
                foreach (var layer in def.layers)
                    foreach (var src in layer.sources)
                    {
                        map[src] = layer.zPos; // full def path
                        // also key by the body-type-stripped path, since the importer's source
                        // omits the <bodytype> segment (e.g. "neck/capeclip/male" -> "neck/capeclip")
                        var segs = src.Split('/');
                        if (segs.Length >= 2 && LpcBodyType.IsKnown(segs[segs.Length - 1]))
                            map[string.Join("/", segs, 0, segs.Length - 1)] = layer.zPos;
                    }
            }
            return map;
        }
    }
}
