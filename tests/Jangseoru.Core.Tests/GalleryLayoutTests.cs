using System.Linq;
using Xunit;

namespace Jangseoru.Core.Tests
{
    public class GalleryLayoutTests
    {
        [Theory]
        [InlineData(908.4f, 6)]
        [InlineData(720f, 6)]
        [InlineData(1280f, 6)]
        [InlineData(908.4f, 3)]
        public void Category_tabs_fit_before_reserved_progress_counter(float stripWidth, int count)
        {
            const float reserved = 146f, gap = 10f;
            var width = GalleryLayout.CategoryTabWidth(stripWidth, count, reserved, gap, 150f);
            Assert.InRange(width, 68f, 150f); // two Korean glyphs at the existing font size remain readable
            Assert.True(count * width + (count - 1) * gap <= stripWidth - reserved + .01f);
            if (count == 3) Assert.Equal(150f, width);
        }

        [Fact]
        public void Tab_font_shrinks_so_the_longest_label_fits_its_tab()
        {
            Assert.Equal(34, GalleryLayout.TabFontSize(150f, 2, 34, 18)); // roomy tabs keep the preferred size
            var narrow = GalleryLayout.TabFontSize(76f, 3, 34, 18);
            Assert.InRange(narrow, 18, 26); // three glyphs in 76 units must be smaller than they are in 150
            Assert.True(narrow * 3 <= 76f * 1.05f);
        }

        [Theory]
        [InlineData(0f, 3)]
        [InlineData(-5f, 2)]
        [InlineData(float.NaN, 2)]
        [InlineData(76f, 0)]
        public void Tab_font_never_goes_below_the_minimum_or_above_the_preferred(float width, int length)
        {
            Assert.InRange(GalleryLayout.TabFontSize(width, length, 34, 18), 18, 34);
        }

        // ---- Columns ----

        [Fact]
        public void Columns_uses_a_fixed_column_count_when_the_grid_has_one()
        {
            Assert.Equal(4, GalleryLayout.Columns(true, 4, 0f, 0f, 0f, 0f, 1));
        }

        [Fact]
        public void Columns_is_derived_from_content_width_otherwise()
        {
            // EndGame grid: 906 wide content, 200 cells, ~6 spacing, 12+12 padding -> 4 columns.
            Assert.Equal(4, GalleryLayout.Columns(false, 0, 906f, 200f, 6f, 24f, 1));
        }

        [Fact]
        public void Columns_falls_back_when_geometry_is_unusable()
        {
            Assert.Equal(4, GalleryLayout.Columns(false, 0, 0f, 200f, 6f, 0f, 4));
            Assert.Equal(4, GalleryLayout.Columns(false, 0, 900f, 0f, 6f, 0f, 4));
            Assert.Equal(4, GalleryLayout.Columns(false, 0, float.NaN, 200f, 6f, 0f, 4));
            Assert.Equal(1, GalleryLayout.Columns(false, 0, 0f, 0f, 0f, 0f, 0));
            Assert.Equal(3, GalleryLayout.Columns(true, 0, 0f, 0f, 0f, 0f, 3)); // fixed but count 0
        }

        // ---- ThumbnailWindow ----

        [Fact]
        public void Window_before_layout_is_valid_covers_only_the_first_three_rows()
        {
            GalleryLayout.ThumbnailWindow(305, 4, 0f, 0f, 0f, 1, out var first, out var last);
            Assert.Equal(0, first);
            Assert.Equal(11, last);

            GalleryLayout.ThumbnailWindow(305, 4, 298f, 0f, 0f, 1, out first, out last); // viewport not laid out yet
            Assert.Equal(0, first);
            Assert.Equal(11, last);

            GalleryLayout.ThumbnailWindow(305, 4, float.NaN, 600f, 0f, 1, out first, out last);
            Assert.Equal(0, first);
            Assert.Equal(11, last);
        }

        [Fact]
        public void Window_before_layout_is_valid_is_clamped_to_a_short_list()
        {
            GalleryLayout.ThumbnailWindow(5, 4, 0f, 0f, 0f, 1, out var first, out var last);
            Assert.Equal(0, first);
            Assert.Equal(4, last);
        }

        [Fact]
        public void Window_with_valid_layout_matches_VisibleRange()
        {
            GalleryLayout.ThumbnailWindow(305, 4, 298f, 584f, 1500f, 1, out var first, out var last);
            VisibleRange.Compute(305, 4, 298f, 584f, 1500f, 1, out var f2, out var l2);
            Assert.Equal(f2, first);
            Assert.Equal(l2, last);
            Assert.True(first > 0);
            Assert.True(last < 304);
        }

        [Fact]
        public void Window_of_an_empty_list_is_empty()
        {
            GalleryLayout.ThumbnailWindow(0, 4, 0f, 0f, 0f, 1, out var first, out var last);
            Assert.Equal(0, first);
            Assert.Equal(-1, last);
            GalleryLayout.ThumbnailWindow(0, 4, 298f, 584f, 0f, 1, out first, out last);
            Assert.Equal(0, first);
            Assert.Equal(-1, last);
        }

        [Fact]
        public void Window_treats_zero_columns_as_one()
        {
            GalleryLayout.ThumbnailWindow(305, 0, 0f, 0f, 0f, 1, out var first, out var last);
            Assert.Equal(0, first);
            Assert.Equal(2, last);
        }

        // ---- EventLabels ----

        [Fact]
        public void EventLabels_number_cards_within_their_group()
        {
            var labels = GalleryLayout.EventLabels(new[] { "pic_girl_event_003", "pic_walk_001", "pic_girl_event_101", "pic_walk_002" });
            Assert.Equal(new[] { "girl event 1", "walk 1", "girl event 2", "walk 2" }, labels.ToArray());
        }

        [Fact]
        public void EventLabels_leave_single_card_groups_unnumbered()
        {
            var labels = GalleryLayout.EventLabels(new[] { "pic_firebird_001", "pic_mystery" });
            Assert.Equal(new[] { "firebird", "mystery" }, labels.ToArray());
        }

        [Fact]
        public void EventLabels_handle_null_and_empty_input()
        {
            Assert.Empty(GalleryLayout.EventLabels(null));
            Assert.Equal(new[] { "", "" }, GalleryLayout.EventLabels(new string[] { null, "" }).ToArray());
        }

        [Fact]
        public void EventLabels_cover_the_whole_catalog_one_to_one()
        {
            var labels = GalleryLayout.EventLabels(PictureCatalog.Keys);
            Assert.Equal(PictureCatalog.Count, labels.Count);
            Assert.All(labels, l => Assert.False(string.IsNullOrWhiteSpace(l)));
        }

        // ---- InsetTop ----

        [Theory]
        [InlineData(0f)]
        [InlineData(0.5f)]
        [InlineData(1f)]
        public void InsetTop_moves_the_top_edge_down_and_keeps_the_bottom(float pivotY)
        {
            const float pos = 368f, height = 640.9f, inset = 56f;
            GalleryLayout.InsetTop(pos, height, pivotY, inset, out var newPos, out var newHeight);

            Assert.Equal(height - inset, newHeight, 3);
            Assert.Equal(pos - pivotY * height, newPos - pivotY * newHeight, 3);                    // bottom unchanged
            Assert.Equal(pos + (1 - pivotY) * height - inset, newPos + (1 - pivotY) * newHeight, 3); // top moved by inset
        }

        [Fact]
        public void InsetTop_never_makes_the_height_negative()
        {
            GalleryLayout.InsetTop(0f, 30f, 1f, 56f, out _, out var newHeight);
            Assert.Equal(0f, newHeight);
        }
    }
}
