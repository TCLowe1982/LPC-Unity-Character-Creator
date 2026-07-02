using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Lpc.Editor
{
    /// <summary>One layer of an LPC sheet_definition: its draw order (zPos), the per-body-type
    /// source paths it maps to (relative to spritesheets/, e.g. "cape/solid/fg"), and the
    /// custom animation it is drawn with (e.g. "slash_oversize" for a 192px big-weapon swing),
    /// null for standard 64px layers.</summary>
    public class LpcSheetLayer
    {
        public int zPos;
        public string customAnimation;
        public List<string> sources = new List<string>();
    }

    /// <summary>Parsed LPC sheet_definition: name + its layers. Multi-layer parts (e.g. a cape's
    /// foreground `layer_1` and behind-body `layer_2`) carry different zPos here.</summary>
    public class LpcSheetDef
    {
        public string name;
        public List<LpcSheetLayer> layers = new List<LpcSheetLayer>();
    }

    /// <summary>
    /// Parses LPC <c>sheet_definitions/*.json</c> for draw order. The JSON has dynamic keys
    /// (`layer_1`, `layer_2`, …; per-body-type path keys), so JsonUtility can't read it — this is a
    /// small tolerant regex parser for the fields we need: each layer's <c>zPos</c> and its source
    /// paths. This is how the importer learns the correct z for multi-layer parts (a cape's fg in
    /// front of the body, its bg behind) instead of falling back to one category default.
    ///
    /// Pure string logic (no Unity), so it's unit-tested with synthetic JSON. Embedded `credits`
    /// in the definition are NOT parsed here — attribution comes from CREDITS.csv (see LpcCreditsReader).
    /// </summary>
    public static class LpcSheetDefParser
    {
        static readonly Regex LayerRx = new Regex("\"layer_\\d+\"\\s*:\\s*\\{([^}]*)\\}", RegexOptions.Singleline);
        static readonly Regex ZRx = new Regex("\"zPos\"\\s*:\\s*(-?\\d+)");
        static readonly Regex PathRx = new Regex("\"(\\w+)\"\\s*:\\s*\"([^\"]+)\"");
        static readonly Regex NameRx = new Regex("\"name\"\\s*:\\s*\"([^\"]*)\"");

        public static LpcSheetDef Parse(string json)
        {
            var def = new LpcSheetDef();
            if (string.IsNullOrEmpty(json)) return def;

            var nameM = NameRx.Match(json);
            if (nameM.Success) def.name = nameM.Groups[1].Value;

            // layer blocks are flat (zPos + body-type:path strings, no nested objects), so [^}]* is safe
            foreach (Match lm in LayerRx.Matches(json))
            {
                string body = lm.Groups[1].Value;
                var layer = new LpcSheetLayer();
                var zM = ZRx.Match(body);
                layer.zPos = zM.Success ? int.Parse(zM.Groups[1].Value) : 0;
                foreach (Match pm in PathRx.Matches(body))
                {
                    if (pm.Groups[1].Value == "zPos") continue; // numeric, not a path
                    if (pm.Groups[1].Value == "custom_animation") // oversize/remapped playback, not a path
                    { layer.customAnimation = pm.Groups[2].Value; continue; }
                    string path = pm.Groups[2].Value.Replace('\\', '/').Trim().TrimEnd('/');
                    if (path.Length > 0 && !layer.sources.Contains(path)) layer.sources.Add(path);
                }
                def.layers.Add(layer);
            }
            return def;
        }
    }
}
