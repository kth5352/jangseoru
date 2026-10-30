using Xunit;

namespace Jangseoru.Core.Tests
{
    public class MediaViewInputTests
    {
        [Fact]
        public void Polled_left_click_does_not_close_the_view()
        {
            // A left click closes through the backdrop's own click handler (it consumes the click); polling it as well can let
            // the same click reach the card underneath and reopen the view.
            Assert.False(MediaViewInput.ClosesOnPolledInput(escape: false, leftDown: true, rightDown: false));
        }

        [Fact]
        public void Escape_or_right_click_closes_the_view()
        {
            Assert.True(MediaViewInput.ClosesOnPolledInput(escape: true, leftDown: false, rightDown: false));
            Assert.True(MediaViewInput.ClosesOnPolledInput(escape: false, leftDown: false, rightDown: true));
            Assert.False(MediaViewInput.ClosesOnPolledInput(escape: false, leftDown: false, rightDown: false));
        }
    }

    public class SeenOnceTests
    {
        [Fact]
        public void An_id_is_inspected_only_the_first_time()
        {
            var seen = new SeenOnce(10);
            Assert.True(seen.FirstTime(42));
            Assert.False(seen.FirstTime(42));
            Assert.True(seen.FirstTime(7));
        }

        [Fact]
        public void The_set_forgets_everything_when_it_reaches_its_cap()
        {
            var seen = new SeenOnce(3);
            Assert.True(seen.FirstTime(1));
            Assert.True(seen.FirstTime(2));
            Assert.True(seen.FirstTime(3));
            Assert.True(seen.FirstTime(4)); // cap reached: cleared, so memory stays bounded
            Assert.True(seen.FirstTime(1)); // forgotten, inspected again (harmless: recording is idempotent)
            Assert.False(seen.FirstTime(4));
        }
    }
}
