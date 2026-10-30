using System;

namespace Jangseoru.Core
{
    /// <summary>Works out which gallery cards need their image loaded for the current scroll position.</summary>
    public static class VisibleRange
    {
        /// <summary>
        /// Card index range [first, last] covering the visible rows plus <paramref name="marginRows"/> rows on each side.
        /// Empty list gives first = 0, last = -1. Columns below 1 count as 1; negative margin counts as 0.
        /// Unusable geometry (row height not positive/finite, viewport negative/NaN) returns the whole list so no card stays blank.
        /// </summary>
        public static void Compute(int total, int columns, float rowHeight, float viewportHeight, float scrollOffset,
            int marginRows, out int first, out int last)
        {
            first = 0;
            last = -1;
            if (total <= 0) return;

            last = total - 1;
            if (columns < 1) columns = 1;
            if (marginRows < 0) marginRows = 0;

            bool badGeometry = !(rowHeight > 0f) || float.IsInfinity(rowHeight)
                || !(viewportHeight >= 0f) || float.IsInfinity(viewportHeight)
                || float.IsNaN(scrollOffset) || float.IsInfinity(scrollOffset);
            if (badGeometry) return;

            double scroll = Math.Max(0.0, scrollOffset);
            double firstRow = Math.Floor(scroll / rowHeight) - marginRows;
            double lastRow = Math.Floor((scroll + viewportHeight) / rowHeight) + marginRows;

            double firstIndex = Math.Max(0.0, firstRow) * columns;
            double lastIndex = (lastRow + 1.0) * columns - 1.0;

            int maxIndex = total - 1;
            last = lastIndex >= maxIndex ? maxIndex : (int)Math.Max(0.0, lastIndex);
            first = firstIndex >= last ? last : (int)firstIndex;
        }
    }
}
