using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Jangseoru.Core
{
    /// <summary>One oath card of the gallery: its game id, 1-based display number and whether it was obtained.</summary>
    public sealed class OathCard
    {
        public string Id { get; }
        public int Number { get; }
        public bool Unlocked { get; }

        public OathCard(string id, int number, bool unlocked)
        {
            Id = id;
            Number = number;
            Unlocked = unlocked;
        }
    }

    /// <summary>Pure model for the oath tab: ordering, numbering and progress text.</summary>
    public static class OathModel
    {
        /// <summary>
        /// Builds cards ordered by numeric id; ids that are not plain non-negative integers are ignored.
        /// A card is unlocked when its count is positive.
        /// </summary>
        public static IReadOnlyList<OathCard> Build(IEnumerable<KeyValuePair<string, int>> idAndCount)
        {
            var result = new List<OathCard>();
            if (idAndCount == null) return result;

            var parsed = new List<KeyValuePair<long, KeyValuePair<string, int>>>();
            foreach (var pair in idAndCount)
            {
                if (pair.Key == null) continue;
                if (!long.TryParse(pair.Key, NumberStyles.None, CultureInfo.InvariantCulture, out var n)) continue;
                parsed.Add(new KeyValuePair<long, KeyValuePair<string, int>>(n, pair));
            }

            var ordered = parsed.OrderBy(p => p.Key).ToList();
            for (int i = 0; i < ordered.Count; i++)
                result.Add(new OathCard(ordered[i].Value.Key, i + 1, ordered[i].Value.Value > 0));
            return result;
        }

        /// <summary>
        /// The same cards with the mod's layer applied: an oath is unlocked when the F11 popup opened it, or when the game's count is
        /// above the baseline taken at the last reset (no store: the game's count alone).
        /// </summary>
        public static IReadOnlyList<OathCard> Build(IEnumerable<KeyValuePair<string, int>> idAndCount, OathStore overlay)
        {
            var plain = Build(idAndCount);
            if (overlay == null || idAndCount == null) return plain;
            var counts = new Dictionary<string, int>();
            foreach (var pair in idAndCount)
                if (pair.Key != null && !counts.ContainsKey(pair.Key)) counts[pair.Key] = pair.Value;
            return plain.Select(c => new OathCard(c.Id, c.Number,
                overlay.Opened(c.Id) || (counts.TryGetValue(c.Id, out var n) && n > overlay.Baseline(c.Id)))).ToList();
        }

        public static string Progress(int unlocked, int total)
        {
            return unlocked.ToString(CultureInfo.InvariantCulture) + "/" + total.ToString(CultureInfo.InvariantCulture);
        }
    }
}
