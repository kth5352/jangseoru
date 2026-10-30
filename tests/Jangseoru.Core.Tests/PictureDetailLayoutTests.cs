using System;
using Xunit;

namespace Jangseoru.Core.Tests
{
    public class PictureDetailLayoutTests
    {
        // ---- Normalize ----

        [Fact]
        public void Normalize_turns_top_left_pixels_into_bottom_left_anchors()
        {
            var r = PictureDetailLayout.Normalize(1000f, 500f, 100f, 50f, 600f, 450f);
            Assert.Equal(0.1f, r.XMin, 4);
            Assert.Equal(0.6f, r.XMax, 4);
            Assert.Equal(0.1f, r.YMin, 4); // bottom edge 450 from the top = 50 from the bottom
            Assert.Equal(0.9f, r.YMax, 4);
        }

        [Fact]
        public void Normalize_orders_swapped_edges_and_clamps_to_the_frame()
        {
            var r = PictureDetailLayout.Normalize(100f, 100f, 120f, 90f, -10f, 10f);
            Assert.Equal(0f, r.XMin, 4);
            Assert.Equal(1f, r.XMax, 4);
            Assert.Equal(0.1f, r.YMin, 4);
            Assert.Equal(0.9f, r.YMax, 4);
        }

        [Fact]
        public void Normalize_of_an_unusable_frame_is_the_whole_frame()
        {
            var r = PictureDetailLayout.Normalize(0f, 100f, 1f, 2f, 3f, 4f);
            Assert.Equal(0f, r.XMin);
            Assert.Equal(0f, r.YMin);
            Assert.Equal(1f, r.XMax);
            Assert.Equal(1f, r.YMax);
        }

        // ---- geometries ----

        [Fact]
        public void Book_geometry_matches_the_pic_book_1_sprite()
        {
            var g = PictureDetailLayout.Book;
            Assert.Equal(1254f, g.Width);
            Assert.Equal(1080f, g.Height);
            Assert.True(g.HasPlate);
        }

