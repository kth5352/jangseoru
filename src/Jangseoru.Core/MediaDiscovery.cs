using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Jangseoru.Core
{
    /// <summary>One entry of the game's Addressables catalog as the plugin reads it (no Unity types).</summary>
    public sealed class CatalogLocation
    {
        public string Key { get; set; }
        /// <summary>The asset's path, e.g. <c>Assets/__Project/Images/Background/x.png</c>.</summary>
        public string InternalId { get; set; }
        /// <summary>The asset type's short name (<c>Sprite</c>, <c>VideoClip</c>, ...).</summary>
        public string ResourceType { get; set; }
    }

    /// <summary>
    /// Finds the game's pictures, backgrounds, big pictures and videos in its catalog, the same way the built-in lists were made,
    /// so that content added by a game update shows up without a new mod build. Pure; the plugin only supplies the locations.
    /// </summary>
    public static class MediaDiscovery
    {
        public const string PictureFolder = "Assets/__Project/Images/Picture/";
        public const string BackgroundFolder = "Assets/__Project/Images/Background/";
        public const string BigPictureFolder = "Assets/__Project/Images/BigPicture/";
        public const string VideoFolder = "Assets/__Project/Videos/";
        /// <summary>A catalog with far more entries than the game has is not trusted further.</summary>
        public const int MaxPerKind = 5000;

        private static readonly Regex ValidKey = new Regex("^[A-Za-z0-9_]{1,80}$", RegexOptions.CultureInvariant);

        /// <summary>The <c>pic_*</c> sprite names under the picture folder, sorted.</summary>
        public static IReadOnlyList<string> Pictures(IEnumerable<CatalogLocation> locations)
        {
            var keys = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var key in Names(locations, PictureFolder, "Sprite"))
                if (key.StartsWith("pic_", StringComparison.Ordinal) && keys.Count < MaxPerKind) keys.Add(key);
            return keys.ToList();
        }

        /// <summary>
        /// Entries of one media tab. Backgrounds are keyed by sprite name and loaded by path, combat ones last; big pictures and
        /// videos are keyed and loaded by name (the script key with the game's prefix).
        /// </summary>
        public static IReadOnlyList<MediaEntry> Entries(MediaKind kind, IEnumerable<CatalogLocation> locations)
        {
            string folder, type;
            switch (kind)
            {
                case MediaKind.Background: folder = BackgroundFolder; type = "Sprite"; break;
                case MediaKind.BigPicture: folder = BigPictureFolder; type = "Sprite"; break;
                default: folder = VideoFolder; type = "VideoClip"; break;
            }
            var byKey = new Dictionary<string, MediaEntry>(StringComparer.Ordinal);
            foreach (var location in Matching(locations, folder, type))
            {
                var key = NameOf(location.InternalId);
                if (!ValidKey.IsMatch(key) || byKey.ContainsKey(key) || byKey.Count >= MaxPerKind) continue;
                byKey[key] = new MediaEntry
                {
                    Kind = kind,
                    Key = key,
                    Address = kind == MediaKind.Background ? location.InternalId : key,
                };
            }
            return Order(kind, byKey.Values);
        }

        internal static IReadOnlyList<MediaEntry> Order(MediaKind kind, IEnumerable<MediaEntry> entries)
        {
            return kind == MediaKind.Background
                ? entries.OrderBy(e => IsCombat(e) ? 1 : 0).ThenBy(e => e.Address, StringComparer.Ordinal).ToList()
                : entries.OrderBy(e => e.Address, StringComparer.Ordinal).ToList();
        }

        private static bool IsCombat(MediaEntry e) => e.Address != null && e.Address.IndexOf("/Combat/", StringComparison.Ordinal) >= 0;

        private static IEnumerable<string> Names(IEnumerable<CatalogLocation> locations, string folder, string type)
            => Matching(locations, folder, type).Select(l => NameOf(l.InternalId)).Where(n => n.Length > 0).Distinct(StringComparer.Ordinal);

        private static IEnumerable<CatalogLocation> Matching(IEnumerable<CatalogLocation> locations, string folder, string type)
        {
            if (locations == null) yield break;
            foreach (var l in locations)
                if (l != null && l.InternalId != null && l.ResourceType == type && l.InternalId.StartsWith(folder, StringComparison.Ordinal))
                    yield return l;
        }

        private static string NameOf(string internalId)
        {
            var slash = internalId.LastIndexOf('/');
            var file = slash >= 0 ? internalId.Substring(slash + 1) : internalId;
            var dot = file.LastIndexOf('.');
            return dot > 0 ? file.Substring(0, dot) : file;
        }
    }

    /// <summary>Merges the built-in lists with what discovery found: the built-in entries stay as they are, new ones are added.</summary>
    public static class CatalogMerge
    {
        public static IReadOnlyList<string> Pictures(IEnumerable<string> builtIn, IEnumerable<string> discovered)
        {
            var set = new SortedSet<string>(builtIn ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            if (discovered != null)
                foreach (var key in discovered)
                    if (key != null && key.StartsWith("pic_", StringComparison.Ordinal) && Regex.IsMatch(key, "^[A-Za-z0-9_]{1,80}$")) set.Add(key);
            return set.ToList();
        }

        public static IReadOnlyList<MediaEntry> Media(MediaKind kind, IEnumerable<MediaEntry> builtIn, IEnumerable<MediaEntry> discovered)
        {
            var byKey = new Dictionary<string, MediaEntry>(StringComparer.Ordinal);
            foreach (var e in builtIn ?? Enumerable.Empty<MediaEntry>())
                if (e != null && !byKey.ContainsKey(e.Key)) byKey[e.Key] = e;
            foreach (var e in discovered ?? Enumerable.Empty<MediaEntry>())
                if (e != null && e.Kind == kind && !byKey.ContainsKey(e.Key)) byKey[e.Key] = e; // the built-in entry wins
            return MediaDiscovery.Order(kind, byKey.Values);
        }
    }
}
