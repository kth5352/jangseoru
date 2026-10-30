using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Jangseoru.Core.Tests
{
    public class MediaDiscoveryTests
    {
        private const string Pic = "Assets/__Project/Images/Picture/";
        private const string Bg = "Assets/__Project/Images/Background/";
        private const string Big = "Assets/__Project/Images/BigPicture/";
        private const string Vid = "Assets/__Project/Videos/";

        private static CatalogLocation L(string internalId, string type, string key = null)
            => new CatalogLocation { Key = key ?? internalId, InternalId = internalId, ResourceType = type };

        [Fact]
        public void Pictures_are_the_pic_sprites_under_the_picture_folder()
        {
            var keys = MediaDiscovery.Pictures(new[]
            {
                L(Pic + "pic_new_001.png", "Sprite"),
                L(Pic + "pic_new_001.png", "Texture2D"),          // the same file as a texture: not a second picture
                L(Pic + "pic_new_001.png", "Sprite", "pic_new_001"), // another key of the same sprite
                L(Pic + "pic_aaa_002.png", "Sprite"),
                L(Pic + "readme.png", "Sprite"),                   // not a pic_ name
                L(Big + "pic_big_fight_001.png", "Sprite"),         // another folder
                null,
            });
            Assert.Equal(new[] { "pic_aaa_002", "pic_new_001" }, keys);
        }

        [Fact]
        public void Backgrounds_are_keyed_by_sprite_name_load_by_path_and_combat_ones_come_last()
        {
            var list = MediaDiscovery.Entries(MediaKind.Background, new[]
            {
                L(Bg + "Combat/back_combat_x.png", "Sprite"),
                L(Bg + "background_b.png", "Sprite"),
                L(Bg + "background_a.png", "Sprite"),
                L(Bg + "background_a.png", "Texture2D"),
            });
            Assert.Equal(new[] { "background_a", "background_b", "back_combat_x" }, list.Select(e => e.Key));
            Assert.Equal(Bg + "background_a.png", list[0].Address);
            Assert.All(list, e => Assert.Equal(MediaKind.Background, e.Kind));
        }

        [Fact]
        public void Big_pictures_and_videos_are_keyed_and_loaded_by_name()
        {
            var big = MediaDiscovery.Entries(MediaKind.BigPicture, new[] { L(Big + "pic_big_z.png", "Sprite"), L(Big + "pic_big_a.png", "Sprite") });
            Assert.Equal(new[] { "pic_big_a", "pic_big_z" }, big.Select(e => e.Key));
            Assert.Equal("pic_big_a", big[0].Address);

            var vid = MediaDiscovery.Entries(MediaKind.Video, new[] { L(Vid + "video_new_001.mp4", "VideoClip"), L(Vid + "video_new_001.mp4", "Sprite") });
            Assert.Equal("video_new_001", Assert.Single(vid).Key);
            Assert.Equal("video_new_001", vid[0].Address);
        }

        [Fact]
        public void Keys_with_characters_the_stores_cannot_hold_are_skipped()
        {
            var list = MediaDiscovery.Entries(MediaKind.Video, new[]
            {
                L(Vid + "bad name.mp4", "VideoClip"),
                L(Vid + new string('a', 90) + ".mp4", "VideoClip"),
                L(Vid + "ok_1.mp4", "VideoClip"),
            });
            Assert.Equal("ok_1", Assert.Single(list).Key);
        }

        [Fact]
        public void Discovery_is_capped()
        {
            var many = Enumerable.Range(0, 20000).Select(i => L(Pic + "pic_k" + i + ".png", "Sprite")).ToList();
            Assert.True(MediaDiscovery.Pictures(many).Count <= MediaDiscovery.MaxPerKind);
        }
    }

    public class CatalogMergeTests
    {
        [Fact]
        public void Merged_pictures_keep_the_base_and_add_only_new_valid_keys_in_order()
        {
            var merged = CatalogMerge.Pictures(new[] { "pic_a", "pic_c" }, new[] { "pic_b", "pic_a", "nope", null, "pic_b" });
            Assert.Equal(new[] { "pic_a", "pic_b", "pic_c" }, merged);
        }

        [Fact]
        public void Nothing_new_returns_the_same_keys()
        {
            var merged = CatalogMerge.Pictures(new[] { "pic_a", "pic_b" }, new[] { "pic_a" });
            Assert.Equal(new[] { "pic_a", "pic_b" }, merged);
        }

        [Fact]
        public void Merged_media_keeps_existing_entries_adds_new_and_orders_backgrounds_combat_last()
        {
            var baseList = new List<MediaEntry>
            {
                new MediaEntry { Kind = MediaKind.Background, Key = "background_b", Address = "p/background_b.png" },
                new MediaEntry { Kind = MediaKind.Background, Key = "back_combat_x", Address = "p/Combat/back_combat_x.png" },
            };
            var found = new List<MediaEntry>
            {
                new MediaEntry { Kind = MediaKind.Background, Key = "background_a", Address = "p/background_a.png" },
                new MediaEntry { Kind = MediaKind.Background, Key = "background_b", Address = "OTHER" }, // already known: the base wins
            };
            var merged = CatalogMerge.Media(MediaKind.Background, baseList, found);
            Assert.Equal(new[] { "background_a", "background_b", "back_combat_x" }, merged.Select(e => e.Key));
            Assert.Equal("p/background_b.png", merged.Single(e => e.Key == "background_b").Address);
        }

        [Fact]
        public void Merged_videos_stay_in_address_order()
        {
            var merged = CatalogMerge.Media(MediaKind.Video,
                new List<MediaEntry> { new MediaEntry { Kind = MediaKind.Video, Key = "video_b", Address = "video_b" } },
                new List<MediaEntry> { new MediaEntry { Kind = MediaKind.Video, Key = "video_a", Address = "video_a" } });
            Assert.Equal(new[] { "video_a", "video_b" }, merged.Select(e => e.Key));
        }
    }
}
