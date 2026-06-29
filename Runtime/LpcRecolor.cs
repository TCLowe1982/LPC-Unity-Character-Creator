using System.Collections.Generic;
using UnityEngine;

namespace Lpc
{
    /// <summary>
    /// Runtime palette-swap recolour. A base LPC layer is drawn in some reference ramp; we
    /// detect that ramp's shades from the texture, then remap each pixel onto a target ramp
    /// shade-for-shade (preserving the shading structure and alpha). The layer's frames all
    /// share one texture, so we recolour the texture ONCE and re-slice — cheap enough to do
    /// live as the player cycles colours.
    ///
    /// Source shades are the most-common opaque colours (the ramp dominates; anti-aliased
    /// edge pixels snap to the nearest shade). Both ramps are ordered by luminance so a
    /// dark base shade maps to the dark target shade, etc. Requires the source texture to be
    /// read/write enabled (the catalog postprocessor marks catalog textures readable).
    /// </summary>
    public static class LpcRecolor
    {
        static readonly Dictionary<int, Texture2D> _cache = new Dictionary<int, Texture2D>();

        /// <summary>Recolour a layer's frames onto <paramref name="target"/>; returns new frames.</summary>
        public static Sprite[] RecolorFrames(Sprite[] baseFrames, Color[] target)
        {
            if (baseFrames == null || baseFrames.Length == 0 || target == null || target.Length == 0)
                return baseFrames;

            var srcTex = baseFrames[0] != null ? baseFrames[0].texture : null;
            if (srcTex == null) return baseFrames;

            var recolored = RecolorTexture(srcTex, target);
            if (recolored == null) return baseFrames;

            var outFrames = new Sprite[baseFrames.Length];
            for (int i = 0; i < baseFrames.Length; i++)
            {
                var b = baseFrames[i];
                if (b == null) { outFrames[i] = null; continue; }
                var pivot = new Vector2(
                    b.rect.width > 0 ? b.pivot.x / b.rect.width : 0.5f,
                    b.rect.height > 0 ? b.pivot.y / b.rect.height : 0f);
                outFrames[i] = Sprite.Create(recolored, b.rect, pivot, b.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                outFrames[i].name = b.name + "_recolor";
            }
            return outFrames;
        }

        /// <summary>Recolour a whole sheet texture onto a target ramp. Cached by (texture, ramp).</summary>
        public static Texture2D RecolorTexture(Texture2D src, Color[] target)
        {
            if (src == null || target == null || target.Length == 0) return null;
            int key = src.GetInstanceID() * 397 ^ RampKey(target);
            if (_cache.TryGetValue(key, out var hit) && hit != null) return hit;

            Color32[] px;
            try { px = src.GetPixels32(); }
            catch { Debug.LogWarning($"[LPC] Recolor needs a readable texture: {src.name}"); return null; }

            int n = target.Length;
            // detect the n most-common opaque source shades
            var hist = new Dictionary<int, int>();
            var col = new Dictionary<int, Color32>();
            for (int i = 0; i < px.Length; i++)
            {
                if (px[i].a < 16) continue;
                int c = (px[i].r << 16) | (px[i].g << 8) | px[i].b;
                hist[c] = hist.TryGetValue(c, out var v) ? v + 1 : 1;
                col[c] = px[i];
            }
            var shades = new List<Color32>();
            foreach (var kv in SortByValueDesc(hist)) { shades.Add(col[kv]); if (shades.Count >= n) break; }
            if (shades.Count == 0) return null;
            shades.Sort((a, b) => Lum(a).CompareTo(Lum(b)));

            var tgt = new List<Color>(target);
            tgt.Sort((a, b) => Lum(a).CompareTo(Lum(b)));
            // pad/clamp target to source-shade count
            while (tgt.Count < shades.Count) tgt.Add(tgt[tgt.Count - 1]);

            var outPx = new Color32[px.Length];
            for (int i = 0; i < px.Length; i++)
            {
                var p = px[i];
                if (p.a < 16) { outPx[i] = p; continue; }
                int idx = Nearest(p, shades);
                var t = tgt[Mathf.Min(idx, tgt.Count - 1)];
                outPx[i] = new Color32((byte)(t.r * 255), (byte)(t.g * 255), (byte)(t.b * 255), p.a);
            }

            var nt = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            nt.SetPixels32(outPx);
            nt.Apply();
            _cache[key] = nt;
            return nt;
        }

        // ---- helpers ------------------------------------------------------------------

        static int Nearest(Color32 p, List<Color32> shades)
        {
            int best = 0, bestD = int.MaxValue;
            for (int i = 0; i < shades.Count; i++)
            {
                int dr = p.r - shades[i].r, dg = p.g - shades[i].g, db = p.b - shades[i].b;
                int d = dr * dr + dg * dg + db * db;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        static float Lum(Color32 c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
        static float Lum(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

        static int RampKey(Color[] ramp)
        {
            int h = 17;
            foreach (var c in ramp) h = h * 31 + ((Color32)c).GetHashCode();
            return h;
        }

        static IEnumerable<int> SortByValueDesc(Dictionary<int, int> d)
        {
            var list = new List<KeyValuePair<int, int>>(d);
            list.Sort((a, b) => b.Value.CompareTo(a.Value));
            foreach (var kv in list) yield return kv.Key;
        }
    }
}
