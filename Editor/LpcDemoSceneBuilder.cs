using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Lpc.Samples;

namespace Lpc.Editor
{
    /// <summary>
    /// Generates the demo / mini character-creation scene (2g8.12) from whatever the
    /// project has imported: camera + canvas + a live character with an
    /// <see cref="LpcDemoCreator"/> panel (slot cycling, body type, hair recolor) and the
    /// <see cref="LpcAnimationPreview"/> panel (animation buttons + coverage flags).
    ///
    /// The scene is BUILT rather than shipped because the package carries no art: parts
    /// come from the imported catalog's index (so a multi-layer weapon equips all its
    /// layer sets together), which exists only after Tools/LPC/Import Starter Catalog.
    /// </summary>
    public static class LpcDemoSceneBuilder
    {
        const string ScenePath = "Assets/LpcDemo/LpcCharacterCreationDemo.unity";

        [MenuItem("Tools/LPC/Create Demo Scene")]
        public static void Create()
        {
            var parts = CollectParts(out var bodyTypes);
            if (parts.Count == 0)
            {
                EditorUtility.DisplayDialog("LPC Demo Scene",
                    "No imported LPC catalog found.\n\nRun Tools/LPC/Import Bundled Art (or point at an LPC clone) " +
                    "and Tools/LPC/Import Starter Catalog first — the demo builds from the imported LayerSets.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 2.2f;
            cam.transform.position = new Vector3(0f, 1.2f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.16f, 0.2f, 1f);

            var charGo = new GameObject("LpcDemoCharacter");
            charGo.transform.position = Vector3.zero;

            // canvas + event system
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                           typeof(UnityEngine.EventSystems.StandaloneInputModule));

            // left: the character-creation panel
            var creatorGo = Panel(canvasGo.transform, "CreationPanel", 0f, 24f, 300f);
            var creator = creatorGo.AddComponent<LpcDemoCreator>();
            creator.characterTarget = charGo;
            creator.parts = parts;
            creator.bodyTypes = bodyTypes;

            // right: the animation preview (a dev tool, but the demo is exactly where it belongs)
            var previewGo = Panel(canvasGo.transform, "Preview", 1f, -24f, 240f);
            var preview = previewGo.AddComponent<LpcAnimationPreview>();
            preview.startHidden = false;
            preview.player = charGo.AddComponent<LpcClipPlayer>();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath));
            Debug.Log($"[LPC] Demo scene created: {ScenePath} ({parts.Count} parts, {bodyTypes.Length} body type(s)). Press Play.");
        }

        static GameObject Panel(Transform canvas, string name, float anchorX, float offsetX, float width)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(anchorX, 0f);
            rt.anchorMax = new Vector2(anchorX, 1f);
            rt.pivot = new Vector2(anchorX, 0.5f);
            rt.anchoredPosition = new Vector2(offsetX, 0f);
            rt.sizeDelta = new Vector2(width, -60f);
            go.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            return go;
        }

        /// <summary>
        /// Group every imported catalog index's entries by SOURCE into equipable parts, so
        /// multi-layer parts carry all their layer sets (fg/bg/oversize) as one unit.
        /// </summary>
        static List<LpcDemoCreator.Part> CollectParts(out string[] bodyTypes)
        {
            var parts = new List<LpcDemoCreator.Part>();
            var bodies = new List<string>();
            foreach (var indexFile in Directory.GetFiles(Application.dataPath, "catalog_index.json", SearchOption.AllDirectories))
            {
                string root = "Assets" + Path.GetDirectoryName(indexFile).Substring(Application.dataPath.Length).Replace('\\', '/');
                LpcCatalogImporter.Index idx;
                try { idx = JsonUtility.FromJson<LpcCatalogImporter.Index>(File.ReadAllText(indexFile)); }
                catch { continue; }
                if (idx == null || idx.entries == null) continue;

                var bySource = new Dictionary<string, LpcDemoCreator.Part>();
                foreach (var e in idx.entries)
                {
                    // group def-expanded layers under their part: source minus any layer subpath
                    string key = PartKey(e.source);
                    if (!bySource.TryGetValue(key, out var part))
                    {
                        part = new LpcDemoCreator.Part
                        {
                            name = key.Substring(key.LastIndexOf('/') + 1),
                            slot = e.slot,
                            sets = new LpcLayerSet[0],
                        };
                        bySource[key] = part;
                        parts.Add(part);
                    }
                    // the primary layer's slot names the part (sub-slots look like "weapon_l4")
                    if (e.slot.Length < part.slot.Length) part.slot = e.slot;

                    var ls = AssetDatabase.LoadAssetAtPath<LpcLayerSet>(root + "/LayerSets/" + e.slot + "_" + e.id + ".asset");
                    if (ls == null) continue;
                    var grown = new List<LpcLayerSet>(part.sets) { ls };
                    part.sets = grown.ToArray();

                    if (!string.IsNullOrEmpty(e.bodyType) && !bodies.Contains(e.bodyType)) bodies.Add(e.bodyType);
                }
            }
            if (bodies.Count == 0) bodies.Add(LpcBodyType.Male);
            bodyTypes = bodies.ToArray();
            parts.RemoveAll(p => p.sets.Length == 0);
            return parts;
        }

        // def-expanded entries share the part folder: "weapon/sword/longsword/attack_slash"
        // and "weapon/sword/longsword" are one part; body-type subfolders are already
        // excluded from entry sources by the importer.
        static string PartKey(string source)
        {
            string s = (source ?? "").Replace('\\', '/').Trim().TrimEnd('/');
            foreach (var marker in new[] { "/attack_", "/universal", "/foreground", "/background", "/behind" })
            {
                int i = s.IndexOf(marker, System.StringComparison.Ordinal);
                if (i > 0) s = s.Substring(0, i);
            }
            return s;
        }
    }
}
