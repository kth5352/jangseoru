using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Jangseoru.Core
{
    /// <summary>
    /// The mod's own layer over the game's ending record (LibrarySystem.EndCg counts), which the mod never writes: oaths the F11
    /// popup opened, and per-oath count baselines taken at a reset (an oath counts as seen only when the game's count rises above
    /// its baseline). Text format v1: the header, then <c>Open:id</c> and <c>Base:id:count</c> lines. Pure data.
    /// </summary>
    public sealed class OathStore
    {
        public const string Header = "jangseoru.oath v1";
        private const int MaxLines = 200; // the game has six oaths
        private readonly SortedSet<string> opened = new SortedSet<string>(StringComparer.Ordinal);
        private readonly SortedDictionary<string, int> baselines = new SortedDictionary<string, int>(StringComparer.Ordinal);
        private string futureRaw;

        public bool ReadOnly { get; private set; }

        public bool Opened(string id) => id != null && opened.Contains(id);

        public int Baseline(string id) => id != null && baselines.TryGetValue(id, out var n) ? n : 0;

        public bool Open(string id)
        {
            if (ReadOnly || !IsId(id) || opened.Count >= MaxLines) return false;
            return opened.Add(id);
        }

        /// <summary>Forgets every opened oath and takes the game's current counts as the new baselines.</summary>
        public bool ResetTo(IEnumerable<KeyValuePair<string, int>> gameCounts)
        {
            if (ReadOnly) return false;
            opened.Clear();
            baselines.Clear();
            if (gameCounts != null)
                foreach (var pair in gameCounts)
                    if (IsId(pair.Key) && pair.Value > 0 && baselines.Count < MaxLines) baselines[pair.Key] = pair.Value;
            return true;
        }

        public string Serialize()
        {
            if (ReadOnly) return futureRaw;
            var sb = new StringBuilder();
            sb.Append(Header).Append('\n');
            foreach (var id in opened) sb.Append("Open:").Append(id).Append('\n');
            foreach (var pair in baselines)
                sb.Append("Base:").Append(pair.Key).Append(':').Append(pair.Value.ToString(CultureInfo.InvariantCulture)).Append('\n');
            return sb.ToString();
        }

        /// <summary>Never throws. A garbled header is corrupt; a newer version is read-only; malformed lines are skipped.</summary>
        public static OathStore Parse(string text, out bool wasCorrupt)
        {
            wasCorrupt = true;
            try
            {
                if (text == null) return new OathStore();
                var lines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
                const string prefix = "jangseoru.oath v";
                var header = lines[0];
                if (!header.StartsWith(prefix, StringComparison.Ordinal)) return new OathStore();
                var version = header.Substring(prefix.Length);
                if (version.Length == 0 || version[0] == '0' || version.Any(c => c < '0' || c > '9')) return new OathStore();
                wasCorrupt = false;
                if (version != "1") return new OathStore { ReadOnly = true, futureRaw = text };
                var store = new OathStore();
                foreach (var line in lines.Skip(1).Take(MaxLines * 2))
                {
                    var parts = line.Split(':');
                    if (parts.Length == 2 && parts[0] == "Open" && IsId(parts[1])) store.opened.Add(parts[1]);
                    else if (parts.Length == 3 && parts[0] == "Base" && IsId(parts[1])
                             && int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n > 0)
                        store.baselines[parts[1]] = n;
                }
                return store;
            }
            catch (Exception)
            {
                wasCorrupt = true;
                return new OathStore();
            }
        }

        private static bool IsId(string id) => !string.IsNullOrEmpty(id) && id.Length <= 12 && id.All(c => c >= '0' && c <= '9');
    }
}
