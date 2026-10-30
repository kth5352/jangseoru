using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Jangseoru.Core
{
    /// <summary>
    /// The fixed list of event illustration keys (<c>pic_*</c>) the gallery knows about, loaded once from an
    /// embedded resource. An unreadable resource yields an empty catalog; nothing here throws.
    /// </summary>
    public static class PictureCatalog
    {
        private const string Prefix = "pic_";
        private const string ResourceSuffix = "picture_keys.txt";

        private static readonly Lazy<Loaded> data = new Lazy<Loaded>(Load);
        private static readonly object Sync = new object();
        private static volatile Loaded extended; // the built-in list plus what the game's catalog added since (see MediaDiscovery)

        private static Loaded Current => extended ?? data.Value;

        /// <summary>
        /// Adds picture keys the game's catalog has but the built-in list lacks (content of a game update). Returns how many were new.
        /// The built-in keys are never removed.
        /// </summary>
        public static int Extend(IEnumerable<string> discovered)
        {
            lock (Sync)
            {
                var cur = Current;
                var merged = CatalogMerge.Pictures(cur.Keys, discovered);
                if (merged.Count == cur.Keys.Count) return 0;
                extended = new Loaded { Keys = merged, Set = new HashSet<string>(merged, StringComparer.Ordinal) };
                return merged.Count - cur.Keys.Count;
            }
        }

        private sealed class Loaded
        {
            public IReadOnlyList<string> Keys;
            public HashSet<string> Set;
        }

        public static IReadOnlyList<string> Keys => Current.Keys;

        public static int Count => Current.Keys.Count;

        public static bool Contains(string catalogKey)
        {
            return catalogKey != null && Current.Set.Contains(catalogKey);
        }

        public static bool TryNormalize(string scriptKey, out string catalogKey)
        {
            catalogKey = null;
            if (string.IsNullOrWhiteSpace(scriptKey)) return false;
            var candidate = Prefix + scriptKey;
            if (!Current.Set.Contains(candidate)) return false;
            catalogKey = candidate;
            return true;
        }

        public static string Group(string catalogKey)
        {
            if (string.IsNullOrEmpty(catalogKey)) return string.Empty;
            var name = catalogKey.StartsWith(Prefix, StringComparison.Ordinal) ? catalogKey.Substring(Prefix.Length) : catalogKey;
            int cut = name.LastIndexOf('_');
            if (cut > 0 && cut < name.Length - 1 && name.Skip(cut + 1).All(c => c >= '0' && c <= '9'))
                return name.Substring(0, cut);
            return name;
        }

        private static Loaded Load()
        {
            var keys = new List<string>();
            try
            {
                var asm = typeof(PictureCatalog).Assembly;
                var name = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(ResourceSuffix, StringComparison.Ordinal));
                if (name != null)
                {
                    using (var stream = asm.GetManifestResourceStream(name))
                    {
                        if (stream != null)
                        {
                            using (var reader = new StreamReader(stream))
                            {
                                string line;
                                while ((line = reader.ReadLine()) != null)
                                {
                                    line = line.Trim();
                                    if (line.Length > 0) keys.Add(line);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                keys.Clear();
            }
            var set = new HashSet<string>(keys, StringComparer.Ordinal);
            var sorted = set.ToList();
            sorted.Sort(StringComparer.Ordinal);
            return new Loaded { Keys = sorted, Set = set };
        }
    }
}
