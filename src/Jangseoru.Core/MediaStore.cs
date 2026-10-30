using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Jangseoru.Core
{
    /// <summary>
    /// Mod-owned record of the backgrounds, big pictures and videos the player has seen. Pure data; no game or file access.
    /// Text format v1: the header line, then one <c>Kind:key</c> line per picture or video in ordinal order.
    /// </summary>
    public sealed class MediaStore
    {
        public const string Header = "jangseoru.media v1";

        private const int MaxPassthrough = 2000;
        // The game has a few hundred pictures and videos; a file with far more known lines is damaged or hostile.
        private const int MaxKnown = 4000;
        private static readonly Regex ValidId = new Regex("^[A-Za-z0-9_]{1,80}$", RegexOptions.CultureInvariant);
        private static readonly Regex UnknownLine = new Regex("^[A-Za-z][A-Za-z0-9_]{0,20}:[A-Za-z0-9_]{1,80}$", RegexOptions.CultureInvariant);

        private readonly SortedSet<string> known = new SortedSet<string>(StringComparer.Ordinal);
        // Well-formed lines of kinds this build does not know: kept and re-written so a newer or older build never erases them.
        private readonly SortedSet<string> passthrough = new SortedSet<string>(StringComparer.Ordinal);
        private string futureRaw;

        /// <summary>True for a file written by a newer version of this mod: never modified on disk.</summary>
        public bool ReadOnly { get; private set; }

        /// <summary>Entries of the known kinds.</summary>
        public int Count => known.Count;

        public bool Has(MediaKind kind, string id) => IsValidId(id) && known.Contains(Line(kind, id));

        /// <summary>True only when the key is valid and new.</summary>
        public bool Add(MediaKind kind, string id)
        {
            if (!IsValidId(id)) return false;
            return known.Add(Line(kind, id));
        }

        /// <summary>Forgets every entry of the known kinds (lines of unknown kinds are kept); a read-only store is left alone.</summary>
        public bool Clear()
        {
            if (ReadOnly) return false;
            known.Clear();
            return true;
        }

        /// <summary>A read-only (newer-version) store returns its original text.</summary>
        public string Serialize()
        {
            if (ReadOnly) return futureRaw;
            var sb = new StringBuilder();
            sb.Append(Header).Append('\n');
            foreach (var line in known.Union(passthrough).OrderBy(l => l, StringComparer.Ordinal))
                sb.Append(line).Append('\n');
            return sb.ToString();
        }

        /// <summary>
        /// Never throws. A missing or garbled header is corrupt (empty writable store, <paramref name="wasCorrupt"/> true);
        /// a newer header version yields a read-only store. Duplicate, blank and malformed lines are skipped.
        /// </summary>
        public static MediaStore Parse(string text, out bool wasCorrupt)
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
            return new MediaStore();
        }

        private static MediaStore TryParse(string text)
        {
            if (text == null) return null;
            var lines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();

            var version = ParseHeader(lines[0]);
            if (version < 1) return null;
            if (version > 1) return new MediaStore { ReadOnly = true, futureRaw = text };

            var store = new MediaStore();
            for (var i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.Length == 0) continue;
                var cut = line.IndexOf(':');
                if (cut > 0 && MediaKinds.TryFromPrefix(line.Substring(0, cut), out var kind))
                {
                    if (store.known.Count < MaxKnown) store.Add(kind, line.Substring(cut + 1)); // a malformed id is dropped
                    continue;
                }
                if (cut > 0 && UnknownLine.IsMatch(line) && store.passthrough.Count < MaxPassthrough)
                    store.passthrough.Add(line);
            }
            return store;
        }

        private static bool IsValidId(string id) => id != null && ValidId.IsMatch(id);

        private static string Line(MediaKind kind, string id) => MediaKinds.Prefix(kind) + ":" + id;

        private static long ParseHeader(string line)
        {
            const string prefix = "jangseoru.media v";
            if (line == null || !line.StartsWith(prefix, StringComparison.Ordinal)) return -1;
            var digits = line.Substring(prefix.Length);
            if (digits.Length == 0 || digits[0] == '0' || digits.Any(c => c < '0' || c > '9')) return -1;
            return long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var v) ? v : long.MaxValue;
        }
    }
}
