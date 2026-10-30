using System.Collections.Generic;
using System.Linq;

namespace Jangseoru.Core
{
    /// <summary>One game item as the list model sees it (no Unity types).</summary>
    public sealed class ItemSource
    {
        public ItemKind Kind { get; set; }
        public string Id { get; set; }
        /// <summary>False for development entries without a picture; they are left out of the list.</summary>
        public bool HasImage { get; set; }
        /// <summary>The translated name; null or empty when the game's text could not be read.</summary>
        public string Name { get; set; }
    }

    public sealed class ItemRow
    {
        public ItemKind Kind { get; set; }
        public string Id { get; set; }
        public bool Unlocked { get; set; }
        /// <summary>The name, or <see cref="ItemList.Hidden"/> while locked.</summary>
        public string Label { get; set; }
    }

    /// <summary>The rows of an item tab: which game items appear, in which order, and what their names show.</summary>
    public static class ItemList
    {
        public const string Hidden = "???";

        /// <summary>
        /// Items of <paramref name="kind"/> in the order given (the game's order). Items without a picture are left out. An item
        /// is unlocked when the store has it and its name could be read; otherwise its label is <see cref="Hidden"/>.
        /// </summary>
        public static IReadOnlyList<ItemRow> Build(ItemKind kind, IReadOnlyList<ItemSource> sources, ItemStore store)
        {
            var rows = new List<ItemRow>();
            if (sources == null) return rows;
            foreach (var source in sources)
            {
                if (source == null || source.Kind != kind || !source.HasImage) continue;
                var unlocked = store != null && store.Has(kind, source.Id) && !string.IsNullOrEmpty(source.Name);
                rows.Add(new ItemRow
                {
                    Kind = kind,
                    Id = source.Id,
                    Unlocked = unlocked,
                    Label = unlocked ? source.Name : Hidden,
                });
            }
            return rows;
        }

        /// <summary>"n/total", the same shape as the other tabs' progress.</summary>
        public static string Progress(IReadOnlyList<ItemRow> rows)
        {
            if (rows == null) return OathModel.Progress(0, 0);
            return OathModel.Progress(rows.Count(r => r.Unlocked), rows.Count);
        }
    }
}
