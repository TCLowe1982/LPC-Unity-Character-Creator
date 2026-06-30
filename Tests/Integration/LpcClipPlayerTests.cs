using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Lpc;

namespace Lpc.Tests.Integration
{
    /// <summary>Drives LpcClipPlayer on a built character and checks it loops the selected
    /// clip within that clip's frame range, and ignores clips the character lacks.</summary>
    public class LpcClipPlayerTests
    {
        GameObject go;
        readonly List<Object> temp = new List<Object>();

        [TearDown]
        public void Cleanup()
        {
            if (go != null) Object.DestroyImmediate(go);
            foreach (var o in temp) if (o != null) Object.DestroyImmediate(o);
            temp.Clear();
            go = null;
        }

        Sprite[] Frames(int n)
        {
            var a = new Sprite[n];
            for (int i = 0; i < n; i++)
            {
                var tex = new Texture2D(2, 2); temp.Add(tex);
                a[i] = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0f), 32f); temp.Add(a[i]);
            }
            return a;
        }

        (LpcCharacter c, LpcClipPlayer p, SpriteRenderer sr, Sprite[] idle) Build()
        {
            var ls = ScriptableObject.CreateInstance<LpcLayerSet>(); temp.Add(ls);
            ls.slot = "body"; ls.bodyType = LpcBodyType.Male;
            var walk = Frames(36);
            var idle = Frames(8); // 2 frames x 4 dirs
            ls.frames = walk;
            ls.clips = new[]
            {
                new LpcClipFrames { clip = "walk", frames = walk },
                new LpcClipFrames { clip = "idle", frames = idle },
            };
            var recipe = ScriptableObject.CreateInstance<LpcRecipe>(); temp.Add(recipe);
            recipe.layers = new[] { ls };
            go = new GameObject("LPC_TEST");
            var c = LpcCharacterBuilder.Build(recipe, go);
            var p = go.AddComponent<LpcClipPlayer>();
            SpriteRenderer sr = null;
            foreach (var L in c.layers) if (L.name == "body") sr = L.renderer;
            return (c, p, sr, idle);
        }

        static int IndexOf(Sprite[] frames, Sprite s)
        {
            for (int i = 0; i < frames.Length; i++) if (frames[i] == s) return i;
            return -1;
        }

        [Test]
        public void Play_LoopsSelectedClip_WithinItsRange()
        {
            var (_, p, sr, idle) = Build();
            p.direction = 2; // south
            p.Play("idle");
            Assert.IsTrue(p.IsPlaying);
            Assert.AreEqual("idle", p.Current.name);

            var seen = new HashSet<int>();
            for (int i = 0; i < 10; i++) { p.Tick(0.5f); seen.Add(IndexOf(idle, sr.sprite)); }

            // idle is 2x4 -> south row is indices 4,5; both should appear and nothing else
            foreach (var idx in seen) { Assert.GreaterOrEqual(idx, 4); Assert.LessOrEqual(idx, 5); }
            Assert.AreEqual(2, seen.Count, "idle should cycle through both of its frames");
        }

        [Test]
        public void Play_UnavailableClip_IsIgnored()
        {
            var (_, p, _, _) = Build();
            p.Play("spellcast"); // not present on this character
            Assert.IsFalse(p.IsPlaying);
        }
    }
}
