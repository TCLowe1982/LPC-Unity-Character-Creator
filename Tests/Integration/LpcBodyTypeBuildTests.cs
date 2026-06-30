using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Lpc;

namespace Lpc.Tests.Integration
{
    /// <summary>
    /// Verifies the builder resolves a recipe's per-slot body-type variant correctly:
    /// exact match, fallback, dropping unsupported slots, and switching body type. Uses real
    /// ScriptableObjects (LpcRecipe/LpcLayerSet), so Unity-only — lives under Tests/Integration
    /// where the offline bridge's non-recursive glob skips it.
    /// </summary>
    public class LpcBodyTypeBuildTests
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

        LpcLayerSet Layer(string slot, string bodyType, int z)
        {
            var ls = ScriptableObject.CreateInstance<LpcLayerSet>(); temp.Add(ls);
            ls.slot = slot; ls.bodyType = bodyType; ls.zOrder = z;
            return ls;
        }

        LpcRecipe Recipe(string bodyType, params LpcLayerSet[] layers)
        {
            var r = ScriptableObject.CreateInstance<LpcRecipe>(); temp.Add(r);
            r.bodyType = bodyType; r.layers = layers;
            return r;
        }

        static Dictionary<string, string> BySlot(List<LpcLayerSet> chosen)
        {
            var d = new Dictionary<string, string>();
            foreach (var l in chosen) d[l.slot] = l.bodyType;
            return d;
        }

        [Test]
        public void Resolve_PicksExactBodyTypePerSlot()
        {
            var r = Recipe("female",
                Layer("body", "male", 0), Layer("body", "female", 0),
                Layer("torso", "male", 30), Layer("torso", "female", 30));

            var picked = BySlot(LpcCharacterBuilder.ResolveLayers(r));
            Assert.AreEqual("female", picked["body"]);
            Assert.AreEqual("female", picked["torso"]);
            Assert.AreEqual(2, picked.Count); // one per slot
        }

        [Test]
        public void Resolve_FallsBackWhenExactMissing()
        {
            // muscular requested, only male/female variants exist -> male (chain muscular->male)
            var r = Recipe("muscular",
                Layer("body", "male", 0), Layer("body", "female", 0));
            var picked = BySlot(LpcCharacterBuilder.ResolveLayers(r));
            Assert.AreEqual("male", picked["body"]);
        }

        [Test]
        public void Resolve_DropsSlotUnsupportedForBodyType()
        {
            // a child body exists, but the hat only ships male/female and child has no fallback
            var r = Recipe("child",
                Layer("body", "child", 0),
                Layer("hat", "male", 60), Layer("hat", "female", 60));
            var chosen = LpcCharacterBuilder.ResolveLayers(r);
            var picked = BySlot(chosen);
            Assert.IsTrue(picked.ContainsKey("body"));
            Assert.IsFalse(picked.ContainsKey("hat"), "hat unsupported for child -> dropped");
            Assert.AreEqual(1, chosen.Count);
        }

        [Test]
        public void Resolve_OrdersByZOrder()
        {
            var r = Recipe("male",
                Layer("hair", "male", 50),
                Layer("body", "male", 0),
                Layer("torso", "male", 30));
            var chosen = LpcCharacterBuilder.ResolveLayers(r);
            Assert.AreEqual("body", chosen[0].slot);
            Assert.AreEqual("torso", chosen[1].slot);
            Assert.AreEqual("hair", chosen[2].slot);
        }

        [Test]
        public void SwitchingBodyType_ReResolvesVariants()
        {
            LpcLayerSet[] pool =
            {
                Layer("body", "male", 0), Layer("body", "female", 0),
                Layer("torso", "male", 30), Layer("torso", "female", 30),
            };
            Assert.AreEqual("male", BySlot(LpcCharacterBuilder.ResolveLayers(Recipe("male", pool)))["torso"]);
            Assert.AreEqual("female", BySlot(LpcCharacterBuilder.ResolveLayers(Recipe("female", pool)))["torso"]);
        }

        [Test]
        public void Build_UsesResolvedLayerCount()
        {
            var r = Recipe("female",
                Layer("body", "male", 0), Layer("body", "female", 0),
                Layer("torso", "male", 30), Layer("torso", "female", 30));
            go = new GameObject("LPC_TEST");
            var c = LpcCharacterBuilder.Build(r, go);
            Assert.IsNotNull(c);
            Assert.AreEqual(2, c.layers.Length); // body + torso, one variant each
        }
    }
}
