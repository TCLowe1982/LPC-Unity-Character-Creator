using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Lpc;

namespace Lpc.Tests.Integration
{
    /// <summary>Coverage queries that drive the "flag incomplete parts / report what hides"
    /// UI: which clips a part supports, and which worn slots hide for a given clip.</summary>
    public class LpcCoverageTests
    {
        readonly List<Object> temp = new List<Object>();
        GameObject go;

        [TearDown]
        public void Cleanup()
        {
            if (go != null) Object.DestroyImmediate(go);
            foreach (var o in temp) if (o != null) Object.DestroyImmediate(o);
            temp.Clear();
            go = null;
        }

        LpcLayerSet Layer(string slot, params (string clip, int n)[] clips)
        {
            var ls = ScriptableObject.CreateInstance<LpcLayerSet>(); temp.Add(ls);
            ls.slot = slot;
            var arr = new LpcClipFrames[clips.Length];
            for (int i = 0; i < clips.Length; i++)
                arr[i] = new LpcClipFrames { clip = clips[i].clip, frames = new Sprite[clips[i].n] };
            ls.clips = arr;
            return ls;
        }

        [Test]
        public void LayerSet_SupportedAndMissingClips()
        {
            var ls = Layer("torso", ("walk", 36), ("jump", 20), ("slash", 24));
            var sup = ls.SupportedClips();
            CollectionAssert.AreEquivalent(new[] { "walk", "jump", "slash" }, sup);
            Assert.AreEqual(LpcClips.All.Length - 3, ls.MissingClips().Count);
            CollectionAssert.DoesNotContain(ls.MissingClips(), "walk");
        }

        [Test]
        public void Character_SlotsMissingClip_NamesTheHiddenParts()
        {
            // body animates everything; the shirt only has walk (like a limited variant)
            var body = ScriptableObject.CreateInstance<LpcLayerSet>(); temp.Add(body);
            body.slot = "body";
            var bodyClips = new LpcClipFrames[LpcClips.All.Length];
            for (int i = 0; i < bodyClips.Length; i++)
                bodyClips[i] = new LpcClipFrames { clip = LpcClips.All[i].name, frames = new Sprite[LpcClips.All[i].TotalFrames] };
            body.clips = bodyClips;

            var torso = Layer("torso", ("walk", 36));

            var recipe = ScriptableObject.CreateInstance<LpcRecipe>(); temp.Add(recipe);
            recipe.layers = new[] { body, torso };
            go = new GameObject("LPC");
            var c = LpcCharacterBuilder.Build(recipe, go);

            CollectionAssert.AreEqual(new[] { "torso" }, c.SlotsMissingClip("jump")); // only the shirt hides
            Assert.AreEqual(0, c.SlotsMissingClip("walk").Count);                      // both animate
        }
    }
}
