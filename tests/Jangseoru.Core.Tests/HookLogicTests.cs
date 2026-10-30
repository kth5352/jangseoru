using System;
using System.Linq;
using Xunit;

namespace Jangseoru.Core.Tests
{
    public class HookLogicTests
    {
        private const string Root = @"C:\Users\someone\AppData\LocalLow\Obb Studio\Mortal";

        [Theory]
        [InlineData("title", 0, 0, true)]
        [InlineData("title", 25, 25, true)]
        [InlineData("title", 7, 7, true)]
        [InlineData("title", 26, 25, false)]
        [InlineData("title", 25, 26, false)]
        [InlineData("title", -1, 3, false)]
        [InlineData("title", 3, -1, false)]
        [InlineData("karma", 5, 5, false)]
        [InlineData("Title", 5, 5, false)]
        [InlineData(null, 5, 5, false)]
        [InlineData("", 5, 5, false)]
        public void ShouldCollectOnSetPlayer_accepts_only_title_with_both_values_in_range(string name, int requested, int actual, bool expected)
        {
            Assert.Equal(expected, HookLogic.ShouldCollectOnSetPlayer(name, requested, actual));
        }

        [Theory]
        [InlineData(@"C:\Users\someone\AppData\LocalLow\Obb Studio\Mortal\76561198000000000\Save_001.dat", "76561198000000000", "001")]
        [InlineData(@"C:\Users\someone\AppData\LocalLow\Obb Studio\Mortal\76561198000000000\Save_auto.dat", "76561198000000000", "auto")]
        [InlineData(@"C:\Users\someone\AppData\LocalLow\Obb Studio\Mortal\76561198000000000\Save_auto_free.dat", "76561198000000000", "auto_free")]
        [InlineData(@"C:\Users\someone\AppData\LocalLow\Obb Studio\Mortal\76561198000000000\Save_custom_slot.dat", "76561198000000000", "custom_slot")]
        [InlineData(@"C:\Users\someone\AppData\LocalLow\Obb Studio\Mortal\Save\Save_002.dat", "save", "002")]
        [InlineData(@"c:\users\SOMEONE\appdata\locallow\obb studio\mortal\ABC\SAVE_Upper.dat", "abc", "upper")]
        public void TryGetSlotIdentity_accepts_one_profile_folder_and_a_Save_dat_file(string file, string profile, string stem)
        {
            Assert.True(HookLogic.TryGetSlotIdentity(Root, file, out var p, out var s));
            Assert.Equal(profile, p);
            Assert.Equal(stem, s);
        }

        [Fact]
        public void TryGetSlotIdentity_accepts_forward_slashes_and_trailing_separator_on_the_root()
        {
            Assert.True(HookLogic.TryGetSlotIdentity(Root + "/", @"C:/Users/someone/AppData/LocalLow/Obb Studio/Mortal/7656/Save_004.dat", out var p, out var s));
            Assert.Equal("7656", p);
            Assert.Equal("004", s);
        }

        [Theory]
        [InlineData(@"C:\Users\someone\AppData\LocalLow\Obb Studio\Mortal\7656\Save_universe.dat")]
        [InlineData(@"C:\Users\someone\AppData\LocalLow\Obb Studio\Mortal\7656\Save_UNIVERSE.dat")]
        [InlineData(@"C:\Users\someone\AppData\LocalLow\Obb Studio\Mortal\7656\Save_universe.json")]
        [InlineData(@"C:\Users\someone\AppData\LocalLow\Obb Studio\Mortal\7656\Legend_001.dat")]
        [InlineData(@"C:\Users\someone\AppData\LocalLow\Obb Studio\Mortal\7656\Save_001.dat.tmp")]
        [InlineData(@"C:\Users\someone\AppData\LocalLow\Obb Studio\Mortal\7656\Save_.dat")]
        [InlineData(@"C:\Users\someone\AppData\LocalLow\Obb Studio\Mortal\7656\x.txt")]
        [InlineData(@"C:\Users\someone\AppData\LocalLow\Obb Studio\Mortal\Backup\20260220065841\Save_001.dat")]
        [InlineData(@"C:\Users\someone\AppData\LocalLow\Obb Studio\Mortal\Save_001.dat")]
        [InlineData(@"C:\Users\someone\AppData\LocalLow\Obb Studio\Mortal\7656\sub\Save_001.dat")]
        [InlineData(@"C:\Users\someone\AppData\LocalLow\Obb Studio\Mortal\..\Other\7656\Save_001.dat")]
        [InlineData(@"C:\Users\someone\AppData\LocalLow\Obb Studio\Mortal\7656\..\..\Evil\Save_001.dat")]
        [InlineData(@"C:\Elsewhere\7656\Save_001.dat")]
        [InlineData(@"C:\Users\someone\AppData\LocalLow\Obb Studio\Mortal2\7656\Save_001.dat")]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("not a path \0 at all")]
        public void TryGetSlotIdentity_rejects_everything_else(string file)
        {
            Assert.False(HookLogic.TryGetSlotIdentity(Root, file, out var p, out var s));
            Assert.Null(p);
            Assert.Null(s);
        }

