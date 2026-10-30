using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Jangseoru.Core.Tests
{
    public class MediaCatalogTests
    {
        [Fact]
        public void Catalog_has_the_games_backgrounds_big_pictures_and_videos()
        {
            Assert.Equal(213, MediaCatalog.Entries(MediaKind.Background).Count);
            Assert.Equal(29, MediaCatalog.Entries(MediaKind.BigPicture).Count);
            Assert.Equal(10, MediaCatalog.Entries(MediaKind.Video).Count);
        }

        [Fact]
        public void Keys_are_unique_within_a_kind_and_know_their_address()
        {
            foreach (MediaKind kind in Enum.GetValues(typeof(MediaKind)))
            {
                var entries = MediaCatalog.Entries(kind);
                Assert.Equal(entries.Count, entries.Select(e => e.Key).Distinct(StringComparer.Ordinal).Count());
                Assert.All(entries, e => Assert.False(string.IsNullOrEmpty(e.Address)));
            }
            var bg = MediaCatalog.Find(MediaKind.Background, "background_map_section_001");
            Assert.Equal("Assets/__Project/Images/Background/background_map_section_001.png", bg.Address);
            Assert.Equal("pic_big_fight_001", MediaCatalog.Find(MediaKind.BigPicture, "pic_big_fight_001").Address);
            Assert.Equal("video_event_001", MediaCatalog.Find(MediaKind.Video, "video_event_001").Address);
        }

        [Fact]
        public void Combat_backgrounds_come_last_in_the_background_list()
        {
            var list = MediaCatalog.Entries(MediaKind.Background);
            Assert.StartsWith("back_combat_", list[list.Count - 1].Key);
            Assert.StartsWith("back_combat_", list[list.Count - 2].Key);
            Assert.DoesNotContain("back_combat_", list[list.Count - 3].Key);
        }

        [Fact]
        public void Contains_checks_the_kind_and_rejects_junk()
        {
            Assert.True(MediaCatalog.Contains(MediaKind.Video, "video_event_001"));
            Assert.False(MediaCatalog.Contains(MediaKind.Background, "video_event_001"));
            Assert.False(MediaCatalog.Contains(MediaKind.Video, null));
            Assert.False(MediaCatalog.Contains(MediaKind.Video, "nothing"));
            Assert.Null(MediaCatalog.Find(MediaKind.Video, "nothing"));
        }

        [Fact]
        public void Kind_prefixes_round_trip_and_tabs_have_labels()
        {
            foreach (MediaKind kind in Enum.GetValues(typeof(MediaKind)))
            {
                Assert.True(MediaKinds.TryFromPrefix(MediaKinds.Prefix(kind), out var back));
                Assert.Equal(kind, back);
                Assert.False(string.IsNullOrEmpty(MediaKinds.TabLabel(kind)));
            }
            Assert.Equal("풍광", MediaKinds.TabLabel(MediaKind.Background));
            Assert.Equal("대사건", MediaKinds.TabLabel(MediaKind.BigPicture));
            Assert.Equal("환영", MediaKinds.TabLabel(MediaKind.Video));
            Assert.False(MediaKinds.TryFromPrefix("Book", out _));
        }
    }

    public class MediaStoreTests
    {
        [Fact]
        public void Add_and_has_work_per_kind_and_reject_invalid_keys()
        {
            var s = new MediaStore();
            Assert.True(s.Add(MediaKind.Video, "video_event_001"));
            Assert.False(s.Add(MediaKind.Video, "video_event_001"));
            Assert.True(s.Has(MediaKind.Video, "video_event_001"));
            Assert.False(s.Has(MediaKind.Background, "video_event_001"));
            Assert.False(s.Add(MediaKind.Video, "bad key"));
            Assert.False(s.Add(MediaKind.Video, null));
            Assert.False(s.Add(MediaKind.Video, new string('a', 81)));
            Assert.Equal(1, s.Count);
        }

        [Fact]
        public void Serialize_is_sorted_and_parse_restores_it()
        {
            var s = new MediaStore();
            s.Add(MediaKind.Video, "video_event_001");
            s.Add(MediaKind.Background, "background_map_section_001");
            s.Add(MediaKind.BigPicture, "pic_big_fight_001");
            var text = s.Serialize();
            Assert.StartsWith("jangseoru.media v1\n", text);
            var back = MediaStore.Parse(text, out var corrupt);
            Assert.False(corrupt);
            Assert.True(back.Has(MediaKind.Background, "background_map_section_001"));
            Assert.True(back.Has(MediaKind.BigPicture, "pic_big_fight_001"));
            Assert.True(back.Has(MediaKind.Video, "video_event_001"));
            Assert.Equal(text, back.Serialize());
        }

        [Fact]
        public void Garbled_header_is_corrupt_and_a_newer_version_is_read_only()
        {
            MediaStore.Parse("nonsense\n", out var corrupt);
            Assert.True(corrupt);
            var newer = MediaStore.Parse("jangseoru.media v2\nVid:x\n", out var c2);
            Assert.False(c2);
            Assert.True(newer.ReadOnly);
            Assert.Equal("jangseoru.media v2\nVid:x\n", newer.Serialize());
        }

        [Fact]
        public void Unknown_kind_lines_survive_a_round_trip_and_junk_is_dropped()
        {
            var s = MediaStore.Parse("jangseoru.media v1\nBg:background_map_section_001\nFuture:abc\n@@@\n\nVid:bad key\n", out _);
            Assert.True(s.Has(MediaKind.Background, "background_map_section_001"));
            Assert.Contains("Future:abc", s.Serialize());
            Assert.DoesNotContain("@@@", s.Serialize());
            Assert.DoesNotContain("bad key", s.Serialize());
        }

        [Fact]
        public void A_file_with_far_too_many_known_lines_is_capped()
        {
            var lines = string.Join("\n", Enumerable.Range(0, 9000).Select(i => "Vid:k" + i));
            var s = MediaStore.Parse("jangseoru.media v1\n" + lines + "\n", out _);
            Assert.True(s.Count <= 4000);
        }
    }

    public class MediaRecorderAndListTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 10, 22, 6, 0, 0, DateTimeKind.Utc);
        private static readonly TimeSpan Debounce = TimeSpan.FromSeconds(2);

        private static MediaFile Open(string path, IFileOps ops = null)
        {
            var f = new MediaFile(path, new ListLog(), Debounce, ops ?? RealFileOps.Instance);
            f.Load(T0);
            return f;
        }

        [Fact]
        public void Shown_catalog_key_is_recorded_once_and_marks_dirty()
        {
            using (var dir = new TempDir())
            {
                var path = dir.File("media.txt");
                var ops = new InjectableFileOps();
                var f = Open(path, ops);
                var r = new MediaRecorder(f, new ListLog());
                r.OnShown(MediaKind.Video, "video_event_001", T0);
                r.OnShown(MediaKind.Video, "video_event_001", T0);
                Assert.True(f.Store.Has(MediaKind.Video, "video_event_001"));
                Assert.Equal(1, f.Store.Count);
                f.FlushIfDue(T0.AddSeconds(2));
                Assert.Equal(1, ops.WriteNewCalls);
                f.FlushIfDue(T0.AddSeconds(9));
                Assert.Equal(1, ops.WriteNewCalls);
                Assert.True(Open(path).Store.Has(MediaKind.Video, "video_event_001"));
            }
        }

        [Fact]
        public void Keys_outside_the_catalog_are_ignored()
        {
            using (var dir = new TempDir())
            {
                var f = Open(dir.File("media.txt"));
                var r = new MediaRecorder(f, new ListLog());
                r.OnShown(MediaKind.Background, "not_a_background", T0);
                r.OnShown(MediaKind.Background, "video_event_001", T0); // right text, wrong kind
                r.OnShown(MediaKind.Video, null, T0);
                new MediaRecorder(null, new ListLog()).OnShown(MediaKind.Video, "video_event_001", T0); // no file: no throw
                Assert.Equal(0, f.Store.Count);
            }
        }

        [Fact]
        public void List_has_every_catalog_entry_in_order_with_locked_ones_hidden()
        {
            var store = new MediaStore();
            store.Add(MediaKind.Video, "video_event_002");
            var rows = MediaList.Build(MediaKind.Video, store);
            Assert.Equal(10, rows.Count);
            Assert.Equal(MediaCatalog.Entries(MediaKind.Video).Select(e => e.Key), rows.Select(r => r.Key));
            var open = rows.Single(r => r.Unlocked);
            Assert.Equal("video_event_002", open.Key);
            Assert.Equal((rows.ToList().IndexOf(open) + 1).ToString(), open.Label);
            Assert.All(rows.Where(r => !r.Unlocked), r => Assert.Equal(MediaList.Hidden, r.Label));
            Assert.Equal("1/10", MediaList.Progress(rows));
        }

        [Fact]
        public void List_without_a_store_is_all_locked_and_progress_is_zero()
        {
            var rows = MediaList.Build(MediaKind.BigPicture, null);
            Assert.Equal(29, rows.Count);
            Assert.All(rows, r => Assert.False(r.Unlocked));
            Assert.Equal("0/29", MediaList.Progress(rows));
            Assert.Equal("0/0", MediaList.Progress(null));
        }

        [Fact]
        public void Media_file_path_sits_next_to_the_other_mod_files()
        {
            var p = StorePaths.ResolveMedia(Path.Combine("C:", "Users", "x", "AppData", "LocalLow", "Obb Studio", "Mortal"));
            Assert.EndsWith(Path.Combine("JangseoruMod", "media.txt"), p);
            Assert.Null(StorePaths.ResolveMedia(null));
        }
    }
}
