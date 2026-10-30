using System.Linq;
using Xunit;

namespace Jangseoru.Core.Tests
{
    public class GalleryStoreTests
    {
        private static string K(int i) => PictureCatalog.Keys[i];

        [Fact]
        public void AddPicture_is_idempotent_and_rejects_uncataloged()
        {
            var s = new GalleryStore();
            Assert.True(s.AddPicture(K(0)));
            Assert.False(s.AddPicture(K(0)));
            Assert.False(s.AddPicture("pic_not_in_catalog"));
            Assert.False(s.AddPicture(null));
            Assert.False(s.AddPicture(""));
            Assert.True(s.HasPicture(K(0)));
            Assert.False(s.HasPicture(K(1)));
            Assert.Equal(1, s.PictureCount);
            Assert.Equal(new[] { K(0) }, s.Pictures.ToArray());
        }

        [Fact]
        public void Serialize_then_Parse_roundtrips_in_sorted_order()
        {
            var s = new GalleryStore();
            var keys = new[] { K(5), K(2), K(9) };
            foreach (var k in keys) s.AddPicture(k);

            var text = s.Serialize();
            Assert.StartsWith(GalleryStore.Header + "\n", text);

            var back = GalleryStore.Parse(text, out var corrupt);
            Assert.False(corrupt);
            Assert.False(back.ReadOnly);
            Assert.Equal(keys.OrderBy(k => k, System.StringComparer.Ordinal).ToArray(), back.Pictures.ToArray());
            Assert.Equal(text, back.Serialize());
        }

        [Fact]
        public void Parse_skips_unknown_and_duplicate_keys_but_keeps_the_rest()
        {
            var text = GalleryStore.Header + "\n" + K(1) + "\n\npic_bogus\n" + K(1) + "\r\n" + K(3) + "\n";
            var s = GalleryStore.Parse(text, out var corrupt);
            Assert.False(corrupt);
            Assert.Equal(2, s.PictureCount);
            Assert.True(s.HasPicture(K(1)));
            Assert.True(s.HasPicture(K(3)));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("pic_adventure_001\n")]
        [InlineData("jangseoru.gallery v0\n")]
        [InlineData("jangseoru.gallery v01\n")]
        [InlineData("jangseoru.byeolho v1\n")]
        public void Parse_without_header_is_corrupt(string text)
        {
            var s = GalleryStore.Parse(text, out var corrupt);
            Assert.True(corrupt);
            Assert.Equal(0, s.PictureCount);
            Assert.False(s.ReadOnly);
        }

        [Fact]
        public void Unknown_wellformed_key_survives_roundtrip_but_is_not_counted()
        {
            var text = GalleryStore.Header + "\n" + K(1) + "\npic_future_999\n";
            var s = GalleryStore.Parse(text, out var corrupt);
            Assert.False(corrupt);
            Assert.Equal(1, s.PictureCount);
            Assert.False(s.HasPicture("pic_future_999"));
            Assert.DoesNotContain("pic_future_999", s.Pictures);
            Assert.False(s.AddPicture("pic_future_999"));
            var lines = s.Serialize().Split('\n');
            Assert.Contains(K(1), lines);
            Assert.Equal(1, lines.Count(l => l == "pic_future_999"));
        }

        [Fact]
        public void Malformed_keys_are_dropped()
        {
            var text = GalleryStore.Header + "\npic_has space\nPIC_upper\njangseoru.gallery v1\npic_" + new string('a', 81) + "\npic_\npic_ok_1\n";
            var s = GalleryStore.Parse(text, out var corrupt);
            Assert.False(corrupt);
            Assert.Equal(GalleryStore.Header + "\npic_ok_1\n", s.Serialize());
        }

        [Fact]
        public void Passthrough_is_capped_at_2000()
        {
            var sb = new System.Text.StringBuilder(GalleryStore.Header + "\n");
            for (var i = 0; i < 2500; i++) sb.Append("pic_zz_").Append(i).Append('\n');
            var s = GalleryStore.Parse(sb.ToString(), out _);
            Assert.Equal(2000, s.Serialize().Split('\n').Count(l => l.StartsWith("pic_zz_")));
        }

        [Fact]
        public void Serialize_merges_collected_and_passthrough_sorted_without_duplicates()
        {
            var s = GalleryStore.Parse(GalleryStore.Header + "\npic_zzz_1\npic_000_unknown\n" + K(0) + "\n", out _);
            s.AddPicture(K(0));
            s.AddPicture(K(1));
            var body = s.Serialize().Split('\n').Skip(1).Where(l => l.Length > 0).ToArray();
            Assert.Equal(body.OrderBy(k => k, System.StringComparer.Ordinal).ToArray(), body);
            Assert.Equal(body.Length, body.Distinct().Count());
            Assert.Equal(4, body.Length);
        }

        [Fact]
        public void Parse_future_version_is_read_only_not_overwritten()
        {
            var text = "jangseoru.gallery v2\nsome new format\n";
            var s = GalleryStore.Parse(text, out var corrupt);
            Assert.False(corrupt);
            Assert.True(s.ReadOnly);
            Assert.Equal(text, s.Serialize());
        }
    }
}
