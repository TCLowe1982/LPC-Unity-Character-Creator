using UnityEngine;
using UnityEngine.UI;

namespace Lpc.Samples
{
    /// <summary>
    /// Builds a vertical column of buttons — one per animation the target character actually
    /// has — that play that clip on an <see cref="LpcClipPlayer"/>. Drop it on a UI panel (e.g.
    /// to the left of a character-preview frame) and assign the player; the menu populates
    /// itself from the character's available clips, so it always matches what was imported.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class LpcAnimationMenu : MonoBehaviour
    {
        [Tooltip("Preview driver the buttons control.")]
        public LpcClipPlayer player;

        [Tooltip("Character whose available clips drive the menu. Defaults to the player's character.")]
        public LpcCharacter character;

        public float buttonHeight = 40f;
        public int fontSize = 20;
        public Color buttonColor = new Color(0.18f, 0.18f, 0.22f, 0.95f);

        void Start()
        {
            if (player != null) Build();
        }

        /// <summary>(Re)build the button list from the character's available clips.</summary>
        public void Build()
        {
            var src = character != null ? character : (player != null ? player.GetComponent<LpcCharacter>() : null);

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var ch = transform.GetChild(i).gameObject;
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
                MakeButton(clip.name, font);
            }
        }

        void MakeButton(string clipName, Font font)
        {
            var go = new GameObject("Btn_" + clipName, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            go.AddComponent<LayoutElement>().preferredHeight = buttonHeight;
            go.AddComponent<Image>().color = buttonColor;

            var btn = go.AddComponent<Button>();
            string name = clipName; // capture per button
            btn.onClick.AddListener(() => { if (player != null) player.Play(name); });

            var label = new GameObject("Label", typeof(RectTransform));
            label.transform.SetParent(go.transform, false);
            var rt = (RectTransform)label.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var txt = label.AddComponent<Text>();
            txt.text = clipName;
            txt.font = font;
            txt.fontSize = fontSize;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
        }
    }
}
