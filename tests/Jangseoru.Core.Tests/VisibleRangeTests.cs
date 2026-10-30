using Xunit;

namespace Jangseoru.Core.Tests
{
    public class VisibleRangeTests
    {
        [Fact]
        public void Top_of_list_shows_first_rows_plus_margin()
        {
            VisibleRange.Compute(305, 4, 300f, 640f, 0f, 1, out var first, out var last);

            Assert.Equal(0, first);
            Assert.Equal(4 * (3 + 1) - 1, last); // 15
        }

        [Fact]
        public void Scrolled_to_the_middle_is_clamped_both_ends()
        {
            // rows: floor(1500/300)=5 .. floor(2140/300)=7, margin 1 -> rows 4..8
            VisibleRange.Compute(305, 4, 300f, 640f, 1500f, 1, out var first, out var last);
            Assert.Equal(16, first);
            Assert.Equal(35, last);

            // near the top the lower end clamps at 0
            VisibleRange.Compute(305, 4, 300f, 640f, 300f, 2, out first, out last);
            Assert.Equal(0, first);
            Assert.Equal(23, last);
        }

        [Fact]
        public void Past_the_end_clamps_to_last_card()
        {
            VisibleRange.Compute(10, 4, 300f, 640f, 99999f, 1, out var first, out var last);
            Assert.True(first >= 0 && first <= 9);
            Assert.Equal(9, last);

            VisibleRange.Compute(305, 4, 300f, 640f, 1e9f, 1, out first, out last);
            Assert.Equal(304, last);
            Assert.InRange(first, 0, 304);
        }

        [Fact]
        public void Empty_list_gives_empty_range()
        {
            VisibleRange.Compute(0, 4, 300f, 640f, 0f, 1, out var first, out var last);
            Assert.Equal(0, first);
            Assert.Equal(-1, last);

            VisibleRange.Compute(-5, 4, 300f, 640f, 0f, 1, out first, out last);
            Assert.Equal(0, first);
            Assert.Equal(-1, last);
        }

        [Fact]
        public void Single_card_and_zero_columns_do_not_throw()
        {
            VisibleRange.Compute(1, 0, 300f, 640f, 0f, 1, out var first, out var last);
            Assert.Equal(0, first);
            Assert.Equal(0, last);

            // columns <= 0 behaves like 1 column
            VisibleRange.Compute(100, 0, 300f, 640f, 0f, 0, out first, out last);
            Assert.Equal(0, first);
            Assert.Equal(2, last); // rows 0..floor(640/300)=2
        }

        [Fact]
        public void Invalid_geometry_returns_whole_list_so_nothing_stays_blank()
        {
            VisibleRange.Compute(50, 4, 0f, 640f, 0f, 1, out var first, out var last);
            Assert.Equal(0, first);
            Assert.Equal(49, last);

            VisibleRange.Compute(50, 4, 300f, -1f, 0f, 1, out first, out last);
            Assert.Equal(0, first);
            Assert.Equal(49, last);

            VisibleRange.Compute(50, 4, float.NaN, 640f, 0f, 1, out first, out last);
            Assert.Equal(0, first);
            Assert.Equal(49, last);
        }

        [Fact]
        public void Negative_scroll_and_margin_are_treated_as_zero()
        {
            VisibleRange.Compute(305, 4, 300f, 640f, -500f, -3, out var first, out var last);
            Assert.Equal(0, first);
            Assert.Equal(11, last); // rows 0..2
        }
    }
}
