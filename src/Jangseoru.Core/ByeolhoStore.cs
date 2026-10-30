using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Jangseoru.Core
{
    public enum SlotMatch
    {
        None,
        Exact,
        Mismatch,
        Ambiguous
    }

    /// <summary>
    /// Identity of one observed save version: the user profile folder, the save file's slot name, and the
    /// game's own <c>GameSave.TimeTick</c> token. The token is compared as an opaque long (never as a time).
    /// </summary>
    public sealed class SnapshotKey : IEquatable<SnapshotKey>
    {
        public string Profile { get; }
        public string Stem { get; }
        public long TimeTick { get; }

        public SnapshotKey(string profile, string stem, long timeTick)
        {
            if (string.IsNullOrEmpty(profile)) throw new ArgumentException("profile is required", nameof(profile));
            if (string.IsNullOrEmpty(stem)) throw new ArgumentException("stem is required", nameof(stem));
            if (timeTick <= 0 || timeTick > DateTime.MaxValue.Ticks) throw new ArgumentException("timeTick must be a positive DateTime tick value", nameof(timeTick));
            Profile = profile;
            Stem = stem;
            TimeTick = timeTick;
        }

        public bool Equals(SnapshotKey other)
            => other != null
               && TimeTick == other.TimeTick
               && string.Equals(Profile, other.Profile, StringComparison.Ordinal)
               && string.Equals(Stem, other.Stem, StringComparison.Ordinal);

        public override bool Equals(object obj) => Equals(obj as SnapshotKey);

        public override int GetHashCode()
        {
            unchecked
            {
                var h = StringComparer.Ordinal.GetHashCode(Profile);
                h = (h * 397) ^ StringComparer.Ordinal.GetHashCode(Stem);
                return (h * 397) ^ TimeTick.GetHashCode();
            }
        }
    }

    /// <summary>
    /// Mod-owned record of acquired byeolho: a global set plus per-save-version snapshots.
    /// Pure data; no game or file access. Text format v1 is documented on <see cref="Serialize"/>.
    /// </summary>
    public sealed class ByeolhoStore
    {
        public const int MaxByeolhoId = 25;

        private const string Header = "jangseoru.byeolho v1";
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        private readonly SortedDictionary<int, long> global = new SortedDictionary<int, long>();
        private readonly SortedSet<int> active = new SortedSet<int>();
        private readonly Dictionary<SnapshotKey, int[]> snapshots = new Dictionary<SnapshotKey, int[]>();
        private readonly HashSet<SnapshotKey> ambiguous = new HashSet<SnapshotKey>();
        private readonly HashSet<string> knownSlots = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> unknownLines = new List<string>();
        private string futureRaw;

        public string GameVersion { get; set; }

        /// <summary>True for a file written by a newer version of this mod: never modified on disk.</summary>
        public bool ReadOnly { get; private set; }

        /// <summary>Every byeolho ever collected on this machine, ascending. A fresh copy on every call.</summary>
        public IReadOnlyCollection<int> Collected => new ReadOnlyCollection<int>(global.Keys.ToList());

        /// <summary>Byeolho of the currently active save version, ascending. A fresh copy on every call.</summary>
        public IReadOnlyCollection<int> ActiveCollected => new ReadOnlyCollection<int>(active.ToList());

        /// <summary>
        /// Adds a valid id (0..25) to the global set and, if requested, to the active list. Returns true only
        /// when the id is new to the global set; the active list may change even when this returns false.
        /// </summary>
        public bool Collect(int id, DateTime utcNow, bool includeActive)
        {
            if (id < 0 || id > MaxByeolhoId) return false;
            if (includeActive) active.Add(id);
            if (global.ContainsKey(id)) return false;
            global[id] = Math.Max(1L, utcNow.Ticks);
            return true;
        }

        /// <summary>
        /// Stores an independent copy of the active list under <paramref name="key"/>. Identical repeats are
        /// ignored; a different list for an already-recorded key marks the key ambiguous. Other tokens are untouched.
        /// </summary>
        public bool RecordSnapshot(SnapshotKey key)
        {
            if (key == null) return false;
            if (ambiguous.Contains(key)) return false;

            var copy = active.ToArray();
            if (snapshots.TryGetValue(key, out var existing))
            {
                if (existing.SequenceEqual(copy)) return false;
                snapshots.Remove(key);
                ambiguous.Add(key);
                return true;
            }

            snapshots[key] = copy;
            knownSlots.Add(SlotId(key));
            return true;
        }

        /// <summary>Always empties the active list first; restores only an exact, unambiguous key.</summary>
        public SlotMatch ActivateSnapshot(SnapshotKey key)
        {
            active.Clear();
            if (key == null) return SlotMatch.None;
            if (ambiguous.Contains(key)) return SlotMatch.Ambiguous;
            if (snapshots.TryGetValue(key, out var ids))
            {
                foreach (var id in ids) active.Add(id);
                return SlotMatch.Exact;
            }
            return knownSlots.Contains(SlotId(key)) ? SlotMatch.Mismatch : SlotMatch.None;
        }

        public void ClearActive() => active.Clear();

        /// <summary>
        /// Text format (UTF-8, '\n' lines), deterministic order:
        /// <code>
        /// jangseoru.byeolho v1
        /// game &lt;base64 utf8 version&gt;
        /// global &lt;id&gt; &lt;firstSeenUtcTicks&gt;
        /// snapshot &lt;b64 profile&gt; &lt;b64 stem&gt; &lt;timeTick&gt; &lt;id,id,...|-&gt;
        /// ambiguous &lt;b64 profile&gt; &lt;b64 stem&gt; &lt;timeTick&gt;
        /// </code>
        /// Unknown v1 lines are kept at the end. A read-only (newer-version) store returns its original text.
        /// </summary>
        public string Serialize()
        {
            if (ReadOnly) return futureRaw;

            var sb = new StringBuilder();
            sb.Append(Header).Append('\n');
            if (GameVersion != null) sb.Append("game ").Append(B64(GameVersion)).Append('\n');
            foreach (var kv in global)
                sb.Append("global ").Append(kv.Key.ToString(CultureInfo.InvariantCulture)).Append(' ')
                  .Append(kv.Value.ToString(CultureInfo.InvariantCulture)).Append('\n');
            foreach (var kv in snapshots.OrderBy(k => k.Key.Profile, StringComparer.Ordinal)
                                        .ThenBy(k => k.Key.Stem, StringComparer.Ordinal)
                                        .ThenBy(k => k.Key.TimeTick))
            {
                sb.Append("snapshot ").Append(KeyText(kv.Key)).Append(' ')
                  .Append(kv.Value.Length == 0 ? "-" : string.Join(",", kv.Value.Select(i => i.ToString(CultureInfo.InvariantCulture))))
                  .Append('\n');
            }
            foreach (var key in ambiguous.OrderBy(k => k.Profile, StringComparer.Ordinal)
                                         .ThenBy(k => k.Stem, StringComparer.Ordinal)
                                         .ThenBy(k => k.TimeTick))
                sb.Append("ambiguous ").Append(KeyText(key)).Append('\n');
            foreach (var line in unknownLines) sb.Append(line).Append('\n');
            return sb.ToString();
        }

        /// <summary>
        /// Never throws. A missing/garbled header or a malformed known record is corrupt (empty writable store
        /// returned, <paramref name="wasCorrupt"/> true). A newer header version yields a read-only store.
        /// </summary>
        public static ByeolhoStore Parse(string text, out bool wasCorrupt)
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
            return new ByeolhoStore();
        }

        private static ByeolhoStore TryParse(string text)
        {
            if (text == null) return null;
            var lines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();

            var version = ParseHeader(lines[0]);
            if (version < 1) return null;
            if (version > 1) return new ByeolhoStore { ReadOnly = true, futureRaw = text };

            var store = new ByeolhoStore();
            for (var i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.Length == 0) continue;
                var parts = line.Split(' ');
                switch (parts[0])
                {
                    case "game":
                        if (parts.Length != 2 || !TryDecode(parts[1], out var version2)) return null;
                        store.GameVersion = version2;
                        break;
                    case "global":
                        if (parts.Length != 3 || !TryId(parts[1], out var id) || !TryTick(parts[2], out var first)) return null;
                        if (store.global.ContainsKey(id)) return null;
                        store.global[id] = first;
                        break;
                    case "snapshot":
                        if (parts.Length != 5 || !TryKey(parts[1], parts[2], parts[3], out var skey) || !TryIds(parts[4], out var ids)) return null;
                        store.AddParsedSnapshot(skey, ids);
                        break;
                    case "ambiguous":
                        if (parts.Length != 4 || !TryKey(parts[1], parts[2], parts[3], out var akey)) return null;
                        store.snapshots.Remove(akey);
                        store.ambiguous.Add(akey);
                        store.knownSlots.Add(SlotId(akey));
                        break;
                    default:
                        store.unknownLines.Add(line);
                        break;
                }
            }
            return store;
        }

        private void AddParsedSnapshot(SnapshotKey key, int[] ids)
        {
            if (ambiguous.Contains(key)) return;
            if (snapshots.TryGetValue(key, out var existing))
            {
                if (existing.SequenceEqual(ids)) return;
                snapshots.Remove(key);
                ambiguous.Add(key);
                return;
            }
            snapshots[key] = ids;
            knownSlots.Add(SlotId(key));
        }

        private static long ParseHeader(string line)
        {
            const string prefix = "jangseoru.byeolho v";
            if (line == null || !line.StartsWith(prefix, StringComparison.Ordinal)) return -1;
            var digits = line.Substring(prefix.Length);
            if (digits.Length == 0 || digits[0] == '0' || digits.Any(c => c < '0' || c > '9')) return -1;
            return long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var v) ? v : long.MaxValue;
        }

        private static bool TryId(string s, out int id)
            => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out id) && id >= 0 && id <= MaxByeolhoId;

        private static bool TryTick(string s, out long tick)
            => long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out tick) && tick > 0 && tick <= DateTime.MaxValue.Ticks;

        private static bool TryIds(string s, out int[] ids)
        {
            ids = new int[0];
            if (s == "-") return true;
            var set = new SortedSet<int>();
            foreach (var token in s.Split(','))
            {
                if (!TryId(token, out var id) || !set.Add(id)) return false;
            }
            ids = set.ToArray();
            return true;
        }

        private static bool TryKey(string profileB64, string stemB64, string tick, out SnapshotKey key)
        {
            key = null;
            if (!TryDecode(profileB64, out var profile) || profile.Length == 0) return false;
            if (!TryDecode(stemB64, out var stem) || stem.Length == 0) return false;
            if (!TryTick(tick, out var t)) return false;
            key = new SnapshotKey(profile, stem, t);
            return true;
        }

        private static bool TryDecode(string b64, out string value)
        {
            value = null;
            try
            {
                value = StrictUtf8.GetString(Convert.FromBase64String(b64));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string B64(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

        private static string KeyText(SnapshotKey key)
            => B64(key.Profile) + " " + B64(key.Stem) + " " + key.TimeTick.ToString(CultureInfo.InvariantCulture);

        private static string SlotId(SnapshotKey key)
            => key.Profile.Length.ToString(CultureInfo.InvariantCulture) + ":" + key.Profile + key.Stem;
    }
}
