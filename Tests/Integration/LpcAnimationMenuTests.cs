using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Lpc;
using Lpc.Samples;

namespace Lpc.Tests.Integration
{
    /// <summary>The animation menu should generate exactly one button per clip the character
    /// actually has — so it always matches what was imported.</summary>
    public class LpcAnimationMenuTests
    {
        readonly List<Object> temp = new List<Object>();
        GameObject character, menuGo;

        [TearDown]
        public void Cleanup()
        {
            if (character != null) Object.DestroyImmediate(character);
            if (menuGo != null) Object.DestroyImmediate(menuGo);
            foreach (var o in temp) if (o != null) Object.DestroyImmediate(o);
            temp.Clear();
            character = menuGo = null;
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

        [Test]
        public void Build_MakesOneButtonPerAvailableClip()
        {
            var ls = ScriptableObject.CreateInstance<LpcLayerSet>(); temp.Add(ls);
            ls.slot = "body";
            ls.clips = new[]
            {
                new LpcClipFrames { clip = "walk", frames = Frames(36) },
                new LpcClipFrames { clip = "idle", frames = Frames(8) },
                new LpcClipFrames { clip = "slash", frames = Frames(24) },
            };
            ls.frames = ls.clips[0].frames;
            var recipe = ScriptableObject.CreateInstance<LpcRecipe>(); temp.Add(recipe);
            recipe.layers = new[] { ls };

            character = new GameObject("LPC");
            var c = LpcCharacterBuilder.Build(recipe, character);
            var player = character.AddComponent<LpcClipPlayer>();

            menuGo = new GameObject("Menu", typeof(RectTransform));
            var menu = menuGo.AddComponent<LpcAnimationMenu>();
            menu.player = player;
            menu.character = c;
            menu.Build();

            Assert.AreEqual(3, menuGo.transform.childCount, "one button per available clip");

            var names = new HashSet<string>();
            foreach (Transform t in menuGo.transform) names.Add(t.name);
            Assert.Contains("Btn_walk", new List<string>(names));
            Assert.Contains("Btn_idle", new List<string>(names));
            Assert.Contains("Btn_slash", new List<string>(names));
        }
    }
}
