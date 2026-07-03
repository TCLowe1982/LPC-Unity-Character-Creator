using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Lpc;

namespace Lpc.Tests.Integration
{
    /// <summary>
    /// End-to-end checks that a BUILT character actually animates: real GameObjects, real
    /// SpriteRenderers, real Sprites, driven through <see cref="LpcAnimator.Tick"/>. These
    /// confirm the runtime pathway (animator -> clip selection -> LpcCharacter.SetPose ->
    /// the correct sprite on the renderer), which the pure-logic tests can't cover.
    ///
    /// Unity-only (creates Texture2D/Sprite/GameObject), so it lives under Tests/Integration
    /// where the offline dotnet bridge's non-recursive glob skips it; Unity's EditMode asmdef
    /// compiles it recursively. Frames are distinct Sprites so the renderer's current sprite
    /// maps back to an exact frame index.
    /// </summary>
    public class LpcAnimationPathwayTests
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

        // ---- fixtures -----------------------------------------------------------------

        Sprite[] MakeFrames(int n, string tag)
        {
            var arr = new Sprite[n];
            for (int i = 0; i < n; i++)
            {
                var tex = new Texture2D(4, 4); temp.Add(tex);
                var s = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0f), 32f);
                s.name = tag + "_" + i; temp.Add(s);
                arr[i] = s;
            }
            return arr;
        }

        LpcLayerSet MakeBody()
        {
            var ls = ScriptableObject.CreateInstance<LpcLayerSet>(); temp.Add(ls);
            ls.slot = "body"; ls.zOrder = 0;
            ls.frames = MakeFrames(36, "walk"); // legacy walk sheet: 9 frames x 4 dirs
            return ls;
        }

        (LpcCharacter c, LpcAnimator a, SpriteRenderer sr) Build(LpcLayerSet body)
        {
            var recipe = ScriptableObject.CreateInstance<LpcRecipe>(); temp.Add(recipe);
            recipe.layers = new[] { body };
            go = new GameObject("LPC_TEST");
            var c = LpcCharacterBuilder.Build(recipe, go);
            var a = go.AddComponent<LpcAnimator>();
            a.speedScale = 1f;
            SpriteRenderer sr = null;
            foreach (var L in c.layers) if (L.name == "body") sr = L.renderer;
            return (c, a, sr);
        }

        static int IndexOf(Sprite[] frames, Sprite s)
        {
            for (int i = 0; i < frames.Length; i++) if (frames[i] == s) return i;
            return -1;
        }

        // ---- tests --------------------------------------------------------------------

        [Test]
        public void Build_InitialPose_IsStandingFacingDown()
        {
            var body = MakeBody();
            var (_, _, sr) = Build(body);
            Assert.AreEqual(18, IndexOf(body.frames, sr.sprite)); // dir 2 (south) * 9 + frame 0
        }

        [Test]
        public void Walking_CyclesWithinDirectionRow_AndSkipsStandingFrame()
        {
            var body = MakeBody();
            var (_, a, sr) = Build(body);
            a.facing = new Vector2Int(0, -1); // south -> dir 2
            a.walking = true;

            var seen = new HashSet<int>();
            for (int i = 0; i < 16; i++) { a.Tick(0.125f); seen.Add(IndexOf(body.frames, sr.sprite)); }

            foreach (var idx in seen)
            {
                Assert.GreaterOrEqual(idx, 19, "frame should be in dir-2 walk row (19..26)");
                Assert.LessOrEqual(idx, 26, "frame should be in dir-2 walk row (19..26)");
            }
            Assert.IsFalse(seen.Contains(18), "must never show standing frame 0 while walking");
            Assert.GreaterOrEqual(seen.Count, 2, "animation must actually advance, not freeze");
        }

        [Test]
        public void Facing_SelectsCorrectDirectionRow()
        {
            var body = MakeBody();
            var (_, a, sr) = Build(body);
            a.walking = true;

            a.facing = new Vector2Int(-1, 0); // west -> dir 1
            a.Tick(0.125f);
            int west = IndexOf(body.frames, sr.sprite);
            Assert.GreaterOrEqual(west, 10); Assert.LessOrEqual(west, 17); // dir 1 row

            a.facing = new Vector2Int(1, 0);  // east -> dir 3
            a.Tick(0.125f);
            int east = IndexOf(body.frames, sr.sprite);
            Assert.GreaterOrEqual(east, 28); Assert.LessOrEqual(east, 35); // dir 3 row
        }

        [Test]
        public void LayerMissingClip_FallsBackToStandingWalkFrame()
        {
            // body has walk + jump; torso (a shirt) has only walk — like a formal shirt with no jump sheet
            var body = ScriptableObject.CreateInstance<LpcLayerSet>(); temp.Add(body);
            body.slot = "body"; body.zOrder = 0;
            var bodyWalk = MakeFrames(36, "bodywalk");
            body.frames = bodyWalk;
            body.clips = new[]
            {
                new LpcClipFrames { clip = "walk", frames = bodyWalk },
                new LpcClipFrames { clip = "jump", frames = MakeFrames(20, "bodyjump") }, // 5x4
            };

            var torso = ScriptableObject.CreateInstance<LpcLayerSet>(); temp.Add(torso);
            torso.slot = "torso"; torso.zOrder = 40;
            var torsoWalk = MakeFrames(36, "torsowalk");
            torso.frames = torsoWalk;
            torso.clips = new[] { new LpcClipFrames { clip = "walk", frames = torsoWalk } }; // no jump

            var recipe = ScriptableObject.CreateInstance<LpcRecipe>(); temp.Add(recipe);
            recipe.layers = new[] { body, torso };
            go = new GameObject("LPC_TEST");
            var c = LpcCharacterBuilder.Build(recipe, go);

            SpriteRenderer bodySr = null, torsoSr = null;
            foreach (var L in c.layers) { if (L.name == "body") bodySr = L.renderer; if (L.name == "torso") torsoSr = L.renderer; }

            c.Play(LpcClips.Walk); c.SetPose(2, 1);
            Assert.IsNotNull(bodySr.sprite); Assert.IsNotNull(torsoSr.sprite, "both visible while walking");

            c.Play(LpcClips.Get("jump")); c.SetPose(2, 2);
            Assert.IsNotNull(bodySr.sprite, "body has jump frames");
            // torso lacks jump: it must not vanish (equipment popping) NOR show a stale
            // animated pose — it holds walk frame 0 of the same direction (standing south)
            Assert.AreEqual(18, IndexOf(torsoWalk, torsoSr.sprite),
                "torso lacks jump -> holds walk standing frame (dir 2 * 9 + 0)");
        }

        [Test]
        public void LayerMissingClipAndWalk_StillHides()
        {
            var body = MakeBody();

            // a trap effect with ONLY a slash sheet: nothing sensible to stand on
            var fx = ScriptableObject.CreateInstance<LpcLayerSet>(); temp.Add(fx);
            fx.slot = "fx"; fx.zOrder = 90;
            fx.clips = new[] { new LpcClipFrames { clip = "slash", frames = MakeFrames(24, "fxslash") } };

            var recipe = ScriptableObject.CreateInstance<LpcRecipe>(); temp.Add(recipe);
            recipe.layers = new[] { body, fx };
            go = new GameObject("LPC_TEST");
            var c = LpcCharacterBuilder.Build(recipe, go);

            SpriteRenderer fxSr = null;
            foreach (var L in c.layers) if (L.name == "fx") fxSr = L.renderer;

            c.Play(LpcClips.Get("jump")); c.SetPose(2, 2);
            Assert.IsNull(fxSr.sprite, "no jump AND no walk -> hidden, not a stale pose");
        }

        [Test]
        public void OneShotOnPartialLayer_EquipmentHoldsThroughStartAndStop()
        {
            // the reported bug: a guard's longsword ships walk+slash only; standing (idle)
            // made the sword vanish. Drive the real animator through walk -> idle -> slash.
            var body = ScriptableObject.CreateInstance<LpcLayerSet>(); temp.Add(body);
            body.slot = "body"; body.zOrder = 0;
            var bodyWalk = MakeFrames(36, "bodywalk");
            body.frames = bodyWalk;
            body.clips = new[]
            {
                new LpcClipFrames { clip = "walk", frames = bodyWalk },
                new LpcClipFrames { clip = "idle", frames = MakeFrames(8, "bodyidle") }, // 2x4
            };

            var sword = ScriptableObject.CreateInstance<LpcLayerSet>(); temp.Add(sword);
            sword.slot = "weapon"; sword.zOrder = 60;
            var swordWalk = MakeFrames(36, "swordwalk");
            sword.clips = new[]
            {
                new LpcClipFrames { clip = "walk",  frames = swordWalk },
                new LpcClipFrames { clip = "slash", frames = MakeFrames(24, "swordslash") },
            };

            var recipe = ScriptableObject.CreateInstance<LpcRecipe>(); temp.Add(recipe);
            recipe.layers = new[] { body, sword };
            go = new GameObject("LPC_TEST");
            var c = LpcCharacterBuilder.Build(recipe, go);
            var a = go.AddComponent<LpcAnimator>();

            SpriteRenderer swordSr = null;
            foreach (var L in c.layers) if (L.name == "weapon") swordSr = L.renderer;

            a.facing = new Vector2Int(0, -1); // south -> dir 2
            a.walking = true;
            a.Tick(0.125f);
            Assert.IsNotNull(swordSr.sprite, "sword visible while walking");

            a.walking = false; // body idles (has idle); sword lacks idle
            a.Tick(0.125f);
            Assert.AreEqual(18, IndexOf(swordWalk, swordSr.sprite),
                "sword must hold its walk standing frame while the body idles, not vanish");

            a.PlayOnce("slash");
            a.Tick(0.01f);
            int s = IndexOf(sword.clips[1].frames, swordSr.sprite);
            Assert.GreaterOrEqual(s, 12, "sword animates its own slash frames (dir 2 row)");
            Assert.LessOrEqual(s, 17);
        }

        [Test]
        public void StopWalking_ReturnsToStandingPose()
        {
            var body = MakeBody();
            var (_, a, sr) = Build(body);
            a.facing = new Vector2Int(0, -1);
            a.walking = true;
            for (int i = 0; i < 4; i++) a.Tick(0.125f);

            a.walking = false;
            a.Tick(0.125f);
            Assert.AreEqual(18, IndexOf(body.frames, sr.sprite)); // back to standing-down
        }

        [Test]
        public void PlayOnce_PlaysOneShotThenReturnsToLocomotion()
        {
            var body = MakeBody();
            // give the body a 'slash' clip: 6 frames x 4 dirs = 24
            body.clips = new[] { new LpcClipFrames { clip = "slash", frames = MakeFrames(24, "slash") } };
            var (_, a, sr) = Build(body);
            a.facing = new Vector2Int(0, -1); // dir 2
            a.walking = false;

            a.PlayOnce("slash");
            a.Tick(0.01f);
            int s = IndexOf(body.clips[0].frames, sr.sprite);
            Assert.GreaterOrEqual(s, 12); Assert.LessOrEqual(s, 17); // dir 2 of a 6-frame clip: 12..17

            // run well past the clip's length (slash fps 12, 6 frames -> done after t>=6)
            for (int i = 0; i < 80; i++) a.Tick(0.0125f);
            Assert.AreEqual(18, IndexOf(body.frames, sr.sprite)); // returned to standing on the walk sheet
        }
    }
}
