using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Jangseoru.Core
{
    public sealed class CharacterDefinition
    {
        public string Id { get; internal set; }
        public string NameId { get; internal set; }
        public string HeadIcon { get; internal set; }
        public IReadOnlyList<string> BiographyIds { get; internal set; }
        public IReadOnlyList<string> Pictures { get; internal set; }
    }

    public sealed class RelationshipIdentity
    {
        public string CharacterId { get; internal set; }
        public string TextId { get; internal set; }
    }

    /// <summary>Explicit identities extracted from the game's data. Image folders are not runtime identity heuristics.</summary>
    public static class InyeonCatalog
    {
        private sealed class Data
        {
            internal readonly Dictionary<string, CharacterDefinition> People = new Dictionary<string, CharacterDefinition>(StringComparer.Ordinal);
            internal readonly Dictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.Ordinal);
            internal readonly Dictionary<int, RelationshipIdentity> Relations = new Dictionary<int, RelationshipIdentity>();
            internal readonly Dictionary<string, string> Addresses = new Dictionary<string, string>(StringComparer.Ordinal);
            /// <summary>Which person each known picture belongs to (built-in and learned).</summary>
            internal readonly Dictionary<string, string> Owners = new Dictionary<string, string>(StringComparer.Ordinal);
            internal IReadOnlyList<CharacterDefinition> List = Array.AsReadOnly(new CharacterDefinition[0]);
        }

        private static readonly Lazy<Data> data = new Lazy<Data>(Load);
        private static readonly object Sync = new object();
        private static readonly HashSet<string> learned = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, List<string>> learnedByPerson = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private static Dictionary<string, string[]> builtIn;
        /// <summary>Portraits learned from the running game, in total; a damaged file or odd game state cannot grow the catalog without end.</summary>
        public const int MaxLearned = 3000;
        private const string CharacterImages = "Assets/__Project/Images/Characters/";
        public static IReadOnlyList<CharacterDefinition> Characters => data.Value.List;
        public static CharacterDefinition Find(string id) => id != null && data.Value.People.TryGetValue(id, out var c) ? c : null;
        public static string ResolveStory(string id) => id != null && data.Value.Aliases.TryGetValue(id, out var c) ? c : null;
        public static RelationshipIdentity Relationship(int value) => data.Value.Relations.TryGetValue(value, out var r) ? r : null;
        public static bool IsPicture(string id, string key) => key != null && (Find(id)?.Pictures.Contains(key) ?? false);

        /// <summary>
        /// True for a portrait of the catalog: a built-in or learned one, or a new image of a person the catalog already knows (a game
        /// update's new expression or outfit). The person is never guessed: an unknown person is refused, and so is any path outside
        /// the character images (the generic <c>Misc_</c> folders were left out of the catalog on purpose).
        /// </summary>
        public static bool Accepts(string id, string key) => IsPicture(id, key) || (Find(id) != null && IsNewCharacterImage(key));

        /// <summary>A character image no person owns yet: an image the catalog knows for someone else is never moved to another person.</summary>
        private static bool IsNewCharacterImage(string key) => IsCharacterImage(key) && !data.Value.Owners.ContainsKey(key);

        /// <summary>Adds a new portrait to a known person's pictures. False when it is not accepted, already there, or the cap is reached.</summary>
        public static bool Learn(string id, string key)
        {
            lock (Sync)
            {
                var person = Find(id);
                if (person == null || !IsNewCharacterImage(key) || learned.Count >= MaxLearned) return false;
                if (builtIn == null) builtIn = data.Value.People.ToDictionary(p => p.Key, p => p.Value.Pictures.ToArray(), StringComparer.Ordinal);
                learned.Add(id + "\t" + key);
                data.Value.Owners[key] = id;
                if (!learnedByPerson.TryGetValue(id, out var list)) learnedByPerson[id] = list = new List<string>();
                list.Add(key);
                person.Pictures = Array.AsReadOnly(Order(person.Pictures.Concat(new[] { key })));
                return true;
            }
        }

        /// <summary>Drops everything learned since startup (tests).</summary>
        internal static void ForgetLearned()
        {
            lock (Sync)
            {
                if (builtIn != null)
                    foreach (var pair in builtIn)
                        if (data.Value.People.TryGetValue(pair.Key, out var person)) person.Pictures = Array.AsReadOnly(pair.Value);
                foreach (var keys in learnedByPerson.Values)
                    foreach (var key in keys) data.Value.Owners.Remove(key);
                learned.Clear();
                learnedByPerson.Clear();
            }
        }

        private static string[] Order(IEnumerable<string> keys)
            => keys.Distinct(StringComparer.Ordinal).OrderBy(k => k.EndsWith("/normal.png", StringComparison.Ordinal) ? 0 : 1)
                .ThenBy(k => k, StringComparer.Ordinal).ToArray();

        private static bool IsCharacterImage(string key)
        {
            if (key == null || key.Length > 300 || !key.StartsWith(CharacterImages, StringComparison.Ordinal)
                || !key.EndsWith(".png", StringComparison.Ordinal) || key.IndexOf("..", StringComparison.Ordinal) >= 0
                || key.IndexOf("/Misc_", StringComparison.Ordinal) >= 0) return false;
            foreach (var c in key)
                if (c < ' ' || c == '\\' || c == '\u007f') return false; // control characters (tab, newline) and backslashes
            return true;
        }
        public static string ResolveAddress(string key)
        {
            if (key == null) return null;
            if (data.Value.Addresses.TryGetValue(key, out var path)) return path;
            var sub = key.IndexOf('[');
            return sub > 0 && key.EndsWith("]", StringComparison.Ordinal) && data.Value.Addresses.TryGetValue(key.Substring(0, sub), out path) ? path : null;
        }

        private static Data Load()
        {
            var d = new Data();
            try
            {
                var asm = typeof(InyeonCatalog).Assembly;
                var resource = asm.GetManifestResourceNames().Single(n => n.EndsWith(".inyeon.tsv", StringComparison.Ordinal));
                var pictures = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                using (var stream = asm.GetManifestResourceStream(resource))
                using (var reader = new StreamReader(stream))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        var p = line.Split('\t');
                        if (p[0] == "C" && p.Length == 5)
                        {
                            d.People[p[1]] = new CharacterDefinition { Id = p[1], NameId = p[2], HeadIcon = p[3],
                                BiographyIds = Array.AsReadOnly(p[4].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)) };
                            pictures[p[1]] = new List<string>();
                        }
                        else if (p[0] == "P" && p.Length == 3 && pictures.TryGetValue(p[1], out var list)) list.Add(p[2]);
                        else if (p[0] == "A" && p.Length == 3) d.Aliases[p[1]] = p[2];
                        else if (p[0] == "K" && p.Length == 3) d.Addresses[p[1]] = p[2];
                        else if (p[0] == "R" && p.Length == 4 && int.TryParse(p[1], out var number))
                            d.Relations[number] = new RelationshipIdentity { CharacterId = p[2], TextId = p[3] };
                    }
                }
                foreach (var c in d.People.Values)
                    c.Pictures = Array.AsReadOnly(Order(pictures[c.Id]));
                foreach (var c in d.People.Values)
                    foreach (var key in c.Pictures)
                        if (!d.Owners.ContainsKey(key)) d.Owners[key] = c.Id;
                var familiar = new[] { "master", "brother1", "brother2", "brother3", "brother4", "sister1", "player" };
                d.List = Array.AsReadOnly(d.People.Values.OrderBy(c => Array.IndexOf(familiar, c.Id) is var rank && rank >= 0 ? rank : 100)
                    .ThenBy(c => c.BiographyIds.Count > 0 ? 0 : 1).ThenBy(c => c.Id, StringComparer.Ordinal).ToArray());
                return d;
            }
            catch { return new Data(); }
        }
    }
}
