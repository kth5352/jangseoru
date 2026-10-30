using System;
using Xunit;

namespace Jangseoru.Core.Tests
{
    public class OathTimelineTests
    {
        private static OathTimeline At(int lines, float seconds)
        {
            var t = new OathTimeline(lines);
            t.Advance(seconds);
            return t;
        }

        [Fact]
        public void Starts_with_nothing_visible()
        {
            var t = new OathTimeline(3);
            Assert.Equal(OathPhase.BackdropIn, t.Phase);
            Assert.Equal(0f, t.BackdropAlpha);
            Assert.Equal(0f, t.ContentAlpha);
            Assert.False(t.AcceptsClose);
            Assert.False(t.Finished);
        }

        [Fact]
        public void The_white_fades_in_first_over_one_second_while_the_content_stays_hidden()
        {
            var t = At(3, 0.5f);
            Assert.Equal(0.5f, t.BackdropAlpha, 3);
            Assert.Equal(0f, t.ContentAlpha);
            Assert.Equal(0f, t.LineAlpha(0));
        }

        [Fact]
        public void Then_the_picture_and_text_fade_in_together_over_one_second()
        {
            var t = At(3, 1.5f);
            Assert.Equal(OathPhase.ContentIn, t.Phase);
            Assert.Equal(1f, t.BackdropAlpha);
            Assert.Equal(0.5f, t.ContentAlpha, 3);
            for (var i = 0; i < 3; i++) Assert.Equal(0.5f, t.LineAlpha(i), 3);
        }

        [Fact]
        public void Waits_forever_after_two_seconds_until_a_close_is_requested()
        {
            var t = At(2, 2f);
            Assert.Equal(OathPhase.Wait, t.Phase);
            Assert.True(t.AcceptsClose);
            Assert.Equal(1f, t.BackdropAlpha);
            Assert.Equal(1f, t.ContentAlpha);
            t.Advance(1000f);
            Assert.Equal(OathPhase.Wait, t.Phase);
            Assert.False(t.Finished);
        }

        [Fact]
        public void Small_steps_reach_the_same_state_as_one_big_step()
        {
            var t = new OathTimeline(4);
            for (var i = 0; i < 210; i++) t.Advance(0.01f);
            Assert.Equal(OathPhase.Wait, t.Phase);
        }

        [Fact]
        public void Close_requests_are_ignored_before_the_wait()
        {
            foreach (var s in new[] { 0f, 0.5f, 1.2f, 1.9f })
            {
                var t = At(3, s);
                var before = t.Phase;
                Assert.False(t.RequestClose());
                Assert.Equal(before, t.Phase);
            }
        }

        [Fact]
        public void Closing_reverses_the_order_content_out_first_then_the_white()
        {
            var t = At(3, 3f);
            Assert.True(t.RequestClose());
            Assert.Equal(OathPhase.ContentOut, t.Phase);
            Assert.False(t.AcceptsClose);

            t.Advance(0.25f);
            Assert.Equal(0.75f, t.ContentAlpha, 3);
            Assert.Equal(0.75f, t.LineAlpha(1), 3);
            Assert.Equal(1f, t.BackdropAlpha);

            t.Advance(1f); // 0.25s into the white fade out
            Assert.Equal(OathPhase.BackdropOut, t.Phase);
            Assert.Equal(0f, t.ContentAlpha);
            Assert.Equal(0.75f, t.BackdropAlpha, 3);
            Assert.False(t.Finished);

            t.Advance(0.75f);
            Assert.Equal(OathPhase.Done, t.Phase);
            Assert.True(t.Finished);
            Assert.Equal(0f, t.BackdropAlpha);
            Assert.Equal(0f, t.ContentAlpha);
        }

        [Fact]
        public void A_second_close_request_during_the_fade_out_is_ignored()
        {
            var t = At(3, 3f);
            Assert.True(t.RequestClose());
            t.Advance(0.5f);
            Assert.False(t.RequestClose());
            Assert.Equal(0.5f, t.ContentAlpha, 3);
        }

        [Fact]
        public void Close_requests_after_done_are_ignored()
        {
            var t = At(3, 3f);
            t.RequestClose();
            t.Advance(5f);
            Assert.False(t.RequestClose());
            Assert.Equal(OathPhase.Done, t.Phase);
        }

        [Fact]
        public void Negative_nan_and_infinite_steps_do_not_move_the_timeline()
        {
            var t = At(3, 0.5f);
            t.Advance(-1f);
            t.Advance(float.NaN);
            t.Advance(float.PositiveInfinity);
            t.Advance(float.NegativeInfinity);
            Assert.Equal(OathPhase.BackdropIn, t.Phase);
            Assert.Equal(0.5f, t.BackdropAlpha, 3);
        }

        [Fact]
        public void Out_of_range_line_indexes_are_invisible()
        {
            var t = At(2, 3f);
            Assert.Equal(0f, t.LineAlpha(-1));
            Assert.Equal(0f, t.LineAlpha(2));
        }

        [Fact]
        public void Zero_or_negative_line_counts_are_allowed()
        {
            var t = At(0, 3f);
            Assert.Equal(OathPhase.Wait, t.Phase);
            Assert.Equal(0f, t.LineAlpha(0));
            Assert.Equal(OathPhase.Wait, At(-5, 3f).Phase);
        }

        // ---- SplitLines ----

        [Fact]
        public void SplitLines_splits_on_newlines_like_the_game()
        {
            Assert.Equal(new[] { "a", "b", "c" }, OathTimeline.SplitLines("a\nb\nc"));
        }

        [Fact]
        public void SplitLines_accepts_windows_line_endings()
        {
            Assert.Equal(new[] { "a", "b" }, OathTimeline.SplitLines("a\r\nb\r\n"));
        }

        [Fact]
        public void SplitLines_drops_leading_and_trailing_blank_lines_and_keeps_inner_ones_as_a_space()
        {
            // An inner blank line is a paragraph gap: a space keeps its height in a Unity Text.
            Assert.Equal(new[] { "a", " ", "b" }, OathTimeline.SplitLines("\n \na\n\nb\n\n"));
        }

        [Fact]
        public void SplitLines_returns_nothing_for_missing_text()
        {
            Assert.Empty(OathTimeline.SplitLines(null));
            Assert.Empty(OathTimeline.SplitLines(""));
            Assert.Empty(OathTimeline.SplitLines(" \n\r\n\t"));
        }

        [Fact]
        public void SplitLines_caps_the_line_count()
        {
            var text = string.Join("\n", Array.ConvertAll(new int[500], _ => "line"));
            Assert.Equal(OathTimeline.MaxLines, OathTimeline.SplitLines(text).Count);
        }
    }
}
