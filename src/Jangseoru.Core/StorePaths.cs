using System;
using System.IO;

namespace Jangseoru.Core
{
    /// <summary>The single place that decides where the mod keeps its data.</summary>
    public static class StorePaths
    {
        public const string FolderName = "JangseoruMod";
        public const string FileName = "byeolho.txt";
        public const string GalleryFileName = "gallery.txt";
        public const string InyeonFileName = "inyeon.txt";
        public const string ItemsFileName = "items.txt";
        public const string MediaFileName = "media.txt";
        public const string OathFileName = "oath.txt";

        /// <summary>
        /// A sibling of the game's save folder: <c>&lt;parent of persistentDataPath&gt;\JangseoruMod\byeolho.txt</c>.
        /// Deliberately outside the game's own folder (a Steam Auto-Cloud target). Null when no parent exists.
        /// </summary>
        public static string Resolve(string persistentDataPath) => ResolveIn(persistentDataPath, FileName);

        /// <summary>The gallery file, next to <see cref="FileName"/> and resolved by the same rule.</summary>
        public static string ResolveGallery(string persistentDataPath) => ResolveIn(persistentDataPath, GalleryFileName);
        public static string ResolveInyeon(string persistentDataPath) => ResolveIn(persistentDataPath, InyeonFileName);

        /// <summary>The obtained-items file, next to the others and resolved by the same rule.</summary>
        public static string ResolveItems(string persistentDataPath) => ResolveIn(persistentDataPath, ItemsFileName);

        /// <summary>The seen backgrounds/big pictures/videos file, next to the others and resolved by the same rule.</summary>
        public static string ResolveMedia(string persistentDataPath) => ResolveIn(persistentDataPath, MediaFileName);

        /// <summary>The mod's layer over the game's ending record (opened oaths, reset baselines).</summary>
        public static string ResolveOath(string persistentDataPath) => ResolveIn(persistentDataPath, OathFileName);

        private static string ResolveIn(string persistentDataPath, string fileName)
        {
            if (string.IsNullOrWhiteSpace(persistentDataPath)) return null;
            try
            {
                var trimmed = persistentDataPath.TrimEnd('\\', '/');
                if (trimmed.Length == 0) return null;
                var parent = Path.GetDirectoryName(trimmed);
                if (string.IsNullOrEmpty(parent)) return null;
                return Path.Combine(parent, FolderName, fileName);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
