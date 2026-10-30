using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Jangseoru.Core
{
    /// <summary>One background, big picture or video of the game: the key the mod records and the Addressables address it loads by.</summary>
    public sealed class MediaEntry
    {
        public MediaKind Kind { get; set; }
        /// <summary>Backgrounds: the sprite name; big pictures and videos: the script key the game loads.</summary>
        public string Key { get; set; }
        public string Address { get; set; }
    }

    /// <summary>
    /// The fixed lists of the game's backgrounds (213, combat ones last), big pictures (29) and videos (10), loaded once from an
    /// embedded resource. An unreadable resource yields empty lists; nothing here throws.
    /// </summary>
    public static class MediaCatalog
    {
        private const string ResourceSuffix = "media_keys.txt";
        private static readonly Lazy<Dictionary<MediaKind, List<MediaEntry>>> data = new Lazy<Dictionary<MediaKind, List<MediaEntry>>>(Load);
        private static readonly object Sync = new object();

        private sealed class Snapshot
        {
            public Dictionary<MediaKind, List<MediaEntry>> Lists;
            public Dictionary<MediaKind, Dictionary<string, MediaEntry>> Index;
        }

        private static readonly Lazy<Snapshot> builtIn = new Lazy<Snapshot>(() => Make(data.Value));
        private static volatile Snapshot extended; // the built-in lists plus what the game's catalog added since (see MediaDiscovery)

        private static Snapshot Current => extended ?? builtIn.Value;

        private static Snapshot Make(Dictionary<MediaKind, List<MediaEntry>> lists) => new Snapshot
        {
            Lists = lists,
            Index = lists.ToDictionary(p => p.Key, p => p.Value.ToDictionary(e => e.Key, StringComparer.Ordinal)),
        };

        public static IReadOnlyList<MediaEntry> Entries(MediaKind kind) => Current.Lists[kind];

        public static bool Contains(MediaKind kind, string key) => Find(kind, key) != null;

        public static MediaEntry Find(MediaKind kind, string key)
        {
            if (key == null) return null;
            return Current.Index[kind].TryGetValue(key, out var entry) ? entry : null;
        }

        /// <summary>
        /// Adds entries the game's catalog has but the built-in list lacks (content of a game update). Returns how many were new.
        /// The built-in entries are never removed or changed.
        /// </summary>
        public static int Extend(MediaKind kind, IEnumerable<MediaEntry> discovered)
        {
            lock (Sync)
            {
                var cur = Current;
                var merged = CatalogMerge.Media(kind, cur.Lists[kind], discovered);
                if (merged.Count == cur.Lists[kind].Count) return 0;
                var lists = cur.Lists.ToDictionary(p => p.Key, p => p.Value);
                lists[kind] = merged.ToList();
                extended = Make(lists);
                return merged.Count - cur.Lists[kind].Count;
            }
        }

        private static Dictionary<MediaKind, List<MediaEntry>> Load()
        {
            var result = new Dictionary<MediaKind, List<MediaEntry>>();
            foreach (MediaKind kind in Enum.GetValues(typeof(MediaKind))) result[kind] = new List<MediaEntry>();
            try
            {
                var asm = typeof(MediaCatalog).Assembly;
                var name = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(ResourceSuffix, StringComparison.Ordinal));
                if (name == null) return result;
                using (var stream = asm.GetManifestResourceStream(name))
                {
                    if (stream == null) return result;
                    using (var reader = new StreamReader(stream))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            var parts = line.Trim().Split('\t');
                            if (parts.Length != 3 || !Enum.TryParse(parts[0], out MediaKind kind)) continue;
                            if (parts[1].Length == 0 || parts[2].Length == 0) continue;
                            if (result[kind].Any(e => e.Key == parts[1])) continue;
                            result[kind].Add(new MediaEntry { Kind = kind, Key = parts[1], Address = parts[2] });
                        }
                    }
                }
            }
            catch (Exception)
            {
                foreach (var list in result.Values) list.Clear();
            }
            return result;
        }
    }
}
