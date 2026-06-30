using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Lpc.Editor
{
    /// <summary>
    /// Point-and-pick catalog builder (Tools/LPC/Catalog Window). Set the LPC source path,
    /// pick body types + animations, browse each category and check the parts you want, then
    /// "Write Manifest &amp; Import" — it writes catalog_manifest.json and runs
    /// <see cref="LpcCatalogImporter"/> (which copies + auto-slices into LpcLayerSets).
    /// Categories scan lazily (on expand) so it doesn't walk all 144k files up front.
    /// </summary>
    public class LpcCatalogWindow : EditorWindow
    {
        const string ManifestPath = "Assets/Characters/LPC/catalog_manifest.json";
        const string DestFolder = "Assets/Characters/LPC/Catalog";

        string sourcePath = "";
        string sheetsRoot;
        string status = "";

        readonly HashSet<string> bodyTypes = new HashSet<string> { LpcBodyType.Male };
        readonly HashSet<string> animations = new HashSet<string> { "walk" };
        bool animFoldout;

        readonly Dictionary<string, List<LpcPartOption>> scanned = new Dictionary<string, List<LpcPartOption>>();
        readonly Dictionary<string, bool> catFoldout = new Dictionary<string, bool>();
        readonly HashSet<string> selected = new HashSet<string>();
        readonly Dictionary<string, LpcPartOption> selectedOpt = new Dictionary<string, LpcPartOption>();
        Vector2 scroll;

        [MenuItem("Tools/LPC/Catalog Window")]
        public static void Open() => GetWindow<LpcCatalogWindow>("LPC Catalog");

        void OnEnable()
        {
            if (File.Exists(ManifestPath))
            {
                try
                {
                    var man = JsonUtility.FromJson<LpcCatalogImporter.Manifest>(File.ReadAllText(ManifestPath));
                    if (man != null && !string.IsNullOrEmpty(man.lpcSourcePath)) sourcePath = man.lpcSourcePath;
                }
                catch { /* ignore a malformed manifest */ }
            }
            sheetsRoot = LpcSourceScanner.ResolveSpritesheets(sourcePath);
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("LPC Catalog Builder", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            sourcePath = EditorGUILayout.TextField("LPC source", sourcePath);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                var p = EditorUtility.OpenFolderPanel("LPC clone (contains spritesheets/)", sourcePath, "");
                if (!string.IsNullOrEmpty(p)) { sourcePath = p; Rescan(); }
            }
            if (GUILayout.Button("Scan", GUILayout.Width(50))) Rescan();
            EditorGUILayout.EndHorizontal();

            if (sheetsRoot == null)
            {
                EditorGUILayout.HelpBox("Point at an LPC clone (a folder containing spritesheets/) and press Scan. " +
                    "Tip: Tools/LPC/Import Bundled Art downloads + extracts one.", MessageType.Info);
                DrawStatus();
                return;
            }

            EditorGUILayout.LabelField("Body types", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            foreach (var bt in LpcBodyType.All) Toggle(bodyTypes, bt, bt, 78);
            EditorGUILayout.EndHorizontal();

            animFoldout = EditorGUILayout.Foldout(animFoldout, $"Animations  ({animations.Count}/{LpcClips.All.Length})");
            if (animFoldout)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("All", GUILayout.Width(40))) { animations.Clear(); foreach (var c in LpcClips.All) animations.Add(c.name); }
                if (GUILayout.Button("Walk only", GUILayout.Width(80))) { animations.Clear(); animations.Add("walk"); }
                EditorGUILayout.EndHorizontal();
                int col = 0; EditorGUILayout.BeginHorizontal();
                foreach (var c in LpcClips.All)
                {
                    Toggle(animations, c.name, c.name, 92);
                    if (++col % 4 == 0) { EditorGUILayout.EndHorizontal(); EditorGUILayout.BeginHorizontal(); }
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var cat in LpcCategory.All)
            {
                catFoldout.TryGetValue(cat, out bool open);
                bool nowOpen = EditorGUILayout.Foldout(open, cat);
                if (nowOpen != open)
                {
                    catFoldout[cat] = nowOpen;
                    if (nowOpen && !scanned.ContainsKey(cat))
                    {
                        EditorUtility.DisplayProgressBar("LPC Catalog", "Scanning " + cat + "…", 0.5f);
                        try { scanned[cat] = LpcSourceScanner.ScanCategory(sheetsRoot, cat); }
                        finally { EditorUtility.ClearProgressBar(); }
                    }
                }
                if (nowOpen && scanned.TryGetValue(cat, out var parts))
                {
                    EditorGUI.indentLevel++;
                    if (parts.Count == 0) EditorGUILayout.LabelField("(none)");
                    foreach (var part in parts)
                    {
                        bool on = selected.Contains(part.source);
                        string bt = part.bodyTypes.Count > 0 ? part.bodyTypes.Count + " body" : "any-body";
                        string label = $"{part.source.Substring(cat.Length).TrimStart('/')}   [{bt}, {part.animations.Count} anims]";
                        bool now = EditorGUILayout.ToggleLeft(label, on);
                        if (now != on)
                        {
                            if (now) { selected.Add(part.source); selectedOpt[part.source] = part; }
                            else { selected.Remove(part.source); selectedOpt.Remove(part.source); }
                        }
                    }
                    EditorGUI.indentLevel--;
                }
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"{selected.Count} parts selected", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(selected.Count == 0 || bodyTypes.Count == 0 || animations.Count == 0))
                if (GUILayout.Button("Write Manifest & Import")) WriteAndImport();
            if (GUILayout.Button("Clear", GUILayout.Width(60))) { selected.Clear(); selectedOpt.Clear(); }
            EditorGUILayout.EndHorizontal();
            DrawStatus();
        }

        static void Toggle(HashSet<string> set, string key, string label, float width)
        {
            bool on = set.Contains(key);
            bool now = GUILayout.Toggle(on, label, "Button", GUILayout.Width(width));
            if (now != on) { if (now) set.Add(key); else set.Remove(key); }
        }

        void Rescan()
        {
            sheetsRoot = LpcSourceScanner.ResolveSpritesheets(sourcePath);
            scanned.Clear(); catFoldout.Clear();
            status = sheetsRoot != null ? "Found spritesheets/. Expand a category to browse parts." : "spritesheets/ not found at that path.";
        }

        void DrawStatus()
        {
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.None);
        }

        void WriteAndImport()
        {
            var man = new LpcCatalogImporter.Manifest
            {
                lpcSourcePath = sourcePath,
                destFolder = DestFolder,
                bodyTypes = new List<string>(bodyTypes).ToArray(),
                animations = new List<string>(animations).ToArray(),
            };
            var entries = new List<LpcCatalogImporter.Entry>();
            foreach (var src in selected)
                entries.Add(new LpcCatalogImporter.Entry { slot = selectedOpt[src].category, source = src });
            man.entries = entries.ToArray();

            Directory.CreateDirectory(Path.GetDirectoryName(ManifestPath));
            File.WriteAllText(ManifestPath, JsonUtility.ToJson(man, true));
            AssetDatabase.Refresh();
            LpcCatalogImporter.Import(ManifestPath);
            status = $"Imported {entries.Count} part(s) × {bodyTypes.Count} body type(s) × {animations.Count} animation(s) → {DestFolder}";
        }
    }
}
