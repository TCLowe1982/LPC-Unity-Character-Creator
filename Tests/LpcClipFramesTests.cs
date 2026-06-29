using NUnit.Framework;
using UnityEngine;
using Lpc;

namespace Lpc.Tests
{
    public class LpcClipFramesTests
    {
        // Sprite[] are treated as opaque here: Resolve only inspects array length and
        // identity, never sprite contents, so arrays of nulls are valid fixtures.
        static Sprite[] Frames(int n) => new Sprite[n];

        [Test]
        public void Resolve_PrefersMatchingPerAnimationEntry()
        {
            var slash = Frames(24);
            var clips = new[]
            {
                new LpcClipFrames { clip = "walk",  frames = Frames(36) },
                new LpcClipFrames { clip = "slash", frames = slash },
            };
            Assert.AreSame(slash, LpcClipFrames.Resolve(clips, null, "slash"));
        }

        [Test]
        public void Resolve_FallsBackToLegacyWalkArray()
        {
            var legacy = Frames(36);
            // no per-animation clips yet (pre-2g8.8 import): walk resolves to the flat array
            Assert.AreSame(legacy, LpcClipFrames.Resolve(null, legacy, "walk"));
        }

        [Test]
        public void Resolve_NoLegacyFallbackForNonWalkClip()
        {
            var legacy = Frames(36);
            Assert.IsNull(LpcClipFrames.Resolve(null, legacy, "slash"));
        }

        [Test]
        public void Resolve_UnknownClip_ReturnsNull()
        {
            var clips = new[] { new LpcClipFrames { clip = "walk", frames = Frames(36) } };
            Assert.IsNull(LpcClipFrames.Resolve(clips, null, "spellcast"));
        }

        [Test]
        public void Resolve_IgnoresEmptyOrNullEntries()
        {
            var legacy = Frames(36);
            var clips = new[]
            {
                null,
                new LpcClipFrames { clip = "walk", frames = new Sprite[0] }, // empty -> not a match
            };
            // empty per-animation walk entry shouldn't shadow the legacy fallback
            Assert.AreSame(legacy, LpcClipFrames.Resolve(clips, legacy, "walk"));
        }
    }
}
