using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Lpc;

namespace Lpc.Tests.Integration
{
    /// <summary>RecolorClips must recolor EVERY clip (so a recolor holds across all animations,
    /// not just walk) while preserving clip names and frame counts. Uses real readable textures.</summary>
    public class LpcRecolorClipsTests
    {
        readonly List<Object> temp = new List<Object>();

        [TearDown]
        public void Cleanup()
        {
            foreach (var o in temp) if (o != null) Object.DestroyImmediate(o);
            temp.Clear();
        }

        Sprite Solid(Color c)
        {
            var tex = new Texture2D(2, 2); temp.Add(tex);
            tex.SetPixels(new[] { c, c, c, c }); tex.Apply();
            var s = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0f), 32f); temp.Add(s);
            return s;
        }

        [Test]
        public void RecolorClips_RecolorsEveryClip_PreservesStructure()
        {
            var clips = new[]
            {
                new LpcClipFrames { clip = "walk",  frames = new[] { Solid(Color.red), Solid(Color.red) } },
                new LpcClipFrames { clip = "climb", frames = new[] { Solid(Color.red) } },
            };

            var outp = LpcRecolor.RecolorClips(clips, new[] { Color.blue });

            Assert.AreEqual(2, outp.Length);
            Assert.AreEqual("walk", outp[0].clip);
            Assert.AreEqual(2, outp[0].frames.Length);
            Assert.AreEqual("climb", outp[1].clip);
            Assert.AreEqual(1, outp[1].frames.Length);
            // each clip got a fresh recolored sprite, not the original
            Assert.AreNotSame(clips[0].frames[0], outp[0].frames[0]);
            Assert.AreNotSame(clips[1].frames[0], outp[1].frames[0]);
        }

        [Test]
        public void RecolorClips_HandlesNullInput()
        {
            Assert.IsNull(LpcRecolor.RecolorClips(null, new[] { Color.blue }));
        }
    }
}
