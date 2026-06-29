using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Lpc.Editor
{
    /// <summary>
    /// Bakes an <see cref="LpcPalette"/> asset from an LPC palette_definitions ramp file
    /// (e.g. palette_definitions/hair/hair_lpcr.json: colour name -> 6 hex shades), so the
    /// ramps ship and load at runtime for live recolouring. The LPC source path is read from
    /// the catalog manifest. Part of issue 2g8.4.
    /// </summary>
    public static class LpcPaletteImporter
    {
        const string DefaultManifest = "Assets/Characters/LPC/catalog_manifest.json";

        [MenuItem("Tools/LPC/Import Hair Palette")]
        public static void ImportHair() =>
            Import("hair", "palette_definitions/hair/hair_lpcr.json", "Assets/Characters/LPC/Palettes/Hair.asset");

        public static void Import(string category, string relPaletteFile, string destAsset)
        {
            string src = ReadSourcePath();
            if (string.IsNullOrEmpty(src)) { Debug.LogError("[LPC] Could not resolve lpcSourcePath from manifest."); return; }

            string file = src + "/" + relPaletteFile;
            if (!File.Exists(file)) { Debug.LogError($"[LPC] Palette file not found: {file}"); return; }

            var ramps = Parse(File.ReadAllText(file));
            if (ramps.Count == 0) { Debug.LogError($"[LPC] No ramps parsed from {file}"); return; }

            Directory.CreateDirectory(Path.GetDirectoryName(destAsset));
            var pal = AssetDatabase.LoadAssetAtPath<LpcPalette>(destAsset);
            bool isNew = pal == null;
            if (isNew) pal = ScriptableObject.CreateInstance<LpcPalette>();
            pal.category = category;
            pal.ramps = ramps.ToArray();
            if (isNew) AssetDatabase.CreateAsset(pal, destAsset); else EditorUtility.SetDirty(pal);
            AssetDatabase.SaveAssets();
            Debug.Log($"[LPC] Palette '{category}': {ramps.Count} ramps -> {destAsset}");
        }

        static string ReadSourcePath()
        {
            if (!File.Exists(DefaultManifest)) return null;
            var man = JsonUtility.FromJson<LpcCatalogImporter.Manifest>(File.ReadAllText(DefaultManifest));
            return man != null ? (man.lpcSourcePath ?? "").Replace("\\", "/").TrimEnd('/') : null;
        }

        // palette JSON shape: { "name": ["#rrggbb", ... ], ... }
        static List<LpcPalette.Ramp> Parse(string json)
        {
            var ramps = new List<LpcPalette.Ramp>();
            var entry = new Regex("\"([A-Za-z0-9_]+)\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.Singleline);
            var hex = new Regex("#([0-9A-Fa-f]{6})");
            foreach (Match m in entry.Matches(json))
            {
                string name = m.Groups[1].Value;
                var cols = new List<Color>();
                foreach (Match h in hex.Matches(m.Groups[2].Value))
                    if (ColorUtility.TryParseHtmlString("#" + h.Groups[1].Value, out var c)) cols.Add(c);
                if (cols.Count > 0) ramps.Add(new LpcPalette.Ramp { name = name, colors = cols.ToArray() });
            }
            return ramps;
        }
    }
}
