using System.Collections.Generic;
using System.IO;

namespace Lpc.Editor
{
    /// <summary>
    /// Scans an LPC clone's <c>spritesheets/</c> tree on disk and turns it into selectable
    /// <see cref="LpcPartOption"/>s (pure grouping done by <see cref="LpcSourceLayout"/>). Scoped
    /// per category so the editor window can scan lazily instead of walking all 144k files.
    /// </summary>
    public static class LpcSourceScanner
    {
        /// <summary>The spritesheets/ root for a given LPC source path, or null if not found.</summary>
        public static string ResolveSpritesheets(string lpcSourcePath)
        {
            if (string.IsNullOrEmpty(lpcSourcePath)) return null;
            string root = lpcSourcePath.Replace('\\', '/').TrimEnd('/');
            string sheets = root + "/spritesheets";
            if (Directory.Exists(sheets)) return sheets;
            // allow pointing directly at a spritesheets folder too
            if (Directory.Exists(root) && Path.GetFileName(root) == "spritesheets") return root;
            return null;
        }

        /// <summary>Parts available in one category, scanned from disk. Empty if the category folder is absent.</summary>
        public static List<LpcPartOption> ScanCategory(string spritesheetsRoot, string category)
        {
            var empty = new List<LpcPartOption>();
            if (string.IsNullOrEmpty(spritesheetsRoot)) return empty;
            string root = spritesheetsRoot.Replace('\\', '/').TrimEnd('/');
            string dir = root + "/" + category;
            if (!Directory.Exists(dir)) return empty;

            var rel = new List<string>();
            foreach (var f in Directory.EnumerateFiles(dir, "*.png", SearchOption.AllDirectories))
            {
                string rp = f.Replace('\\', '/');
                if (rp.StartsWith(root + "/")) rp = rp.Substring(root.Length + 1);
                rel.Add(rp); // "<category>/.../<anim>.png"
            }
            return LpcSourceLayout.GroupParts(rel);
        }
    }
}
