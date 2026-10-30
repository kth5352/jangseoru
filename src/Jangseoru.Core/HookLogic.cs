using System;
using System.IO;

namespace Jangseoru.Core
{
    /// <summary>Input validation for game events; pure functions, nothing is opened or read from disk.</summary>
    public static class HookLogic
    {
        /// <summary>True only for the title stat with both the requested and the resulting value in 0..25.</summary>
        public static bool ShouldCollectOnSetPlayer(string name, int requestedValue, int actualValue)
            => string.Equals(name, "title", StringComparison.Ordinal)
               && InRange(requestedValue)
               && InRange(actualValue);

        /// <summary>The gallery hook records only while the feature is on and the game passed a real key.</summary>
        public static bool ShouldRecordPicture(bool galleryOn, string key)
            => galleryOn && !string.IsNullOrWhiteSpace(key);

        private static bool InRange(int v) => v >= 0 && v <= ByeolhoStore.MaxByeolhoId;

        /// <summary>
        /// Save and load observations feed both the global list and the per-save snapshots, so they stay on
        /// while either feature is available: losing one hook must not silence the other.
        /// </summary>
        public static bool ShouldObserveSaveEvents(bool titlesOn, bool perSaveOn) => titlesOn || perSaveOn;

        /// <summary>
        /// Accepts exactly <c>&lt;persistentDataPath&gt;\&lt;one profile folder&gt;\Save_&lt;stem&gt;.dat</c>. The universe save,
        /// legend saves, backups, nested or out-of-root paths and anything unparsable are rejected. Profile and
        /// stem are lower-cased so Windows path casing can never split one slot in two.
        /// </summary>
        public static bool TryGetSlotIdentity(string persistentDataPath, string saveFilePath, out string profile, out string stem)
        {
            profile = null;
            stem = null;
            if (string.IsNullOrWhiteSpace(persistentDataPath) || string.IsNullOrWhiteSpace(saveFilePath)) return false;

            try
            {
                var root = Path.GetFullPath(persistentDataPath).TrimEnd('\\', '/');
                var prefix = root + Path.DirectorySeparatorChar;
                var full = Path.GetFullPath(saveFilePath);
                if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;

                var parts = full.Substring(prefix.Length).Split('\\', '/');
                if (parts.Length != 2 || parts[0].Length == 0) return false;

                var name = parts[1];
                const string head = "Save_";
                const string tail = ".dat";
                if (name.Length <= head.Length + tail.Length
                    || !name.StartsWith(head, StringComparison.OrdinalIgnoreCase)
                    || !name.EndsWith(tail, StringComparison.OrdinalIgnoreCase)) return false;

                var s = name.Substring(head.Length, name.Length - head.Length - tail.Length);
                if (s.Length == 0 || string.Equals(s, "universe", StringComparison.OrdinalIgnoreCase)) return false;

                profile = parts[0].ToLowerInvariant();
                stem = s.ToLowerInvariant();
                return true;
            }
            catch (Exception)
            {
                profile = null;
                stem = null;
                return false;
            }
        }
    }
}
