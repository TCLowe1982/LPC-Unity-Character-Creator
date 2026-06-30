using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Lpc.Editor
{
    /// <summary>
    /// Best-effort reader of LPC attribution from a local generator clone, producing
    /// <see cref="LpcCreditEntry"/> records the importer aggregates via <see cref="LpcCredits"/>.
    ///
    /// The canonical ULPC artifact is a root CREDITS.csv keyed by sprite file path; we match
    /// rows whose path is under a part's source and union their authors/licenses/urls. Column
    /// positions are detected from the header (tolerant of ordering/naming), and parsing is
    /// quote-aware. Manifest-supplied credit fields always override/augment what's read, and
    /// anything unresolved degrades to a part-only entry with a note — the importer never fails
    /// for want of credits. (Schema/locations vary across clones; this is unverified against a
    /// specific clone here — see 2g8.15 for sheet_definitions-embedded credits.)
    /// </summary>
    public static class LpcCreditsReader
    {
        // cache parsed CSV per source root so we parse once per import
        static string _cacheRoot;
        static List<Row> _rows;

        struct Row { public string path, authors, licenses, urls, notes; }

        public static LpcCreditEntry ReadFor(string lpcSourcePath, string source,
            string[] ovAuthors, string[] ovLicenses, string[] ovUrls, string ovNotes)
        {
            var authors = new List<string>();
            var licenses = new List<string>();
            var urls = new List<string>();
            var notes = new List<string>();

            string src = Norm(source);
            foreach (var r in Rows(lpcSourcePath))
            {
                if (string.IsNullOrEmpty(r.path)) continue;
                string p = Norm(r.path);
                if (!(p.StartsWith(src) || p.Contains(src))) continue;
                AddSplit(authors, r.authors);
                AddSplit(licenses, r.licenses);
                AddSplit(urls, r.urls);
                if (!string.IsNullOrEmpty(r.notes)) notes.Add(r.notes.Trim());
            }

            // manifest overrides augment (so an author can fill gaps or correct the CSV)
            AddRange(authors, ovAuthors);
            AddRange(licenses, ovLicenses);
            AddRange(urls, ovUrls);

            string note = !string.IsNullOrEmpty(ovNotes) ? ovNotes
                        : notes.Count > 0 ? string.Join("; ", notes)
                        : (authors.Count == 0 && licenses.Count == 0) ? "credits not found in CREDITS.csv — verify in the LPC source" : null;

            return new LpcCreditEntry
            {
                part = source,
                authors = Dedupe(authors),
                licenses = Dedupe(licenses),
                urls = Dedupe(urls),
                notes = note,
            };
        }

        // ---- CSV ----------------------------------------------------------------------

        static IEnumerable<Row> Rows(string lpcSourcePath)
        {
            if (_cacheRoot == lpcSourcePath && _rows != null) return _rows;
            _cacheRoot = lpcSourcePath;
            _rows = Parse(lpcSourcePath);
            return _rows;
        }

        static List<Row> Parse(string lpcSourcePath)
        {
            var rows = new List<Row>();
            string csv = FindCreditsCsv(lpcSourcePath);
            if (csv == null) return rows;

            var lines = SplitRecords(File.ReadAllText(csv));
            if (lines.Count < 2) return rows;

            var header = lines[0];
            int ip = Col(header, "file", "path", "filename");
            int ia = Col(header, "author");
            int il = Col(header, "licen");
            int iu = Col(header, "url", "link");
            int inn = Col(header, "note");
            if (ip < 0) ip = 0; // assume first column is the path

            for (int i = 1; i < lines.Count; i++)
            {
                var f = lines[i];
                if (f.Count == 0) continue;
                rows.Add(new Row
                {
                    path = Get(f, ip),
                    authors = Get(f, ia),
                    licenses = Get(f, il),
                    urls = Get(f, iu),
                    notes = Get(f, inn),
                });
            }
            return rows;
        }

        static string FindCreditsCsv(string root)
        {
            if (string.IsNullOrEmpty(root)) return null;
            foreach (var name in new[] { "CREDITS.csv", "credits.csv", "spritesheets/CREDITS.csv", "spritesheets/credits.csv" })
            {
                string p = root + "/" + name;
                if (File.Exists(p)) return p;
            }
            return null;
        }

        static int Col(List<string> header, params string[] needles)
        {
            for (int i = 0; i < header.Count; i++)
            {
                string h = (header[i] ?? "").Trim().ToLowerInvariant();
                foreach (var n in needles) if (h.Contains(n)) return i;
            }
            return -1;
        }

        static string Get(List<string> fields, int i) => (i >= 0 && i < fields.Count) ? fields[i] : null;

        // RFC4180-ish: quote-aware split into records of fields.
        static List<List<string>> SplitRecords(string text)
        {
            var records = new List<List<string>>();
            var field = new System.Text.StringBuilder();
            var record = new List<string>();
            bool inQuotes = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else field.Append(c);
                }
                else if (c == '"') inQuotes = true;
                else if (c == ',') { record.Add(field.ToString()); field.Clear(); }
                else if (c == '\r') { /* ignore */ }
                else if (c == '\n') { record.Add(field.ToString()); field.Clear(); records.Add(record); record = new List<string>(); }
                else field.Append(c);
            }
            if (field.Length > 0 || record.Count > 0) { record.Add(field.ToString()); records.Add(record); }
            return records;
        }

        // ---- helpers ------------------------------------------------------------------

        static string Norm(string s) => (s ?? "").Replace('\\', '/').Trim().Trim('/').ToLowerInvariant();

        static void AddSplit(List<string> into, string cell)
        {
            if (string.IsNullOrEmpty(cell)) return;
            foreach (var part in cell.Split(',', ';'))
            {
                var t = part.Trim();
                if (t.Length > 0) into.Add(t);
            }
        }

        static void AddRange(List<string> into, string[] src)
        {
            if (src == null) return;
            foreach (var s in src) { var t = s?.Trim(); if (!string.IsNullOrEmpty(t)) into.Add(t); }
        }

        static string[] Dedupe(List<string> items)
        {
            var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            var outp = new List<string>();
            foreach (var s in items) if (seen.Add(s)) outp.Add(s);
            return outp.ToArray();
        }

        /// <summary>Reset the CSV cache (call at the start of an import run).</summary>
        public static void ResetCache() { _cacheRoot = null; _rows = null; }
    }
}
