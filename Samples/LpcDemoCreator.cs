using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Lpc.Samples
{
    /// <summary>
    /// Mini character-creation demo (2g8.12): a self-building UI panel that cycles each
    /// slot's parts, cycles body type, and recolors the hair — rebuilding a live
    /// <see cref="LpcCharacter"/> (via <see cref="LpcCharacterBuilder"/>) on every change.
    ///
    /// Data-driven: <see cref="parts"/> is wired by the demo scene builder
    /// (Tools/LPC/Create Demo Scene) from the imported catalog's index, so a multi-layer
    /// part (a weapon's fg/bg/oversize attack sheets) equips ALL its layer sets together.
    /// The panel needs no art of its own; like <see cref="LpcAnimationPreview"/> it builds
    /// its uGUI children in Start.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class LpcDemoCreator : MonoBehaviour
    {
        /// <summary>One equipable part: every LayerSet it spans (all layers x body types).</summary>
        [System.Serializable]
        public class Part
        {
            public string name;             // display name, e.g. "longsword"
            public string slot;             // primary slot, e.g. "weapon"
            public LpcLayerSet[] sets;
        }

        [Tooltip("GameObject the character is (re)built on. Created at origin if empty.")]
        public GameObject characterTarget;

        [Tooltip("Equipable parts; wired from the catalog index by Tools/LPC/Create Demo Scene.")]
        public List<Part> parts = new List<Part>();

        [Tooltip("Body types available in the imported catalog.")]
        public string[] bodyTypes = { LpcBodyType.Male };

        public int fontSize = 18;
        public Color buttonColor = new Color(0.18f, 0.18f, 0.22f, 0.95f);

        // hair recolor presets; "natural" (null) first
        static readonly Color[][] HairRamps =
        {
            null,
            new[] { new Color(0.98f, 0.85f, 0.45f), new Color(0.85f, 0.65f, 0.25f), new Color(0.6f, 0.42f, 0.12f) },  // blonde
            new[] { new Color(0.85f, 0.3f, 0.15f), new Color(0.6f, 0.18f, 0.08f), new Color(0.35f, 0.1f, 0.05f) },    // red
            new[] { new Color(0.5f, 0.65f, 0.95f), new Color(0.3f, 0.4f, 0.75f), new Color(0.15f, 0.2f, 0.5f) },      // blue
        };
        static readonly string[] HairRampNames = { "natural", "blonde", "red", "blue" };

        readonly Dictionary<string, int> picked = new Dictionary<string, int>(); // slot -> index into its parts (-1 = none)
        readonly List<string> slots = new List<string>();                         // distinct primary slots, catalog order
        readonly Dictionary<string, List<Part>> bySlot = new Dictionary<string, List<Part>>();
        readonly Dictionary<string, Text> slotLabels = new Dictionary<string, Text>();
        int bodyIdx, hairRampIdx;
        Text bodyLabel, hairLabel;
        LpcCharacter character;

        void Start()
        {
            foreach (var p in parts)
            {
                if (p == null || p.sets == null || p.sets.Length == 0) continue;
                if (!bySlot.TryGetValue(p.slot, out var list))
                {
                    list = new List<Part>(); bySlot[p.slot] = list; slots.Add(p.slot);
                }
                list.Add(p);
            }
            foreach (var s in slots) picked[s] = 0;   // wear the first variant of everything

            BuildUi();
            Rebuild();
        }

        void BuildUi()
        {
            var vlg = GetComponent<VerticalLayoutGroup>();
            if (vlg == null) vlg = gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4f;
            vlg.padding = new RectOffset(6, 6, 6, 6);
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandHeight = false;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            bodyLabel = MakeCycleRow("body type", font, () => Cycle(ref bodyIdx, bodyTypes.Length, false));
            foreach (var s in slots)
            {
                string slot = s; // capture
                bool optional = slot != "body";
                slotLabels[slot] = MakeCycleRow(slot, font, () => CycleSlot(slot, optional));
            }
            hairLabel = MakeCycleRow("hair color", font, () => Cycle(ref hairRampIdx, HairRamps.Length, false));
        }

        void Cycle(ref int idx, int count, bool allowNone)
        {
            idx = (idx + 1) % count;
            Rebuild();
        }

        void CycleSlot(string slot, bool optional)
        {
            int count = bySlot[slot].Count + (optional ? 1 : 0);   // extra step = "none"
            int cur = picked[slot] < 0 ? bySlot[slot].Count : picked[slot];
            cur = (cur + 1) % count;
            picked[slot] = cur >= bySlot[slot].Count ? -1 : cur;
            Rebuild();
        }

        /// <summary>Rebuild the character from the current picks and re-apply the hair ramp.</summary>
        public void Rebuild()
        {
            if (characterTarget == null)
            {
                characterTarget = new GameObject("LpcDemoCharacter");
                characterTarget.transform.position = Vector3.zero;
            }

            var pool = new List<LpcLayerSet>();
            foreach (var s in slots)
                if (picked[s] >= 0)
                    pool.AddRange(bySlot[s][picked[s]].sets);

            var recipe = ScriptableObject.CreateInstance<LpcRecipe>();
            recipe.bodyType = bodyTypes[bodyIdx];
            recipe.layers = pool.ToArray();

            string playing = "walk";
            var player = characterTarget.GetComponent<LpcClipPlayer>();
            if (player != null && player.Current.IsValid) playing = player.Current.name;

            character = LpcCharacterBuilder.Build(recipe, characterTarget);
            Destroy(recipe);
            if (player == null) player = characterTarget.AddComponent<LpcClipPlayer>();
            player.Play(playing);

            ApplyHairRamp();
            RefreshLabels();
        }

        void ApplyHairRamp()
        {
            var ramp = HairRamps[hairRampIdx];
            if (ramp == null || character == null || picked.TryGetValue("hair", out int h) && h < 0) return;
            // recolor from the RESOLVED hair set's original clips so ramps don't stack
            foreach (var L in character.layers)
                if (L.name == "hair")
                    character.SetLayerClips("hair", LpcRecolor.RecolorClips(L.clips, ramp));
        }

        void RefreshLabels()
        {
            bodyLabel.text = "body: " + bodyTypes[bodyIdx];
            hairLabel.text = "hair color: " + HairRampNames[hairRampIdx];
            foreach (var s in slots)
                slotLabels[s].text = s + ": " + (picked[s] < 0 ? "(none)" : bySlot[s][picked[s]].name);
        }

        Text MakeCycleRow(string label, Font font, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Row_" + label, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            go.AddComponent<LayoutElement>().preferredHeight = 34f;
            go.AddComponent<Image>().color = buttonColor;
            go.AddComponent<Button>().onClick.AddListener(onClick);

            var lgo = new GameObject("Label", typeof(RectTransform));
            lgo.transform.SetParent(go.transform, false);
            var rt = (RectTransform)lgo.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(8f, 0f); rt.offsetMax = Vector2.zero;
            var txt = lgo.AddComponent<Text>();
            txt.text = label;
            txt.font = font;
            txt.fontSize = fontSize;
            txt.alignment = TextAnchor.MiddleLeft;
            txt.color = Color.white;
            return txt;
        }
    }
}