        [Fact]
        public void TryGetSlotIdentity_accepts_the_mixed_separators_the_game_builds()
        {
            // Unity reports persistentDataPath with '/', and the game appends "<user>/Save_<slot>.dat" via Path.Combine.
            Assert.True(HookLogic.TryGetSlotIdentity(
                "C:/Users/someone/AppData/LocalLow/Obb Studio/Mortal",
                "C:/Users/someone/AppData/LocalLow/Obb Studio/Mortal\\76561198000000000/Save_FastSave_QQ.dat",
                out var profile, out var stem));
            Assert.Equal("76561198000000000", profile);
            Assert.Equal("fastsave_qq", stem);
        }

        [Theory]
        [InlineData(true, true, true)]
        [InlineData(true, false, true)]
        [InlineData(false, true, true)]
        [InlineData(false, false, false)]
        public void Save_and_load_are_observed_when_either_title_or_per_save_tracking_is_on(bool titlesOn, bool perSaveOn, bool expected)
        {
            // A missing SetPlayer hook must not silence per-save tracking, and vice versa.
            Assert.Equal(expected, HookLogic.ShouldObserveSaveEvents(titlesOn, perSaveOn));
        }

        [Theory]
        [InlineData(true, "adventure_001", true)]
        [InlineData(false, "adventure_001", false)]
        [InlineData(true, null, false)]
        [InlineData(true, "", false)]
        [InlineData(true, "  ", false)]
        public void ShouldRecordPicture_requires_flag_and_non_blank_key(bool galleryOn, string key, bool expected)
        {
            Assert.Equal(expected, HookLogic.ShouldRecordPicture(galleryOn, key));
        }

        [Fact]
        public void Recorder_records_cataloged_key_once_and_ignores_others()
        {
            using (var dir = new TempDir())
            {
                var t0 = new DateTime(2026, 10, 20, 4, 0, 0, DateTimeKind.Utc);
                var path = dir.File("gallery.txt");
                var file = new GalleryFile(path, new ListLog(), TimeSpan.FromSeconds(2));
                file.Load(t0);
                var rec = new Jangseoru.Plugin.GalleryRecorder(file, new ListLog());

                rec.OnPicture("tea_work_001", t0);
                rec.OnPicture(null, t0);
                rec.OnPicture("", t0);
                rec.OnPicture("pic_adventure_001", t0);
                Assert.Equal(0, file.Store.PictureCount);
                file.FlushIfDue(t0.AddSeconds(10));
                Assert.False(System.IO.File.Exists(path));

                rec.OnPicture("adventure_001", t0);
                Assert.True(file.Store.HasPicture("pic_adventure_001"));
                Assert.Equal(1, file.Store.PictureCount);
                Assert.False(System.IO.File.Exists(path)); // the hook itself never writes to disk
                file.FlushIfDue(t0.AddSeconds(10));
                Assert.True(System.IO.File.Exists(path));

                System.IO.File.Delete(path);
                rec.OnPicture("adventure_001", t0.AddSeconds(20)); // repeat: must not mark dirty again
                file.FlushIfDue(t0.AddSeconds(40));
                Assert.False(System.IO.File.Exists(path));
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void TryGetSlotIdentity_rejects_a_missing_root(string root)
        {
            Assert.False(HookLogic.TryGetSlotIdentity(root, @"C:\x\7656\Save_001.dat", out _, out _));
        }
    }
}
