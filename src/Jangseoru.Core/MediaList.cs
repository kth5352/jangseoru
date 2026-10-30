using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Jangseoru.Core
{
    public sealed class MediaRow
    {
        public MediaKind Kind { get; set; }
        public string Key { get; set; }
        public string Address { get; set; }
        public bool Unlocked { get; set; }
        /// <summary>The card number (1-based position in the tab), or <see cref="MediaList.Hidden"/> while locked.</summary>
        public string Label { get; set; }
    }

    /// <summary>The cards of a media tab: every catalog entry in catalog order, opened when the store has seen it.</summary>
    public static class MediaList
    {
        public const string Hidden = "???";

        public static IReadOnlyList<MediaRow> Build(MediaKind kind, MediaStore store)
        {
            var rows = new List<MediaRow>();
            var number = 0;
            foreach (var entry in MediaCatalog.Entries(kind))
            {
                number++;
                var unlocked = store != null && store.Has(kind, entry.Key);
                rows.Add(new MediaRow
                {
                    Kind = kind,
                    Key = entry.Key,
                    Address = entry.Address,
                    Unlocked = unlocked,
                    Label = unlocked ? number.ToString(CultureInfo.InvariantCulture) : Hidden,
                });
            }
            return rows;
        }

        /// <summary>"n/total", the same shape as the other tabs' progress.</summary>
        public static string Progress(IReadOnlyList<MediaRow> rows)
        {
            if (rows == null) return OathModel.Progress(0, 0);
            return OathModel.Progress(rows.Count(r => r.Unlocked), rows.Count);
        }
    }
}
