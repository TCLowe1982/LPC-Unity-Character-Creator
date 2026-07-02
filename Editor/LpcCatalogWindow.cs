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

        // ---- live preview state (2g8.22): drawn straight from the SOURCE sheets, so the
        //      combination can be seen before anything is imported --------------------------
        string previewAnim = "walk";
        int previewDir = 2;               // face south
        bool previewPlay = true;
        int previewBodyIdx;               // index into LpcBodyType.All
        int lastPreviewFrame = -1;
        readonly Dictionary<string, Texture2D> texCache = new Dictionary<string, Texture2D>();
        Dictionary<string, int> zIndex;   // source -> zPos from sheet_definitions (lazy)

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
            EditorApplication.update += OnPreviewTick;
        }

        void OnDisable()
        {
            EditorApplication.update -= OnPreviewTick;
            ClearTexCache();
        }

        // repaint only when the shown frame advances, at the previewed clip's own fps
        void OnPreviewTick()
        {
            if (!previewPlay || selected.Count == 0) return;
            var clip = LpcClips.Get(previewAnim);
            int f = LpcPreviewMath.FrameAt(EditorApplication.timeSinceStartup, clip.fps, clip.framesPerDir);
            if (f != lastPreviewFrame) { lastPreviewFrame = f; Repaint(); }
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

            DrawPreview();

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
            zIndex = null; ClearTexCache();
            status = sheetsRoot != null ? "Found spritesheets/. Expand a category to browse parts." : "spritesheets/ not found at that path.";
        }

        // ---- live preview (2g8.22) ------------------------------------------------------

        void DrawPreview()
        {
            if (selected.Count == 0) return;
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();

            // the character sheet: every selected part's frame layered by zPos, all pivots
            // anchored at one point — the same composition rule the runtime uses
            var box = GUILayoutUtility.GetRect(200f, 210f, GUILayout.Width(200f));
            EditorGUI.DrawRect(box, new Color(0.15f, 0.15f, 0.18f, 1f));
            var clip = LpcClips.Get(previewAnim);
            int frame = previewPlay
                ? LpcPreviewMath.FrameAt(EditorApplication.timeSinceStartup, clip.fps, clip.framesPerDir)
                : 0;
            string previewBody = LpcBodyType.All[Mathf.Clamp(previewBodyIdx, 0, LpcBodyType.All.Length - 1)];

            GUI.BeginGroup(box);   // clip oversize cells to the box
            foreach (var part in SelectionByZ())
            {
                string sheet = ResolveSheet(part, previewBody, previewAnim);
                if (sheet == null) continue;                       // part has no art for this clip: hide
                var tex = LoadTex(sheet);
                if (tex == null) continue;
                if (!LpcSliceMath.TryCellSize(tex.width, tex.height, clip.framesPerDir, clip.directions, out int cw, out int ch2)) continue;
                int dir = clip.directions == 1 ? 0 : previewDir;
                var uv = LpcPreviewMath.FrameUV(clip.framesPerDir, clip.directions, dir, frame);
                var dst = LpcPreviewMath.DestRect(cw, ch2, 2f, box.width / 2f, box.height - 36f);
                GUI.DrawTextureWithTexCoords(dst, tex, uv);
            }
            GUI.EndGroup();

            // controls
            EditorGUILayout.BeginVertical();
            var clipNames = new string[LpcClips.All.Length];
            int clipIdx = 0;
            for (int i = 0; i < LpcClips.All.Length; i++)
            {
                clipNames[i] = LpcClips.All[i].name;
                if (clipNames[i] == previewAnim) clipIdx = i;
            }
            int newClip = EditorGUILayout.Popup("Animation", clipIdx, clipNames);
            if (clipNames[newClip] != previewAnim) { previewAnim = clipNames[newClip]; lastPreviewFrame = -1; }

            previewBodyIdx = EditorGUILayout.Popup("Body type", previewBodyIdx, LpcBodyType.All);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Facing");
            var dirNames = new[] { "N", "W", "S", "E" };
            for (int d = 0; d < 4; d++)
                if (GUILayout.Toggle(previewDir == d, dirNames[d], "Button", GUILayout.Width(32f)) && previewDir != d)
                    previewDir = d;
            EditorGUILayout.EndHorizontal();

            previewPlay = EditorGUILayout.ToggleLeft("Animate", previewPlay);
            EditorGUILayout.LabelField("Preview reads the source sheets directly —", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("nothing is imported until you press Import.", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>Selected parts ordered back-to-front by their sheet_definition zPos
        /// (category default when a part has no definition).</summary>
        List<LpcPartOption> SelectionByZ()
        {
            if (zIndex == null) zIndex = LpcSheetDefIndex.BuildZIndex(sourcePath);
            var parts = new List<LpcPartOption>(selectedOpt.Values);
            parts.Sort((a, b) =>
            {
                int za = zIndex.TryGetValue(a.source, out var ia) ? ia : LpcCategory.DefaultZ(a.category);
                int zb = zIndex.TryGetValue(b.source, out var ib) ? ib : LpcCategory.DefaultZ(b.category);
                return za != zb ? za.CompareTo(zb) : string.CompareOrdinal(a.source, b.source);
            });
            return parts;
        }

        /// <summary>Locate the part's source sheet for an animation, resolving the preview
        /// body type against the variants the part actually has (with fallback).</summary>
        string ResolveSheet(LpcPartOption part, string previewBody, string anim)
        {
            string dir = sheetsRoot + "/" + part.source;
            if (part.bodyTypes.Count > 0)
            {
                string bt = LpcBodyType.Resolve(previewBody, part.bodyTypes) ?? part.bodyTypes[0];
                dir += "/" + bt;
            }
            string variant = part.source.Substring(part.source.LastIndexOf('/') + 1);
            return LpcCatalogImporter.FindAnimSheet(dir, anim, variant);
        }

        Texture2D LoadTex(string path)
        {
            if (texCache.TryGetValue(path, out var cached) && cached != null) return cached;
            try
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                if (!tex.LoadImage(File.ReadAllBytes(path))) { Object.DestroyImmediate(tex); return null; }
                texCache[path] = tex;
                return tex;
            }
            catch { return null; }
        }

        void ClearTexCache()
        {
            foreach (var t in texCache.Values) if (t != null) Object.DestroyImmediate(t);
            texCache.Clear();
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
