using System.Linq;
using Xunit;

namespace Jangseoru.Core.Tests
{
    public class OverlaySetsTests
    {
        [Fact]
        public void Every_key_is_in_the_catalog()
        {
            foreach (var set in OverlaySets.All)
                foreach (var k in set.Keys)
                    Assert.True(PictureCatalog.Contains(k), k);
        }

        [Fact]
        public void Combat_sets_share_two_layers_and_have_their_own_first()
        {
            Assert.Equal(new[] { "pic_combat_event_001", "pic_combat_event_002", "pic_combat_event_003" }, OverlaySets.CombatA.Keys);
            Assert.Equal(new[] { "pic_combat_event_004", "pic_combat_event_002", "pic_combat_event_003" }, OverlaySets.CombatB.Keys);
            Assert.Equal("pic_combat_event_001", OverlaySets.CombatA.UnlockKey);
            Assert.Equal("pic_combat_event_004", OverlaySets.CombatB.UnlockKey);
        }

        [Fact]
        public void Card_and_hidden_frames()
        {
            Assert.Same(OverlaySets.CombatB, OverlaySets.ForCardKey("pic_combat_event_004"));
            Assert.Same(OverlaySets.CutIn, OverlaySets.ForCardKey("pic_cut_in_001"));
            Assert.Null(OverlaySets.ForCardKey("pic_combat_event_002"));
            Assert.True(OverlaySets.IsHiddenFrame("pic_combat_event_002"));
            Assert.True(OverlaySets.IsHiddenFrame("pic_combat_event_003"));
            Assert.True(OverlaySets.IsHiddenFrame("pic_cut_in_004"));
            Assert.False(OverlaySets.IsHiddenFrame("pic_combat_event_001"));
            Assert.False(OverlaySets.IsHiddenFrame("pic_combat_event_004"));
            Assert.False(OverlaySets.IsHiddenFrame("pic_walk_001"));
            Assert.False(OverlaySets.IsHiddenFrame(null));
        }

        [Fact]
        public void Combat_timeline_matches_the_game_values()
        {
            var s = OverlaySets.CombatA;
            Assert.Equal(0f, s.StartOf(0), 4);
            Assert.Equal(0.2f, s.StartOf(1), 4);
            Assert.Equal(0.4f, s.StartOf(2), 4);
            Assert.Equal(0.5f, s.AllInAt, 4);
            Assert.Equal(0.5f + 1.5f + 0.5f, s.PlayLength, 4);

            Assert.Equal(0f, s.Alpha(0, 0f), 4);
            Assert.Equal(0.5f, s.Alpha(0, 0.05f), 4);
            Assert.Equal(1f, s.Alpha(0, 0.3f), 4);
            Assert.Equal(0f, s.Alpha(1, 0.1f), 4);        // second layer not started yet
            Assert.Equal(1f, s.Alpha(2, 1.0f), 4);        // held
            Assert.Equal(0.5f, s.Alpha(2, 2.25f), 3);     // half way through the fade out (2.0 .. 2.5)
            Assert.Equal(0f, s.Alpha(0, 2.5f), 4);
            Assert.Equal(0f, s.Alpha(0, 99f), 4);
        }

        [Fact]
        public void Cut_in_shows_instantly_in_order_and_stays()
        {
            var s = OverlaySets.CutIn;
            Assert.Equal(0f, s.Alpha(1, 0.05f), 4);
            Assert.Equal(1f, s.Alpha(1, 0.1f), 4);
            Assert.Equal(1f, s.Alpha(3, 60f), 4);
            Assert.Equal(s.AllInAt, s.PlayLength, 4);
        }

        [Fact]
        public void Alpha_survives_bad_time()
        {
            Assert.Equal(0f, OverlaySets.CombatA.Alpha(0, float.NaN), 4);
            Assert.Equal(0f, OverlaySets.CombatA.Alpha(0, -3f), 4);
        }

        [Fact]
        public void Strip_centres_are_symmetric()
        {
            Assert.Equal(-OverlaySets.StripCenter(3, 4), OverlaySets.StripCenter(0, 4), 5);
            Assert.Equal(-OverlaySets.StripCenter(2, 4), OverlaySets.StripCenter(1, 4), 5);
            Assert.Equal(OverlaySets.StripAspect, OverlaySets.StripCenter(2, 4) - OverlaySets.StripCenter(1, 4), 5);
            Assert.Equal(0f, OverlaySets.StripCenter(0, 0));
        }

        [Fact]
        public void EventEntries_make_one_card_per_overlay_set()
        {
            var entries = GalleryLayout.EventEntries(PictureCatalog.Keys);
            Assert.DoesNotContain(entries, e => e.Key == "pic_combat_event_002" || e.Key == "pic_combat_event_003" || e.Key == "pic_cut_in_002");
            var a = entries.Single(e => e.Overlay == OverlaySets.CombatA);
            var b = entries.Single(e => e.Overlay == OverlaySets.CombatB);
            var c = entries.Single(e => e.Overlay == OverlaySets.CutIn);
            Assert.Equal("combat event 1", a.Label);
            Assert.Equal("pic_combat_event_004", b.Key);
            Assert.Equal("cut in", c.Label);
            Assert.Equal("pic_cut_in_001", c.UnlockKey);
        }
    }
}
