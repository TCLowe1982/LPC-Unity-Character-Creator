using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Lpc.Samples
{
    /// <summary>
    /// Animation PREVIEW panel (was LpcAnimationMenu): a vertical column of buttons — one per
    /// animation the target character has — that play that clip on an
    /// <see cref="LpcClipPlayer"/>. Drop it on a UI panel (e.g. left of a character-preview
    /// frame) and assign the player; it populates itself from the character's available clips
    /// and re-syncs on part swaps.
    ///
    /// This is a package-dev testing tool, so it stays HIDDEN by default in consuming
    /// projects: with <see cref="startHidden"/> (the default) the panel deactivates itself on
    /// Start and only appears when explicitly opened via <see cref="Show"/>/<see cref="Toggle"/>
    /// (it builds itself on activation). Untick startHidden for a dev/preview scene.
    ///
    /// Coverage transparency: an animation that some worn part can't draw (e.g. the formal
    /// shirt has no jump sheet) is shown but FLAGGED (amber + "*"), and clicking it reports
    /// which worn parts animate and which hold their walk standing frame (or hide, if they
    /// lack walk too) — so nothing silently degrades.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class LpcAnimationPreview : MonoBehaviour
    {
        [Tooltip("Preview driver the buttons control.")]
        public LpcClipPlayer player;

        [Tooltip("Character whose available clips drive the menu. Defaults to the player's character.")]
        public LpcCharacter character;

        [Tooltip("Dev/preview tool: keep hidden in consuming projects until Show()/Toggle() is called. Untick for a dev scene that wants the panel open on play.")]
        public bool startHidden = true;

        public float buttonHeight = 40f;
        public int fontSize = 20;
        public Color buttonColor = new Color(0.18f, 0.18f, 0.22f, 0.95f);
        public Color flagColor = new Color(1f, 0.82f, 0.4f);   // amber: incomplete coverage

        string _signature;
        Text status;

        void Start()
        {
            if (startHidden) { gameObject.SetActive(false); return; }
            Build();
        }

        /// <summary>Open the preview panel (it builds/re-syncs itself on activation).</summary>
        public void Show() => gameObject.SetActive(true);

        /// <summary>Hide the preview panel.</summary>
        public void Hide() => gameObject.SetActive(false);

        /// <summary>Toggle the preview panel.</summary>
        public void Toggle() => gameObject.SetActive(!gameObject.activeSelf);

        // Rebuild when the set of available clips changes — covers the character being built
        // after this menu's Start (script-order races) and live part swaps changing coverage.
        void Update()
        {
            var src = Source();
            if (src != null && Signature(src) != _signature) Build();
        }

        LpcCharacter Source() =>
            character != null ? character : (player != null ? player.GetComponent<LpcCharacter>() : null);

        // signature includes per-clip missing-slot counts, so it also rebuilds when coverage
        // changes (e.g. swapping a full shirt for one that lacks jump) even if the clip stays available.
        static string Signature(LpcCharacter c)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var clip in LpcClips.All)
                if (c.HasClip(clip.name)) sb.Append(clip.name).Append(':').Append(c.SlotsMissingClip(clip.name).Count).Append(',');
            return sb.ToString();
        }

        /// <summary>(Re)build the button list from the character's available clips.</summary>
        public void Build()
        {
            var src = Source();
            _signature = src != null ? Signature(src) : null;

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var ch = transform.GetChild(i).gameObject;
                ch.transform.SetParent(null); // detach now so a rebuild's count is right (Destroy is deferred in play)
                if (Application.isPlaying) Destroy(ch); else DestroyImmediate(ch);
            }

            var vlg = GetComponent<VerticalLayoutGroup>();
            if (vlg == null) vlg = gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4f;
            vlg.padding = new RectOffset(6, 6, 6, 6);
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandHeight = false;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            foreach (var clip in LpcClips.All)
            {
                if (src != null && !src.HasClip(clip.name)) continue;
                MakeButton(clip.name, font, src);
            }
            status = MakeStatus(font);
        }

        void MakeButton(string clipName, Font font, LpcCharacter src)
        {
            int missing = src != null ? src.SlotsMissingClip(clipName).Count : 0;

            var go = new GameObject("Btn_" + clipName, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            go.AddComponent<LayoutElement>().preferredHeight = buttonHeight;
            go.AddComponent<Image>().color = buttonColor;

            var btn = go.AddComponent<Button>();
            string name = clipName; // capture per button
            btn.onClick.AddListener(() => { if (player != null) player.Play(name); ShowStatus(name); });

            var label = new GameObject("Label", typeof(RectTransform));
            label.transform.SetParent(go.transform, false);
            var rt = (RectTransform)label.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var txt = label.AddComponent<Text>();
            txt.text = missing > 0 ? clipName + "  *" : clipName; // flag incomplete coverage
            txt.font = font;
            txt.fontSize = fontSize;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = missing > 0 ? flagColor : Color.white;
        }

        Text MakeStatus(Font font)
        {
            var go = new GameObject("Status", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            go.AddComponent<LayoutElement>().preferredHeight = 84f;
            var txt = go.AddComponent<Text>();
            txt.font = font;
            txt.fontSize = Mathf.Max(12, fontSize - 6);
            txt.alignment = TextAnchor.UpperLeft;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Truncate;
            txt.color = new Color(0.86f, 0.86f, 0.92f);
            txt.text = "Pick an animation. * = some worn part has no art for it.";
            return txt;
        }

        /// <summary>Report which worn parts will and won't animate for the chosen clip.</summary>
        void ShowStatus(string clipName)
        {
            if (status == null) return;
            var src = Source();
            if (src == null) { status.text = ""; return; }
            var missing = src.SlotsMissingClip(clipName);
            if (missing.Count == 0)
            {
                status.text = "\"" + clipName + "\": all worn parts animate.";
                status.color = new Color(0.7f, 0.95f, 0.7f);
            }
            else
            {
                status.text = "\"" + clipName + "\": no " + clipName + " art (holds standing frame): " + string.Join(", ", missing.ToArray()) + ".";
                status.color = flagColor;
            }
        }
    }
}
