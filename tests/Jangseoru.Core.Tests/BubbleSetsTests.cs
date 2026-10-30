using System.Linq;
using Xunit;

namespace Jangseoru.Core.Tests
{
    public class BubbleSetsTests
    {
        [Fact]
        public void Every_frame_key_is_in_the_catalog()
        {
            foreach (var set in BubbleSets.All)
                foreach (var f in set.Frames)
                    Assert.True(PictureCatalog.Contains(f.CatalogKey), f.CatalogKey);
        }

        [Fact]
        public void Sets_have_four_and_six_frames()
        {
            Assert.Equal(4, BubbleSets.Four.Frames.Count);
            Assert.Equal(6, BubbleSets.Six.Frames.Count);
            Assert.Equal("pic_event_bubble_4", BubbleSets.Four.UnlockKey);
            Assert.Equal("pic_event_bubble_105", BubbleSets.Six.UnlockKey);
        }

        [Fact]
        public void TryFromEffect_only_accepts_started_bubble_effects()
        {
            Assert.True(BubbleSets.TryFromEffect("EventBubble_105", 1, out var key));
            Assert.Equal("pic_event_bubble_105", key);
            Assert.True(BubbleSets.TryFromEffect("EventBubble_003", 1, out key));
            Assert.Equal("pic_event_bubble_3", key);
            Assert.False(BubbleSets.TryFromEffect("EventBubble_105", 0, out _)); // the script stops them with play = 0
            Assert.False(BubbleSets.TryFromEffect("Hit_001", 1, out _));
            Assert.False(BubbleSets.TryFromEffect(null, 1, out _));
            Assert.False(BubbleSets.TryFromEffect("", 1, out _));
        }

        [Fact]
        public void ForCatalogKey_and_hidden_frames()
        {
            Assert.Same(BubbleSets.Six, BubbleSets.ForCatalogKey("pic_event_bubble_103"));
            Assert.Same(BubbleSets.Four, BubbleSets.ForCatalogKey("pic_event_bubble_2"));
            Assert.Null(BubbleSets.ForCatalogKey("pic_walk_001"));
            Assert.Null(BubbleSets.ForCatalogKey(null));
            Assert.False(BubbleSets.IsHiddenFrame("pic_event_bubble_100"));
            Assert.True(BubbleSets.IsHiddenFrame("pic_event_bubble_101"));
            Assert.False(BubbleSets.IsHiddenFrame("pic_walk_001"));
        }

        [Fact]
        public void Sway_starts_and_ends_each_cycle_at_the_same_pose()
        {
            BubbleSets.Sway(0f, out var d0, out var s0);
            BubbleSets.Sway(2f, out var d2, out var s2);
            Assert.Equal(-3f, d0, 3);
            Assert.Equal(0.95f, s0, 3);
            Assert.Equal(d0, d2, 3);
            Assert.Equal(s0, s2, 3);
        }

        [Fact]
        public void Sway_peaks_at_mid_cycle()
        {
            BubbleSets.Sway(1f, out var d, out var s);
            Assert.Equal(3f, d, 3);
            Assert.Equal(1f, s, 3);
            BubbleSets.Sway(0.5f, out d, out s);
            Assert.InRange(d, -3f, 3f);
            Assert.InRange(s, 0.95f, 1f);
        }

        [Fact]
        public void Sway_survives_bad_input()
        {
            foreach (var t in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -5f })
            {
                BubbleSets.Sway(t, out var d, out var s);
                Assert.False(float.IsNaN(d) || float.IsNaN(s));
                Assert.InRange(s, 0.9f, 1.05f);
            }
        }

        [Fact]
        public void PixelsPerUnit_scales_with_the_screen()
        {
            Assert.Equal(100f, BubbleSets.PixelsPerUnit(1080f), 3);
            Assert.Equal(0f, BubbleSets.PixelsPerUnit(0f));
            Assert.Equal(0f, BubbleSets.PixelsPerUnit(float.NaN));
        }

        [Fact]
        public void EventEntries_make_one_card_per_bubble_set()
        {
            var entries = GalleryLayout.EventEntries(PictureCatalog.Keys);
            // 4-set keeps 1 of 4 (-3), 6-set keeps 1 of 6 (-5), combat: 002 and 003 are left out (-2), cut in keeps 1 of 4 (-3)
            Assert.Equal(PictureCatalog.Count - 3 - 5 - 2 - 3, entries.Count);
            var four = entries.Single(e => e.Set == BubbleSets.Four);
            var six = entries.Single(e => e.Set == BubbleSets.Six);
            Assert.Equal("pic_event_bubble_1", four.Key);
            Assert.Equal("pic_event_bubble_4", four.UnlockKey);
            Assert.Equal("event bubble 1", four.Label);
            Assert.Equal("pic_event_bubble_100", six.Key);
            Assert.Equal("pic_event_bubble_105", six.UnlockKey);
            Assert.DoesNotContain(entries, e => e.Key == "pic_event_bubble_101");
            var walk = entries.First(e => e.Key == "pic_walk_001");
            Assert.Null(walk.Set);
            Assert.Equal("pic_walk_001", walk.UnlockKey);
        }

        [Fact]
        public void EventEntries_of_nothing_is_empty()
        {
            Assert.Empty(GalleryLayout.EventEntries(null));
        }
    }
}
