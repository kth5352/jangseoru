using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Jangseoru.Core
{
    /// <summary>
    /// Mod-owned record of the event illustrations the player has seen. Pure data; no game or file access.
    /// Text format v1: the header line, then one catalog key per line in ordinal order.
    /// </summary>
    public sealed class GalleryStore
    {
        public const string Header = "jangseoru.gallery v1";

        private readonly SortedSet<string> pictures = new SortedSet<string>(StringComparer.Ordinal);
        // Well-formed keys this build does not know: kept and re-written so a catalog change or downgrade never erases them.
        private readonly SortedSet<string> passthrough = new SortedSet<string>(StringComparer.Ordinal);
        private string futureRaw;

        private const int MaxPassthrough = 2000;
        private static readonly System.Text.RegularExpressions.Regex WellFormed =
            new System.Text.RegularExpressions.Regex("^pic_[A-Za-z0-9_]{1,80}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        /// <summary>True for a file written by a newer version of this mod: never modified on disk.</summary>
        public bool ReadOnly { get; private set; }

        public int PictureCount => pictures.Count;

        /// <summary>Collected catalog keys, ordinal ascending. A fresh copy on every call.</summary>
        public IReadOnlyCollection<string> Pictures => new ReadOnlyCollection<string>(pictures.ToList());

        public bool HasPicture(string catalogKey) => catalogKey != null && pictures.Contains(catalogKey);

        /// <summary>True only when the key is in the catalog and new.</summary>
        public bool AddPicture(string catalogKey)
        {
            if (!PictureCatalog.Contains(catalogKey)) return false;
            return pictures.Add(catalogKey);
        }

        /// <summary>Forgets every entry of the known kinds (lines of unknown kinds are kept); a read-only store is left alone.</summary>
        public bool Clear()
        {
            if (ReadOnly) return false;
            pictures.Clear();
            return true;
        }

        /// <summary>A read-only (newer-version) store returns its original text.</summary>
        public string Serialize()
        {
            if (ReadOnly) return futureRaw;
            var sb = new StringBuilder();
            sb.Append(Header).Append('\n');
            foreach (var key in pictures.Union(passthrough).OrderBy(k => k, StringComparer.Ordinal))
                sb.Append(key).Append('\n');
            return sb.ToString();
        }

        /// <summary>
        /// Never throws. A missing/garbled header is corrupt (empty writable store, <paramref name="wasCorrupt"/>
        /// true); a newer header version yields a read-only store. Unknown, duplicate and blank lines are skipped.
        /// </summary>
        public static GalleryStore Parse(string text, out bool wasCorrupt)
        {
            wasCorrupt = false;
            try
            {
                var store = TryParse(text);
                if (store != null) return store;
            }
            catch (Exception)
            {
                // fall through: treated as corrupt
            }
            wasCorrupt = true;
            return new GalleryStore();
        }

        private static GalleryStore TryParse(string text)
        {
            if (text == null) return null;
            var lines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();

            var version = ParseHeader(lines[0]);
            if (version < 1) return null;
            if (version > 1) return new GalleryStore { ReadOnly = true, futureRaw = text };

            var store = new GalleryStore();
            for (var i = 1; i < lines.Length; i++)
            {
                if (lines[i].Length == 0) continue;
                if (store.AddPicture(lines[i])) continue;
                if (!PictureCatalog.Contains(lines[i]) && WellFormed.IsMatch(lines[i]) && store.passthrough.Count < MaxPassthrough)
                    store.passthrough.Add(lines[i]);
            }
            return store;
        }

        private static long ParseHeader(string line)
        {
            const string prefix = "jangseoru.gallery v";
            if (line == null || !line.StartsWith(prefix, StringComparison.Ordinal)) return -1;
            var digits = line.Substring(prefix.Length);
            if (digits.Length == 0 || digits[0] == '0' || digits.Any(c => c < '0' || c > '9')) return -1;
            return long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var v) ? v : long.MaxValue;
        }
    }
}
