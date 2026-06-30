using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Lpc.Editor
{
    /// <summary>
    /// Auto-slicing importer for LPC catalog sheets (issue 2g8.3). Any texture that lands
    /// under a Catalog/&lt;slot&gt;/ folder (identified by a sibling catalog_index.json in the
    /// catalog root) is automatically configured as a sliced sprite sheet, and a matching
    /// <see cref="LpcLayerSet"/> asset is generated from catalog_index.json — so a copied
    /// sheet becomes a usable, ordered layer with zero manual setup.
    ///
    /// Each animation is sliced on its OWN grid (2g8.8): rows = directions and cols =
    /// framesPerDir come from <see cref="LpcClips"/>, and the cell size is derived from the
    /// PNG via <see cref="LpcSliceMath"/> so oversize weapon sheets (128/192px) slice
    /// correctly instead of being mis-cut into 64px tiles. Frame index = dir*cols + frame
    /// with row 0 = the TOP image row, matching the runtime convention. Every animation found
    /// for a part is assembled into that part's <see cref="LpcLayerSet.clips"/>, feeding the
    /// runtime clip system; the walk sheet is also kept in the legacy frames array.
    /// </summary>
    public class LpcCatalogPostprocessor : AssetPostprocessor
    {
        const int FrameSize = 64;     // LPC standard cell
        const float PixelsPerUnit = 32f;
        const string IndexFile = "catalog_index.json";

        // ---- preprocess: slice any catalog texture into a 64px grid -------------------

        void OnPreprocessTexture()
        {
            if (!IsCatalogTexture(assetPath, out _, out _)) return;
            var ti = (TextureImporter)assetImporter;

            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Multiple;
            ti.spritePixelsPerUnit = PixelsPerUnit;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.isReadable = true;   // runtime recolor (2g8.4) samples these via GetPixels32

            if (!ReadPngSize(assetPath, out int w, out int h)) return;
            string baseName = Path.GetFileNameWithoutExtension(assetPath);
            string anim = AnimFromBaseName(baseName);

            // Per-animation grid: rows = directions, cols = framesPerDir from the clip registry.
            // The cell size is derived from the PNG, so oversize sheets (128/192px weapon swings)
            // slice on their own larger grid instead of being mis-cut into 64px tiles.
            int cols, rows;
            if (LpcClips.TryGet(anim, out var clip)) { cols = clip.framesPerDir; rows = clip.directions; }
            else { cols = Mathf.Max(1, w / FrameSize); rows = Mathf.Max(1, h / FrameSize); }

            if (!LpcSliceMath.TrySlice(w, h, cols, rows, out var cells, out _, out _))
            {
                // PNG doesn't divide evenly by the clip's grid (custom/padded sheet):
                // fall back to a fixed 64px grid so we still produce usable sprites.
                cols = Mathf.Max(1, w / FrameSize); rows = Mathf.Max(1, h / FrameSize);
                cells = LpcSliceMath.Slice(w, h, cols, rows, FrameSize, FrameSize);
            }

            var metas = new List<SpriteMetaData>(cells.Length);
            foreach (var cell in cells)
                metas.Add(new SpriteMetaData
                {
                    rect = new Rect(cell.x, cell.y, cell.w, cell.h),
                    alignment = (int)SpriteAlignment.Custom,
                    pivot = new Vector2(0.5f, 0f),               // feet: bottom-center (oversize shares the baseline)
                    name = baseName + "_" + cell.index
                });
            ti.spritesheet = metas.ToArray();
        }

        // walk sheet is "<id>.png"; other animations are "<id>__<anim>.png" (see LpcCatalogImporter)
        static string AnimFromBaseName(string baseName)
        {
            if (string.IsNullOrEmpty(baseName)) return "walk";
            int i = baseName.LastIndexOf("__", System.StringComparison.Ordinal);
            return (i >= 0 && i + 2 < baseName.Length) ? baseName.Substring(i + 2) : "walk";
        }

        // ---- postprocess: build LpcLayerSet assets from catalog_index.json -------------

        static bool _busy;

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (_busy) return;
            var roots = new HashSet<string>();
            foreach (var p in imported)
                if (p.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase) && IsCatalogTexture(p, out var root, out _))
                    roots.Add(root);
            if (roots.Count == 0) return;

            _busy = true;
            try
            {
                foreach (var root in roots) GenerateLayerSets(root);
                AssetDatabase.SaveAssets();
            }
            finally { _busy = false; }
        }

        static void GenerateLayerSets(string root)
        {
            string indexPath = root + "/" + IndexFile;
            if (!File.Exists(indexPath)) return;
            var idx = JsonUtility.FromJson<LpcCatalogImporter.Index>(File.ReadAllText(indexPath));
            if (idx == null || idx.entries == null) return;

            string lsDir = root + "/LayerSets";
            Directory.CreateDirectory(lsDir);
            int made = 0, updated = 0;

            foreach (var e in idx.entries)
            {
                if (e.files == null || e.files.Length == 0) continue;

                // Assemble one clip per animation file into the runtime clip system (2g8.8).
                var clips = new List<LpcClipFrames>();
                Sprite[] walkFrames = null;
                for (int i = 0; i < e.files.Length; i++)
                {
                    var sprites = LoadOrderedSprites(e.files[i]);
                    if (sprites.Length == 0) { Debug.LogWarning($"[LPC] No sliced sprites yet for {e.files[i]}"); continue; }
                    string anim = (e.animations != null && i < e.animations.Length) ? e.animations[i] : "walk";
                    clips.Add(new LpcClipFrames { clip = anim, frames = sprites });
                    if (anim == "walk") walkFrames = sprites;
                }
                if (clips.Count == 0) continue;

                string assetPath = lsDir + "/" + e.slot + "_" + e.id + ".asset";
                var ls = AssetDatabase.LoadAssetAtPath<LpcLayerSet>(assetPath);
                bool isNew = ls == null;
                if (isNew) ls = ScriptableObject.CreateInstance<LpcLayerSet>();

                ls.slot = e.slot;
                ls.bodyType = string.IsNullOrEmpty(e.bodyType) ? LpcBodyType.Male : e.bodyType;
                ls.zOrder = e.zOrder;
                ls.clips = clips.ToArray();
                ls.frames = walkFrames ?? clips[0].frames;   // legacy/back-compat: walk if present, else first

                if (isNew) { AssetDatabase.CreateAsset(ls, assetPath); made++; }
                else { EditorUtility.SetDirty(ls); updated++; }
            }
            Debug.Log($"[LPC] LayerSets generated: {made} new, {updated} updated -> {lsDir}");
        }

        // ---- helpers ------------------------------------------------------------------

        static Sprite[] LoadOrderedSprites(string png)
        {
            var all = AssetDatabase.LoadAllAssetsAtPath(png);
            var list = new List<Sprite>();
            foreach (var o in all) if (o is Sprite s) list.Add(s);
            list.Sort((a, b) => TrailIndex(a.name).CompareTo(TrailIndex(b.name)));
            return list.ToArray();
        }

        static int TrailIndex(string name)
        {
            int i = name.LastIndexOf('_');
            if (i < 0 || i == name.Length - 1) return 0;
            return int.TryParse(name.Substring(i + 1), out var v) ? v : 0;
        }

        static bool IsCatalogTexture(string assetPath, out string root, out string slot)
        {
            root = null; slot = null;
            string dir = Path.GetDirectoryName(assetPath);
            if (string.IsNullOrEmpty(dir)) return false;
            string r = Path.GetDirectoryName(dir);
            if (string.IsNullOrEmpty(r)) return false;
            r = r.Replace('\\', '/');
            if (!File.Exists(r + "/" + IndexFile)) return false;
            root = r;
            slot = Path.GetFileName(dir);
            return true;
        }

        static bool ReadPngSize(string path, out int w, out int h)
        {
            w = 0; h = 0;
            try
            {
                using (var fs = File.OpenRead(path))
                {
                    var b = new byte[24];
                    if (fs.Read(b, 0, 24) < 24) return false;
                    // PNG IHDR: width at byte 16, height at 20, big-endian uint32
                    w = (b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19];
                    h = (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23];
                    return w > 0 && h > 0;
                }
            }
            catch { return false; }
        }
    }
}
