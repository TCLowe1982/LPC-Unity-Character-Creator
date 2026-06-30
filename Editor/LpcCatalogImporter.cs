using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Lpc.Editor
{
    /// <summary>
    /// Selective LPC catalog importer (walk-first). Reads a small JSON manifest that
    /// SELECTS which parts to bring in from a local clone of the LPC Spritesheet
    /// Character Generator, then copies ONLY those animation sheets into the consuming
    /// project's Catalog/&lt;slot&gt;/ layout and writes a catalog_index.json (consumed by
    /// the auto-slicing postprocessor, issue 2g8.3) plus a CREDITS attribution stub.
    ///
    /// This is the volume-control step: the full LPC tree is tens of thousands of files,
    /// so we never dump it — we copy only the manifest's entries, only the listed
    /// animations (walk by default). Adding an option is a one-line manifest edit.
    ///
    /// Covers all 21 LPC categories: draw order comes from each entry's zPos (transcribed
    /// from the part's sheet_definition) or the canonical default for its category
    /// (<see cref="LpcCategory"/>); multi-layer parts are several entries on one slot, and
    /// behind-body parts (shadow/backpack/cape/quiver) get negative z. Per-body-type variants
    /// are resolved (2g8.9) and every animation is sliced per layout (2g8.7/2g8.8).
    ///
    /// Deferred by design: full per-part credit aggregation (2g8.11), automatic
    /// sheet_definitions parsing + per-direction z (2g8.15). CREDITS is still a stub listing
    /// used parts + the LPC license notice.
    /// </summary>
    public static class LpcCatalogImporter
    {
        const string DefaultManifest = "Assets/Characters/LPC/catalog_manifest.json";

        // An entry's draw order comes from its own zPos when set (transcribed from the part's
        // sheet_definition), otherwise the canonical default for its category (LpcCategory).
        // Multi-layer parts (layer_1/layer_2) are expressed as several entries on the same slot
        // with different zPos. int.MinValue means "unset -> use the category default".
        [System.Serializable]
        public class Entry
        {
            public string slot;
            public string source;
            public int zPos = int.MinValue;
        }

        [System.Serializable]
        public class Manifest
        {
            public string lpcSourcePath;   // local LPC generator clone (contains spritesheets/)
            public string destFolder;      // project-relative, e.g. Assets/Characters/LPC/Catalog
            public string bodyType;        // legacy single body type (used when bodyTypes is empty)
            public string[] bodyTypes;     // body types to import, e.g. ["male","female","child"]
            public string[] animations;    // walk-first; default ["walk"]
            public Entry[] entries;        // selection: slot + source path under spritesheets/
        }

        [System.Serializable]
        public class IndexEntry
        {
            public string slot;
            public string id;
            public string bodyType;        // body type this variant is drawn for
            public int zOrder;
            public string source;
            public string[] animations;
            public string[] files;         // project-relative copied png paths
        }

        [System.Serializable]
        public class Index
        {
            public string bodyType;
            public List<IndexEntry> entries = new List<IndexEntry>();
        }

        [MenuItem("Tools/LPC/Import Starter Catalog")]
        public static void ImportDefault() => Import(DefaultManifest);

        /// <summary>Import the catalog described by a manifest JSON at a project-relative path.</summary>
        public static void Import(string manifestPath)
        {
            if (!File.Exists(manifestPath)) { Debug.LogError($"[LPC] Manifest not found: {manifestPath}"); return; }

            var man = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
            if (man == null || man.entries == null || man.entries.Length == 0)
            { Debug.LogError("[LPC] Manifest is empty or invalid."); return; }

            string src = (man.lpcSourcePath ?? "").Replace("\\", "/").TrimEnd('/');
            string spritesheets = src + "/spritesheets";
            if (!Directory.Exists(spritesheets))
            { Debug.LogError($"[LPC] LPC source not found (expected spritesheets/ here): {spritesheets}"); return; }

            var anims = (man.animations != null && man.animations.Length > 0) ? man.animations : new[] { "walk" };
            var bodyTypes = (man.bodyTypes != null && man.bodyTypes.Length > 0) ? man.bodyTypes
                          : new[] { LpcBodyType.Normalize(man.bodyType) };
            string dest = (man.destFolder ?? "Assets/Characters/LPC/Catalog").Replace("\\", "/").TrimEnd('/');
            Directory.CreateDirectory(dest);

            var index = new Index { bodyType = man.bodyType };
            var usedParts = new List<string>();
            int copied = 0, missing = 0;

            foreach (var e in man.entries)
            {
                if (e == null || string.IsNullOrEmpty(e.slot) || string.IsNullOrEmpty(e.source)) continue;

                string srcDir = spritesheets + "/" + e.source.Trim('/');
                if (!Directory.Exists(srcDir)) { Debug.LogWarning($"[LPC] Source dir missing: {e.source}"); missing++; continue; }

                string baseId = Sanitize(e.source);
                string slotDir = dest + "/" + e.slot;
                Directory.CreateDirectory(slotDir);
                int z = (e.zPos != int.MinValue) ? e.zPos : LpcCategory.DefaultZ(e.slot);

                // LPC draws each part per body type in a <bodytype>/ subfolder. Import every
                // requested body type that has a subfolder; if none do, the source is already
                // body-resolved (legacy) and we import it once tagged with the manifest's type.
                var present = new List<string>();
                foreach (var bt in bodyTypes) if (Directory.Exists(srcDir + "/" + bt)) present.Add(bt);
                bool legacy = present.Count == 0;
                var variants = legacy ? new List<string> { LpcBodyType.Normalize(man.bodyType) } : present;

                foreach (var bt in variants)
                {
                    string useDir = legacy ? srcDir : srcDir + "/" + bt;
                    string vid = legacy ? baseId : baseId + "_" + bt;

                    var files = new List<string>();
                    var usedAnims = new List<string>();
                    foreach (var a in anims)
                    {
                        string sp = useDir + "/" + a + ".png";
                        if (!File.Exists(sp)) { missing++; continue; }
                        // walk -> "<vid>.png"; other anims -> "<vid>__<anim>.png" (vid carries the body type)
                        string fileName = (a == "walk") ? vid + ".png" : vid + "__" + a + ".png";
                        string dp = slotDir + "/" + fileName;
                        File.Copy(sp, dp, true);
                        files.Add(dp); usedAnims.Add(a); copied++;
                    }
                    if (files.Count == 0) { Debug.LogWarning($"[LPC] No listed animations found for {e.source} [{bt}]"); continue; }

                    index.entries.Add(new IndexEntry { slot = e.slot, id = vid, bodyType = bt, zOrder = z, source = e.source, animations = usedAnims.ToArray(), files = files.ToArray() });
                    usedParts.Add($"{e.slot,-8} {e.source} [{bt}]");
                }
            }

            File.WriteAllText(dest + "/catalog_index.json", JsonUtility.ToJson(index, true));
            File.WriteAllText(dest + "/CREDITS.txt", BuildCredits(usedParts));
            AssetDatabase.Refresh();
            Debug.Log($"[LPC] Catalog import complete: {index.entries.Count} parts, {copied} sheet(s) copied, {missing} missing -> {dest}");
        }

        static string Sanitize(string rel) => rel.Trim('/').Replace('/', '_').Replace('\\', '_');

        static string BuildCredits(List<string> usedParts)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("LPC ART ATTRIBUTION  (auto-generated by LPC Unity Character Creator)");
            sb.AppendLine(new string('=', 70));
            sb.AppendLine("These sprites come from the Liberated Pixel Cup (LPC) project and are");
            sb.AppendLine("licensed CC-BY-SA 3.0 / GPL 3.0 / OGA-BY 3.0. You MUST credit the original");
            sb.AppendLine("artists. Per-part authors and licenses live in the LPC sheet_definitions");
            sb.AppendLine("JSON of the generator you imported from; full aggregation is issue 2g8.11.");
            sb.AppendLine();
            sb.AppendLine("Parts used in this catalog:");
            foreach (var p in usedParts) sb.AppendLine("  - " + p);
            return sb.ToString();
        }
    }
}
