// Minimal UnityEngine shim for the OFFLINE test bridge only.
//
// This folder is named "Tests~" so the Unity editor ignores it entirely (trailing '~') —
// the real UnityEngine is used inside Unity; this stub is used only by `dotnet test` so the
// pure LPC clip logic (LpcClip / LpcClipMath / LpcClipFrames) can be exercised without the
// editor. It defines ONLY the handful of symbols those files touch. If a linked runtime file
// starts using a new UnityEngine type, add the minimal stub here.

using System;

namespace UnityEngine
{
    /// <summary>Marker stub. The clip logic treats sprites as opaque array elements.</summary>
    public class Sprite { }

    /// <summary>No-op stand-in for UnityEngine.TooltipAttribute (inspector metadata).</summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class TooltipAttribute : Attribute
    {
        public TooltipAttribute(string tooltip) { }
    }

    /// <summary>Minimal Rect for the preview math (x/y/width/height only).</summary>
    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float width, float height)
        { this.x = x; this.y = y; this.width = width; this.height = height; }
    }

    /// <summary>The integer/float helpers the clip math relies on.</summary>
    public static class Mathf
    {
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Max(float a, float b) => a > b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
        public static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);
        public static int FloorToInt(float f) => (int)Math.Floor(f);
    }
}
