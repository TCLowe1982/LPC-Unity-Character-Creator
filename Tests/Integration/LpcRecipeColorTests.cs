using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Lpc;

namespace Lpc.Tests.Integration
{
    /// <summary>
    /// Recipes carry per-slot palette recolors (<see cref="LpcRecipe.SlotColor"/>) that
    /// <see cref="LpcCharacterBuilder.Build"/> applies across ALL clips — so a data-driven
    /// NPC (guard, merchant...) renders in its recipe's colors on every animation, not the
    /// catalog's default palette. Uses real readable textures; Unity-only.
    /// </summary>
    public class LpcRecipeColorTests
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

        Sprite[] SolidFrames(int n, Color c)
        {
            var tex = new Texture2D(2, 2); temp.Add(tex);
            tex.SetPixels(new[] { c, c, c, c }); tex.Apply();
            var arr = new Sprite[n];
            for (int i = 0; i < n; i++)
            {
                var s = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0f), 32f);
                temp.Add(s);
                arr[i] = s;
            }
            return arr;
        }

        LpcLayerSet MakeLayer(string slot, int z, Color c, params string[] clipNames)
        {
            var ls = ScriptableObject.CreateInstance<LpcLayerSet>(); temp.Add(ls);
            ls.slot = slot; ls.zOrder = z;
            var clips = new LpcClipFrames[clipNames.Length];
            for (int i = 0; i < clipNames.Length; i++)
            {
                var clip = LpcClips.Get(clipNames[i]);
                clips[i] = new LpcClipFrames { clip = clip.name, frames = SolidFrames(clip.TotalFrames, c) };
            }
            ls.clips = clips;
            return ls;
        }

        static Color TopColor(Sprite s) => s.texture.GetPixel(0, 0);

        [Test]
        public void Build_AppliesRecipeColors_OnlyToSlotsWithAnEntry()
        {
            var body = MakeLayer("body", 0, Color.red, "walk");
            var torso = MakeLayer("torso", 40, Color.red, "walk");

            var recipe = ScriptableObject.CreateInstance<LpcRecipe>(); temp.Add(recipe);
            recipe.layers = new[] { body, torso };
            recipe.colors = new[] { new LpcRecipe.SlotColor { slot = "torso", ramp = new[] { Color.blue } } };

            go = new GameObject("LPC_TEST");
            var c = LpcCharacterBuilder.Build(recipe, go);

            SpriteRenderer bodySr = null, torsoSr = null;
            foreach (var L in c.layers) { if (L.name == "body") bodySr = L.renderer; if (L.name == "torso") torsoSr = L.renderer; }

            Assert.AreEqual(Color.blue, TopColor(torsoSr.sprite), "torso wears the recipe's ramp");
            Assert.AreEqual(Color.red, TopColor(bodySr.sprite), "slots without an entry keep default colors");
            // the source layer-set asset is untouched (recolor produces new frames)
            Assert.AreEqual(Color.red, TopColor(torso.clips[0].frames[0]), "catalog asset not mutated");
        }

        [Test]
        public void Build_RecolorsAcrossAllClips_NotJustWalk()
        {
            var torso = MakeLayer("torso", 40, Color.red, "walk", "slash");

            var recipe = ScriptableObject.CreateInstance<LpcRecipe>(); temp.Add(recipe);
            recipe.layers = new[] { torso };
            recipe.colors = new[] { new LpcRecipe.SlotColor { slot = "torso", ramp = new[] { Color.blue } } };

            go = new GameObject("LPC_TEST");
            var c = LpcCharacterBuilder.Build(recipe, go);

            SpriteRenderer sr = c.layers[0].renderer;
            c.Play(LpcClips.Get("slash")); c.SetPose(2, 1);
            Assert.AreEqual(Color.blue, TopColor(sr.sprite), "the recolor holds on non-walk clips too");
        }

        [Test]
        public void Build_RecolorsLegacyWalkArray()
        {
            var body = ScriptableObject.CreateInstance<LpcLayerSet>(); temp.Add(body);
            body.slot = "body"; body.zOrder = 0;
            body.frames = SolidFrames(36, Color.red); // pre-2g8.8 import: walk-only flat array

            var recipe = ScriptableObject.CreateInstance<LpcRecipe>(); temp.Add(recipe);
            recipe.layers = new[] { body };
            recipe.colors = new[] { new LpcRecipe.SlotColor { slot = "body", ramp = new[] { Color.blue } } };

            go = new GameObject("LPC_TEST");
            var c = LpcCharacterBuilder.Build(recipe, go);

            Assert.AreEqual(Color.blue, TopColor(c.layers[0].renderer.sprite));
        }

        [Test]
        public void RampFor_ReturnsNullForUnknownSlot_AndSkipsEmptyRamps()
        {
            var recipe = ScriptableObject.CreateInstance<LpcRecipe>(); temp.Add(recipe);
            recipe.colors = new[]
            {
                new LpcRecipe.SlotColor { slot = "hair", ramp = new Color[0] },       // empty -> ignored
                new LpcRecipe.SlotColor { slot = "torso", ramp = new[] { Color.blue } },
            };
            Assert.IsNull(recipe.RampFor("legs"), "no entry -> null");
            Assert.IsNull(recipe.RampFor("hair"), "empty ramp -> null");
            Assert.IsNotNull(recipe.RampFor("torso"));
        }
    }
}
