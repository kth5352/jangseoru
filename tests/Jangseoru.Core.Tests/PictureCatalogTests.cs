using Xunit;

namespace Jangseoru.Core.Tests
{
    public class PictureCatalogTests
    {
        [Fact]
        public void Count_is_309()
        {
            Assert.Equal(309, PictureCatalog.Count);
            Assert.Equal(309, PictureCatalog.Keys.Count);
        }

        [Fact]
        public void Keys_are_sorted_and_prefixed()
        {
            var keys = PictureCatalog.Keys;
            for (int i = 0; i < keys.Count; i++)
            {
                Assert.StartsWith("pic_", keys[i]);
                if (i > 0) Assert.True(string.CompareOrdinal(keys[i - 1], keys[i]) < 0);
            }
        }

        [Fact]
        public void TryNormalize_accepts_script_key()
        {
            Assert.True(PictureCatalog.TryNormalize("adventure_001", out var key));
            Assert.Equal("pic_adventure_001", key);
            Assert.True(PictureCatalog.Contains("pic_adventure_001"));
        }

        [Theory]
        [InlineData("tea_work_001")]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("pic_adventure_001")]
        [InlineData("Adventure_001")]
        public void TryNormalize_rejects_unknown_null_empty_and_prefixed(string input)
        {
            Assert.False(PictureCatalog.TryNormalize(input, out var key));
            Assert.Null(key);
        }

        [Fact]
        public void Group_strips_prefix_and_trailing_number()
        {
            Assert.Equal("girl_event", PictureCatalog.Group("pic_girl_event_003"));
            Assert.Equal("walk", PictureCatalog.Group("pic_walk_001"));
            Assert.Equal("a1b2c3d4", PictureCatalog.Group("pic_a1b2c3d4"));
            Assert.Equal("", PictureCatalog.Group(null));
        }
    }
}
