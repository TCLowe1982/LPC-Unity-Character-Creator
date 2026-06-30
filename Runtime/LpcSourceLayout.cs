using System.Collections.Generic;

namespace Lpc
{
    /// <summary>One selectable LPC part: its category (slot), its source path (relative to
    /// spritesheets/, WITHOUT the body-type or animation segments), and which body types and
    /// animations exist for it. This is exactly what a catalog manifest entry needs.</summary>
    public class LpcPartOption
    {
        public string category;                       // slot, e.g. "torso"
        public string source;                         // e.g. "torso/clothes/longsleeve/longsleeve"
        public List<string> bodyTypes = new List<string>();   // e.g. ["male","female"] ([] = body-agnostic)
        public List<string> animations = new List<string>();  // e.g. ["walk","slash",...]
    }

    /// <summary>
    /// Pure grouping of an LPC spritesheets tree into selectable parts. Given relative PNG
    /// paths (e.g. "body/bodies/male/walk.png"), it figures out the part path, body type, and
    /// animation for each and groups them. The body type is the segment just before the
    /// animation file IF it's a known <see cref="LpcBodyType"/> — so "body/bodies/male/walk.png"
    /// is part "body/bodies" (male, walk), while "hair/afro/adult/walk.png" is part
    /// "hair/afro/adult" (no body type, walk) because "adult" isn't a body type.
    ///
    /// The produced <c>source</c> is exactly what the importer expects (it appends
    /// /&lt;bodytype&gt;/&lt;anim&gt;.png, or &lt;anim&gt;.png when body-agnostic). Pure, so it
    /// unit-tests offline.
    /// </summary>
    public static class LpcSourceLayout
    {
        public static List<LpcPartOption> GroupParts(IEnumerable<string> relativePngPaths)
        {
            var byKey = new Dictionary<string, LpcPartOption>();
            var order = new List<string>();
            if (relativePngPaths == null) return new List<LpcPartOption>();

            foreach (var raw in relativePngPaths)
            {
                if (string.IsNullOrEmpty(raw)) continue;
                var p = raw.Replace('\\', '/').Trim().TrimStart('/');
                if (!p.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)) continue;

                var segs = p.Split('/');
                if (segs.Length < 2) continue;                    // need at least category/anim.png

                string anim = segs[segs.Length - 1];
                anim = anim.Substring(0, anim.Length - 4);        // strip ".png"
                string category = segs[0];

                int partEnd = segs.Length - 1;                    // exclusive of the anim file
                string bodyType = null;
                if (segs.Length >= 3 && LpcBodyType.IsKnown(segs[segs.Length - 2]))
                {
                    bodyType = segs[segs.Length - 2];
                    partEnd = segs.Length - 2;
                }
                string source = string.Join("/", segs, 0, partEnd);

                if (!byKey.TryGetValue(source, out var opt))
                {
                    opt = new LpcPartOption { category = category, source = source };
                    byKey[source] = opt;
                    order.Add(source);
                }
                if (bodyType != null && !opt.bodyTypes.Contains(bodyType)) opt.bodyTypes.Add(bodyType);
                if (!opt.animations.Contains(anim)) opt.animations.Add(anim);
            }

            var list = new List<LpcPartOption>(order.Count);
            foreach (var k in order) list.Add(byKey[k]);
            return list;
        }
    }
}
