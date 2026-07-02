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
            foreach (var def in EnumerateDefs(lpcSourcePath))
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
            return map;
        }

        /// <summary>
        /// source path -> its parsed sheet_definition, so the importer can expand a manifest
        /// entry into the def's layers (a weapon's fg/bg/oversize-attack sheets, a cape's
        /// fg/bg) with each layer's own zPos. Keyed by every layer source path and its
        /// body-type-stripped form; the FIRST def claiming a path wins.
        /// </summary>
        public static Dictionary<string, LpcSheetDef> BuildDefIndex(string lpcSourcePath)
        {
            var map = new Dictionary<string, LpcSheetDef>();
            foreach (var def in EnumerateDefs(lpcSourcePath))
                foreach (var layer in def.layers)
                    foreach (var src in layer.sources)
                    {
                        if (!map.ContainsKey(src)) map[src] = def;
                        var segs = src.Split('/');
                        if (segs.Length >= 2 && LpcBodyType.IsKnown(segs[segs.Length - 1]))
                        {
                            string stripped = string.Join("/", segs, 0, segs.Length - 1);
                            if (!map.ContainsKey(stripped)) map[stripped] = def;
                        }
                    }
            return map;
        }

        static IEnumerable<LpcSheetDef> EnumerateDefs(string lpcSourcePath)
        {
            if (string.IsNullOrEmpty(lpcSourcePath)) yield break;
            string dir = lpcSourcePath.Replace('\\', '/').TrimEnd('/') + "/sheet_definitions";
            if (!Directory.Exists(dir)) yield break;

            foreach (var f in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories))
            {
                LpcSheetDef def;
                try { def = LpcSheetDefParser.Parse(File.ReadAllText(f)); }
                catch { continue; }
                yield return def;
            }
        }
    }
}