        [Fact]
        public void Paper_geometry_is_two_pic_paper_sheets_side_by_side_without_a_plate()
        {
            var g = PictureDetailLayout.Papers;
            Assert.Equal(2 * PictureDetailLayout.PaperWidth, g.Width);
            Assert.Equal(PictureDetailLayout.PaperHeight, g.Height);
            Assert.False(g.HasPlate);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Picture_sits_on_the_left_page_and_the_caption_on_the_right(bool book)
        {
            var g = book ? PictureDetailLayout.Book : PictureDetailLayout.Papers;
            AssertInside(g.Picture);
            AssertInside(g.Caption);
            Assert.True(g.Picture.XMax <= 0.5f, "picture must stay on the left page");
            Assert.True(g.Caption.XMin >= 0.5f, "caption must stay on the right page");
            if (g.HasPlate)
            {
                AssertInside(g.Plate);
                Assert.True(g.Plate.XMin >= 0.5f);
                Assert.True(g.Caption.YMin >= g.Plate.YMax || g.Caption.XMax <= g.Plate.XMin, "caption must not overlap the plate");
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void A_480x700_illustration_is_shown_at_least_at_two_thirds_of_its_size(bool book)
        {
            var g = book ? PictureDetailLayout.Book : PictureDetailLayout.Papers;
            var boxW = (g.Picture.XMax - g.Picture.XMin) * g.Width;
            var boxH = (g.Picture.YMax - g.Picture.YMin) * g.Height;
            PictureDetailLayout.Fit(480f, 700f, boxW, boxH, out var w, out var h);
            Assert.True(h >= 700f * 2f / 3f, "height " + h);
            Assert.True(w <= boxW + 0.01f && h <= boxH + 0.01f);
        }

        private static void AssertInside(NormRect r)
        {
            Assert.True(r.XMin >= 0f && r.XMax <= 1f && r.YMin >= 0f && r.YMax <= 1f);
            Assert.True(r.XMax > r.XMin && r.YMax > r.YMin);
        }

        // ---- Fit ----

        [Fact]
        public void Fit_keeps_the_aspect_ratio_and_touches_the_limiting_side()
        {
            PictureDetailLayout.Fit(480f, 700f, 480f, 780f, out var w, out var h);
            Assert.Equal(480f, w, 3);
            Assert.Equal(700f, h, 3);

            PictureDetailLayout.Fit(480f, 700f, 1000f, 350f, out w, out h);
            Assert.Equal(240f, w, 3);
            Assert.Equal(350f, h, 3);
        }

        [Fact]
        public void Fit_scales_up_to_fill_a_larger_box()
        {
            PictureDetailLayout.Fit(1254f, 1080f, 1920f * 0.92f, 1080f * 0.92f, out var w, out var h);
            Assert.Equal(1080f * 0.92f, h, 2);
            Assert.Equal(1254f * 0.92f, w, 2);
        }

        [Theory]
        [InlineData(0f, 700f)]
        [InlineData(480f, 0f)]
        [InlineData(float.NaN, 700f)]
        [InlineData(480f, float.PositiveInfinity)]
        [InlineData(-480f, 700f)]
        public void Fit_of_an_unusable_source_fills_the_box(float srcW, float srcH)
        {
            PictureDetailLayout.Fit(srcW, srcH, 300f, 200f, out var w, out var h);
            Assert.Equal(300f, w);
            Assert.Equal(200f, h);
        }

        [Fact]
        public void Fit_into_an_unusable_box_is_empty()
        {
            PictureDetailLayout.Fit(480f, 700f, 0f, 200f, out var w, out var h);
            Assert.Equal(0f, w);
            Assert.Equal(0f, h);
            PictureDetailLayout.Fit(480f, 700f, float.NaN, 200f, out w, out h);
            Assert.Equal(0f, w);
            Assert.Equal(0f, h);
        }

        // ---- Caption ----

        [Fact]
        public void Caption_gives_the_number_within_the_group_and_the_group_name()
        {
            var keys = new[] { "pic_girl_event_003", "pic_walk_001", "pic_girl_event_101", "pic_walk_002" };
            Assert.True(PictureDetailLayout.TryCaption(keys, "pic_girl_event_101", out var number, out var group));
            Assert.Equal("2", number);
            Assert.Equal("girl event", group);

            Assert.True(PictureDetailLayout.TryCaption(keys, "pic_walk_001", out number, out group));
            Assert.Equal("1", number);
            Assert.Equal("walk", group);
        }

        [Fact]
        public void Caption_of_a_single_card_group_has_no_number()
        {
            Assert.True(PictureDetailLayout.TryCaption(new[] { "pic_firebird_001", "pic_walk_001" }, "pic_firebird_001", out var number, out var group));
            Assert.Equal("", number);
            Assert.Equal("firebird", group);
        }

        [Fact]
        public void Caption_matches_the_card_label_for_every_catalog_key()
        {
            var labels = GalleryLayout.EventLabels(PictureCatalog.Keys);
            for (var i = 0; i < PictureCatalog.Count; i++)
            {
                Assert.True(PictureDetailLayout.TryCaption(PictureCatalog.Keys, PictureCatalog.Keys[i], out var number, out var group));
                Assert.Equal(labels[i], number.Length == 0 ? group : group + " " + number);
            }
        }

        [Fact]
        public void Caption_of_an_unknown_key_is_false_with_empty_texts()
        {
            Assert.False(PictureDetailLayout.TryCaption(new[] { "pic_walk_001" }, "pic_other_001", out var number, out var group));
            Assert.Equal("", number);
            Assert.Equal("", group);
            Assert.False(PictureDetailLayout.TryCaption(null, "pic_walk_001", out number, out group));
            Assert.False(PictureDetailLayout.TryCaption(new[] { "pic_walk_001" }, null, out number, out group));
            Assert.Equal("", number);
            Assert.Equal("", group);
        }

        // ---- Vertical ----

        [Fact]
        public void Vertical_puts_one_character_per_line()
        {
            Assert.Equal("사\n건", PictureDetailLayout.Vertical("사건"));
            Assert.Equal("", PictureDetailLayout.Vertical(""));
            Assert.Equal("", PictureDetailLayout.Vertical(null));
            Assert.Equal("a", PictureDetailLayout.Vertical("a"));
        }

        [Fact]
        public void Notice_and_plate_texts_are_the_agreed_korean_strings()
        {
            Assert.Equal("이미지를 불러오지 못했습니다", PictureDetailLayout.LoadFailedText);
            Assert.Equal("사건", PictureDetailLayout.PlateText);
        }
    }
}
