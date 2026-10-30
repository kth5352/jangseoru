using System;
using System.Collections.Generic;
using System.Globalization;

namespace Jangseoru.Core
{
    /// <summary>
    /// Pure layout and labelling decisions for the Jangseoru card grid, kept out of the Unity code so they can be tested.
    /// </summary>
    public static class GalleryLayout
    {
        /// <summary>Fits all category buttons before the progress counter's reserved area.</summary>
        public static float CategoryTabWidth(float stripWidth, int count, float reservedWidth, float gap, float preferredWidth)
        {
            if (count <= 0 || float.IsNaN(stripWidth) || float.IsInfinity(stripWidth)) return 0f;
            var available = Math.Max(0f, stripWidth - reservedWidth - Math.Max(0, count - 1) * gap);
            return Math.Max(0f, Math.Min(preferredWidth, available / count));
        }

        /// <summary>
        /// The font size that lets a label of <paramref name="longestLabel"/> glyphs fit a tab of <paramref name="tabWidth"/>
        /// (about one glyph per size unit), between <paramref name="minimum"/> and <paramref name="preferred"/>.
        /// </summary>
        public static int TabFontSize(float tabWidth, int longestLabel, int preferred, int minimum)
        {
            if (float.IsNaN(tabWidth) || float.IsInfinity(tabWidth) || tabWidth <= 0f || longestLabel <= 0) return Math.Max(minimum, Math.Min(preferred, preferred));
            var fit = (int)Math.Floor(tabWidth * 1.05f / longestLabel);
            return Math.Max(minimum, Math.Min(preferred, fit));
        }

        /// <summary>Rows whose thumbnails are loaded before the grid has a usable layout.</summary>
        public const int EagerRows = 3;

        /// <summary>
        /// Column count of a grid: the fixed count when the grid has one, otherwise as many cells as fit the content width.
        /// Unusable geometry gives <paramref name="fallback"/> (at least 1).
        /// </summary>
        public static int Columns(bool fixedColumnCount, int constraintCount, float contentWidth, float cellWidth,
            float spacingX, float paddingHorizontal, int fallback)
        {
            if (fallback < 1) fallback = 1;
            if (fixedColumnCount) return constraintCount > 0 ? constraintCount : fallback;

            if (!(cellWidth > 0f) || !(contentWidth > 0f) || float.IsInfinity(contentWidth) || float.IsInfinity(cellWidth)
                || float.IsNaN(spacingX) || float.IsNaN(paddingHorizontal))
                return fallback;

            double step = cellWidth + Math.Max(0f, spacingX);
            double n = Math.Floor((contentWidth - Math.Max(0f, paddingHorizontal) + Math.Max(0f, spacingX)) / step);
            return n >= 1 ? (int)Math.Min(n, 1000) : fallback;
        }

        /// <summary>
        /// Card range whose thumbnails should be loaded. Until the layout is valid (row height and viewport height both
        /// positive and finite) only the first <see cref="EagerRows"/> rows; afterwards <see cref="VisibleRange.Compute"/>.
        /// </summary>
        public static void ThumbnailWindow(int total, int columns, float rowHeight, float viewportHeight, float scrollOffset,
            int marginRows, out int first, out int last)
        {
            first = 0;
            last = -1;
            if (total <= 0) return;
            if (columns < 1) columns = 1;

            bool layoutValid = rowHeight > 0f && !float.IsInfinity(rowHeight) && viewportHeight > 0f && !float.IsInfinity(viewportHeight);
            if (!layoutValid)
            {
                last = (int)Math.Min((long)total, (long)columns * EagerRows) - 1;
                return;
            }

            VisibleRange.Compute(total, columns, rowHeight, viewportHeight, scrollOffset, marginRows, out first, out last);
        }

        /// <summary>
        /// Card captions for event pictures: the group name (underscores as spaces) plus the card's 1-based position within
        /// its group; groups with a single card get no number. Same length and order as the input.
        /// </summary>
        public static IReadOnlyList<string> EventLabels(IReadOnlyList<string> catalogKeys)
        {
            var result = new List<string>();
            if (catalogKeys == null) return result;

            var groups = new string[catalogKeys.Count];
            var totals = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < catalogKeys.Count; i++)
            {
                var g = PictureCatalog.Group(catalogKeys[i]).Replace('_', ' ').Trim();
                groups[i] = g;
                if (g.Length == 0) continue;
                totals[g] = totals.TryGetValue(g, out var n) ? n + 1 : 1;
            }

            var seen = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < groups.Length; i++)
            {
                var g = groups[i];
                if (g.Length == 0) { result.Add(string.Empty); continue; }
                var ordinal = seen.TryGetValue(g, out var s) ? s + 1 : 1;
                seen[g] = ordinal;
                result.Add(totals[g] > 1 ? g + " " + ordinal.ToString(CultureInfo.InvariantCulture) : g);
            }
            return result;
        }

        /// <summary>One card of the 사건 list.</summary>
        public sealed class EventEntry
        {
            /// <summary>Catalog key the card stands for (its thumbnail, and what a click passes on).</summary>
            public string Key { get; }
            public string Label { get; }
            /// <summary>The card is unlocked when this catalog key was collected.</summary>
            public string UnlockKey { get; }
            /// <summary>Set when the card is a whole bubble presentation.</summary>
            public BubbleSet Set { get; }
            /// <summary>Set when the card is an overlay presentation (combat effects, cut in).</summary>
            public OverlaySet Overlay { get; }

            internal EventEntry(string key, string label, string unlockKey, BubbleSet set, OverlaySet overlay = null)
            {
                Key = key;
                Label = label;
                UnlockKey = unlockKey;
                Set = set;
                Overlay = overlay;
            }
        }

        /// <summary>
        /// The 사건 list: one card per catalog picture, except that each bubble presentation is one card (its other frames are
        /// left out). Order follows the catalog.
        /// </summary>
        public static IReadOnlyList<EventEntry> EventEntries(IReadOnlyList<string> catalogKeys)
        {
            var result = new List<EventEntry>();
            if (catalogKeys == null) return result;
            var labels = EventLabels(catalogKeys);
            for (int i = 0; i < catalogKeys.Count; i++)
            {
                var key = catalogKeys[i];
                var set = BubbleSets.ForCatalogKey(key);
                var overlay = OverlaySets.ForCardKey(key);
                if (overlay != null) result.Add(new EventEntry(key, overlay.Label, overlay.UnlockKey, null, overlay));
                else if (OverlaySets.IsHiddenFrame(key)) continue;
                else if (set == null) result.Add(new EventEntry(key, labels[i], key, null));
                else if (string.Equals(set.CardKey, key, StringComparison.Ordinal)) result.Add(new EventEntry(key, set.Label, set.UnlockKey, set));
            }
            return result;
        }

        /// <summary>
        /// Moves a rect's top edge down by <paramref name="inset"/> while keeping its bottom edge, for any vertical pivot.
        /// The height never goes below zero.
        /// </summary>
        public static void InsetTop(float posY, float height, float pivotY, float inset, out float newPosY, out float newHeight)
        {
            newHeight = Math.Max(0f, height - inset);
            newPosY = posY - pivotY * (height - newHeight);
        }
    }
}
