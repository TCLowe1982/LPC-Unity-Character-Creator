using System.IO;
using NUnit.Framework;
using Lpc.Editor;

namespace Lpc.Tests.Integration
{
    /// <summary>The sheet_definition parser + z-index: multi-layer parts (a cape's fg/bg) must
    /// yield the right per-layer zPos so the importer orders them correctly (fg in front of the
    /// body, bg behind).</summary>
    public class LpcSheetDefTests
    {
        const string CapeJson =
            "{\n" +
            "  \"name\": \"Solid\",\n" +
            "  \"layer_1\": { \"zPos\": 85, \"male\": \"cape/solid/fg/\", \"female\": \"cape/solid/fg/\" },\n" +
            "  \"layer_2\": { \"zPos\": 5, \"male\": \"cape/solid/bg/\" },\n" +
            "  \"variants\": [\"black\"],\n" +
            "  \"animations\": [\"walk\"]\n" +
            "}";

        [Test]
        public void Parse_MultiLayer_ExtractsZPosAndSources()
        {
            var def = LpcSheetDefParser.Parse(CapeJson);
            Assert.AreEqual("Solid", def.name);
            Assert.AreEqual(2, def.layers.Count);

            var fg = def.layers.Find(l => l.zPos == 85);
            Assert.IsNotNull(fg, "foreground layer (zPos 85)");
            CollectionAssert.Contains(fg.sources, "cape/solid/fg"); // trailing slash trimmed

            var bg = def.layers.Find(l => l.zPos == 5);
            Assert.IsNotNull(bg, "background layer (zPos 5)");
            CollectionAssert.Contains(bg.sources, "cape/solid/bg");
        }

        [Test]
        public void Parse_CustomAnimation_IsNotASourcePath()
        {
            const string SwordJson =
                "{ \"name\": \"Longsword\", \"layer_1\": { \"zPos\": 150, " +
                "\"custom_animation\": \"slash_oversize\", " +
                "\"male\": \"weapon/sword/longsword/attack_slash/\" } }";
            var def = LpcSheetDefParser.Parse(SwordJson);
            Assert.AreEqual(1, def.layers.Count);
            Assert.AreEqual("slash_oversize", def.layers[0].customAnimation);
            CollectionAssert.AreEquivalent(
                new[] { "weapon/sword/longsword/attack_slash" }, def.layers[0].sources);
        }

        [Test]
        public void Parse_VariantsAndTypedSources_AreExtracted()
        {
            const string SwordJson =
                "{ \"name\": \"Arming\", " +
                "\"layer_1\": { \"zPos\": 140, \"male\": \"weapon/sword/arming/universal/fg/\", \"female\": \"weapon/sword/arming/universal/fg/\" }, " +
                "\"layer_2\": { \"zPos\": 9, \"male\": \"weapon/sword/arming/universal/bg/\" }, " +
                "\"variants\": [\"brass\", \"steel\", \"gold\"] }";
            var def = LpcSheetDefParser.Parse(SwordJson);

            CollectionAssert.AreEqual(new[] { "brass", "steel", "gold" }, def.variants);
            Assert.AreEqual("weapon/sword/arming/universal/fg", def.layers[0].PathFor("male"));
            Assert.AreEqual("weapon/sword/arming/universal/fg", def.layers[0].PathFor("female"));
            Assert.IsNull(def.layers[0].PathFor("skeleton"), "unlisted body type has no path");
            Assert.IsTrue(def.NeedsLayerExpansion, "multi-layer def needs expansion");
        }

        [Test]
        public void NeedsLayerExpansion_SingleLayerNoCustom_IsFalse()
        {
            const string PlainJson =
                "{ \"name\": \"Pants\", \"layer_1\": { \"zPos\": 25, \"male\": \"legs/pants/male/\" } }";
            Assert.IsFalse(LpcSheetDefParser.Parse(PlainJson).NeedsLayerExpansion);

            const string CustomJson =
                "{ \"name\": \"Spear\", \"layer_1\": { \"zPos\": 145, " +
                "\"custom_animation\": \"thrust_oversize\", \"male\": \"weapon/polearm/x/fg/\" } }";
            Assert.IsTrue(LpcSheetDefParser.Parse(CustomJson).NeedsLayerExpansion,
                "a custom-animation layer needs expansion even when single-layer");
        }

        [Test]
        public void Parse_HandlesEmptyOrMissing()
        {
            Assert.AreEqual(0, LpcSheetDefParser.Parse(null).layers.Count);
            Assert.AreEqual(0, LpcSheetDefParser.Parse("{}").layers.Count);
        }

        [Test]
        public void BuildZIndex_MapsEachSourceToItsLayerZPos()
        {
            string root = Path.Combine(Path.GetTempPath(), "lpc_sheetdef_test").Replace('\\', '/');
            string defs = root + "/sheet_definitions/torso/cape";
            Directory.CreateDirectory(defs);
            File.WriteAllText(defs + "/cape_solid.json", CapeJson);
            try
            {
                var map = LpcSheetDefIndex.BuildZIndex(root);
                Assert.AreEqual(85, map["cape/solid/fg"]); // front of body (body zPos 10)
                Assert.AreEqual(5, map["cape/solid/bg"]);  // behind body
            }
            finally { Directory.Delete(root, true); }
        }

        [Test]
        public void BuildZIndex_AlsoKeysBodyTypeStrippedPath()
        {
            const string ClipJson =
                "{ \"name\": \"Capeclip\", \"layer_1\": { \"zPos\": 90, " +
                "\"male\": \"neck/capeclip/male/\", \"female\": \"neck/capeclip/female/\" } }";
            string root = Path.Combine(Path.GetTempPath(), "lpc_sheetdef_test2").Replace('\\', '/');
            string defs = root + "/sheet_definitions/head/neck";
            Directory.CreateDirectory(defs);
            File.WriteAllText(defs + "/neck_capeclip.json", ClipJson);
            try
            {
                var map = LpcSheetDefIndex.BuildZIndex(root);
                Assert.AreEqual(90, map["neck/capeclip"]);       // body-type stripped = the importer's source key
                Assert.AreEqual(90, map["neck/capeclip/male"]);  // full def path kept too
            }
            finally { Directory.Delete(root, true); }
        }
    }
}
