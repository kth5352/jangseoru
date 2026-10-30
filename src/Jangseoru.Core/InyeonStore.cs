using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Jangseoru.Core
{
    /// <summary>Seen portraits and greatest biography stage, independent of saves and the event gallery format.</summary>
    public sealed class InyeonStore
    {
        public const string Header = "jangseoru.inyeon v1";
        private readonly Dictionary<string, HashSet<string>> pictures = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> biographies = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly SortedSet<string> unknown = new SortedSet<string>(StringComparer.Ordinal);
        private string futureRaw;
        public bool ReadOnly { get; private set; }

        public bool RecordPicture(string id, string key)
        {
            if (!InyeonCatalog.Accepts(id, key)) return false;
            InyeonCatalog.Learn(id, key); // a new image of a known person joins the catalog (no-op for a built-in one)
            if (!pictures.TryGetValue(id, out var set)) pictures[id] = set = new HashSet<string>(StringComparer.Ordinal);
            return set.Add(key);
        }

        public bool RecordBiography(string id, string textId, int stage)
        {
            if (stage < 1 || stage > 4 || !(InyeonCatalog.Find(id)?.BiographyIds.Contains(textId) ?? false)) return false;
            var key = id + "\t" + textId;
            if (biographies.TryGetValue(key, out var old) && old >= stage) return false;
            biographies[key] = stage;
            return true;
        }

        public int BiographyStage(string id, string textId) => id != null && textId != null && biographies.TryGetValue(id + "\t" + textId, out var n) ? n : 0;
        public IReadOnlyList<string> Pictures(string id)
        {
            var person = InyeonCatalog.Find(id);
            if (person == null || !pictures.TryGetValue(id, out var set)) return Array.AsReadOnly(new string[0]);
            return Array.AsReadOnly(person.Pictures.Where(set.Contains).ToArray());
        }
        public bool IsKnown(string id) => InyeonCatalog.Find(id) is var c && c != null
            && ((pictures.TryGetValue(id, out var set) && set.Count > 0) || c.BiographyIds.Any(t => BiographyStage(id, t) > 0));

        public static int StageForAffection(int value, bool prologue)
        {
            if (value < 0 || value > 100) return 0;
            var stage = value < 40 ? 1 : value < 70 ? 2 : value < 100 ? 3 : 4;
            return prologue ? Math.Min(2, stage) : stage;
        }

        /// <summary>Forgets every entry of the known kinds (lines of unknown kinds are kept); a read-only store is left alone.</summary>
        public bool Clear()
        {
            if (ReadOnly) return false;
            pictures.Clear();
            biographies.Clear();
            return true;
        }

        public string Serialize()
        {
            if (ReadOnly) return futureRaw;
            var lines = new SortedSet<string>(unknown, StringComparer.Ordinal);
            foreach (var person in pictures)
                foreach (var key in person.Value) lines.Add("P\t" + person.Key + "\t" + key);
            foreach (var item in biographies) lines.Add("B\t" + item.Key + "\t" + item.Value.ToString(CultureInfo.InvariantCulture));
            return Header + "\n" + string.Join("\n", lines) + (lines.Count > 0 ? "\n" : "");
        }

        public static InyeonStore Parse(string text, out bool wasCorrupt)
        {
            wasCorrupt = true;
            try
            {
                if (text == null) return new InyeonStore();
                var lines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
                const string prefix = "jangseoru.inyeon v";
                var header = lines[0];
                if (!header.StartsWith(prefix, StringComparison.Ordinal)) return new InyeonStore();
                var version = header.Substring(prefix.Length);
                if (version.Length == 0 || version[0] == '0' || version.Any(c => c < '0' || c > '9')) return new InyeonStore();
                wasCorrupt = false;
                if (version != "1") return new InyeonStore { ReadOnly = true, futureRaw = text };
                var store = new InyeonStore();
                foreach (var line in lines.Skip(1))
                {
                    if (line.Length == 0 || line.Length > 4096 || line.IndexOf('\0') >= 0) continue;
                    var parts = line.Split('\t');
                    var understood = false;
                    if (parts.Length == 3 && parts[0] == "P" && InyeonCatalog.Accepts(parts[1], parts[2]))
                    { store.RecordPicture(parts[1], parts[2]); understood = true; }
                    else if (parts.Length == 4 && parts[0] == "B" && int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var stage)
                        && stage >= 1 && stage <= 4 && (InyeonCatalog.Find(parts[1])?.BiographyIds.Contains(parts[2]) ?? false))
                    { store.RecordBiography(parts[1], parts[2], stage); understood = true; }
                    if (!understood && store.unknown.Count < 4000) store.unknown.Add(line);
                }
                return store;
            }
            catch { wasCorrupt = true; return new InyeonStore(); }
        }
    }
}
