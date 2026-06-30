using System.IO;
using System.IO.Compression;
using System.Net;
using UnityEditor;
using UnityEngine;

namespace Lpc.Editor
{
    /// <summary>
    /// Downloads the bundled LPC art Release artifact (the full spritesheet tree is ~483 MB /
    /// 144k PNGs, far too large for git) and extracts it OUTSIDE Assets/ — so Unity doesn't
    /// try to import 144k textures at once. The extract folder becomes a local LPC "source"
    /// that the selective importer (<see cref="LpcCatalogImporter"/>) reads, so you slice only
    /// the parts you actually use into the project.
    ///
    /// Art is CC-BY-SA 3.0 / GPL 3.0 / OGA-BY 3.0 — see LICENSE-ART.txt; per-file authors and
    /// licenses are in the extracted CREDITS.csv. You must credit the artists.
    /// </summary>
    public static class LpcArtImporter
    {
        // Pinned to the Release artifact. Update the tag when a new art bundle ships.
        const string ReleaseUrl =
            "https://github.com/TCLowe1982/LPC-Unity-Character-Creator/releases/download/art-v1/LpcArt-full.zip";

        const string DestFolderName = "LpcArtSource"; // sibling of Assets/, NOT imported by Unity

        [MenuItem("Tools/LPC/Import Bundled Art")]
        public static void ImportBundledArt()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName.Replace('\\', '/');
            string dest = projectRoot + "/" + DestFolderName;

            if (!EditorUtility.DisplayDialog("Import LPC Art Bundle",
                    "Download the full LPC art bundle (~483 MB) and extract it to:\n\n" + dest +
                    "\n\nIt is kept outside Assets/ so Unity won't import 144k files; the LPC importer " +
                    "then slices only the parts you select. The art is CC-BY-SA / GPL / OGA-BY — you must " +
                    "credit the artists (see CREDITS.csv / LICENSE-ART.txt).",
                    "Download (~483 MB)", "Cancel"))
                return;

            string tmpZip = Path.Combine(Path.GetTempPath(), "LpcArt-full.zip");
            try
            {
                EditorUtility.DisplayProgressBar("LPC Art", "Downloading bundle (~483 MB)…", 0.2f);
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
                using (var wc = new WebClient()) wc.DownloadFile(ReleaseUrl, tmpZip);

                EditorUtility.DisplayProgressBar("LPC Art", "Extracting…", 0.7f);
                Directory.CreateDirectory(dest);
                ZipFile.ExtractToDirectory(tmpZip, dest, overwriteFiles: true);

                PointManifestAt(dest);
                Debug.Log($"[LPC] Art bundle extracted to {dest}. The catalog manifest now points here — " +
                          "run Tools/LPC/Import Starter Catalog (or your manifest) to slice the parts you need.");
                EditorUtility.DisplayDialog("LPC Art", "Done. Extracted to:\n" + dest +
                    "\n\nNow run Tools/LPC/Import Starter Catalog to slice parts into your project.", "OK");
            }
            catch (System.Exception e)
            {
                Debug.LogError("[LPC] Art bundle import failed: " + e.Message);
                EditorUtility.DisplayDialog("LPC Art", "Import failed:\n" + e.Message, "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (File.Exists(tmpZip)) { try { File.Delete(tmpZip); } catch { } }
            }
        }

        // Update the starter manifest's lpcSourcePath to the freshly extracted bundle, so the
        // selective importer reads from it with no manual editing.
        static void PointManifestAt(string sourceRoot)
        {
            const string manifestPath = "Assets/Characters/LPC/catalog_manifest.json";
            if (!File.Exists(manifestPath)) return;
            try
            {
                var man = JsonUtility.FromJson<LpcCatalogImporter.Manifest>(File.ReadAllText(manifestPath));
                if (man == null) return;
                man.lpcSourcePath = sourceRoot;
                File.WriteAllText(manifestPath, JsonUtility.ToJson(man, true));
                AssetDatabase.Refresh();
            }
            catch { /* leave the manifest as-is if it isn't ours to rewrite */ }
        }
    }
}
