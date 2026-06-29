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
    /// Grid is derived from the PNG dimensions (frame size 64), so it is animation-agnostic:
    /// walk = 9x4 = 36, hurt = 6x1, etc. Frame index = row*cols + col with row 0 = the TOP
    /// image row, matching the runtime convention (index = direction*framesPerDir + frame,
    /// rows up/left/down/right). 2g8.8 will extend this to oversize/offset animation sheets.
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

            if (!ReadPngSize(assetPath, out int w, out int h)) return;
            int cols = Mathf.Max(1, w / FrameSize);
            int rows = Mathf.Max(1, h / FrameSize);
            string baseName = Path.GetFileNameWithoutExtension(assetPath);

            var metas = new List<SpriteMetaData>(cols * rows);
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    var m = new SpriteMetaData
                    {
                        // Unity texture space has y=0 at the bottom, so the top image row (r=0)
                        // sits at the highest y. This keeps index = dir*cols + frame.
                        rect = new Rect(c * FrameSize, h - (r + 1) * FrameSize, FrameSize, FrameSize),
                        alignment = (int)SpriteAlignment.Custom,
                        pivot = new Vector2(0.5f, 0f),               // feet: bottom-center
                        name = baseName + "_" + (r * cols + c)
                    };
                    metas.Add(m);
                }
            ti.spritesheet = metas.ToArray();
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
                var sprites = LoadOrderedSprites(e.files[0]);   // walk-first
                if (sprites.Length == 0) { Debug.LogWarning($"[LPC] No sliced sprites yet for {e.files[0]}"); continue; }

                string assetPath = lsDir + "/" + e.slot + "_" + e.id + ".asset";
                var ls = AssetDatabase.LoadAssetAtPath<LpcLayerSet>(assetPath);
                bool isNew = ls == null;
                if (isNew) ls = ScriptableObject.CreateInstance<LpcLayerSet>();

                ls.slot = e.slot;
                ls.zOrder = e.zOrder;
                ls.frames = sprites;

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
