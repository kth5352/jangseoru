using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Jangseoru.Core.Tests
{
    public class GalleryFileTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 10, 20, 4, 0, 0, DateTimeKind.Utc);
        private static readonly TimeSpan Debounce = TimeSpan.FromSeconds(2);

        private static GalleryFile Make(string path, ListLog log = null, IFileOps ops = null)
            => new GalleryFile(path, log ?? new ListLog(), Debounce, ops ?? RealFileOps.Instance);

        [Fact]
        public void GalleryFile_load_missing_file_gives_empty_store_and_does_not_block()
        {
            using (var dir = new TempDir())
            {
                var f = Make(dir.File("gallery.txt"));
                f.Load(T0);
                Assert.Equal(0, f.Store.PictureCount);
                Assert.False(f.WritesBlocked);
                Assert.False(f.Degraded);
            }
        }

        [Fact]
        public void GalleryFile_flush_writes_atomically_after_debounce()
        {
            using (var dir = new TempDir())
            {
                var path = dir.File("gallery.txt");
                var f = Make(path);
                f.Load(T0);
                f.Store.AddPicture(PictureCatalog.Keys[0]);
                f.MarkDirty(T0);

                f.FlushIfDue(T0.AddSeconds(1));
                Assert.False(File.Exists(path));
                f.FlushIfDue(T0.AddSeconds(2));
                Assert.True(File.Exists(path));
                Assert.Single(Directory.GetFiles(dir.Path));

                var g = Make(path);
                g.Load(T0);
                Assert.True(g.Store.HasPicture(PictureCatalog.Keys[0]));
                Assert.False(g.WritesBlocked);
            }
        }

        [Fact]
        public void GalleryFile_unreadable_file_blocks_writes_and_never_throws()
        {
            using (var dir = new TempDir())
            {
                var path = dir.File("gallery.txt");
                File.WriteAllText(path, GalleryStore.Header + "\n" + PictureCatalog.Keys[0] + "\n");
                var before = File.ReadAllBytes(path);
                GalleryFile f;
                using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    f = Make(path);
                    f.Load(T0);
                }
                Assert.True(f.WritesBlocked);

                f.Store.AddPicture(PictureCatalog.Keys[1]);
                f.MarkDirty(T0);
                f.FlushNow();
                f.FlushIfDue(T0.AddHours(1));
                Assert.Equal(before, File.ReadAllBytes(path));

                var none = Make(null);
                none.Load(T0);
                none.MarkDirty(T0);
                none.FlushNow();
                Assert.True(none.WritesBlocked);
            }
        }

        [Fact]
        public void GalleryFile_corrupt_is_quarantined_and_future_version_is_never_rewritten()
        {
            using (var dir = new TempDir())
            {
                var bad = dir.File("gallery.txt");
                File.WriteAllText(bad, "junk");
                var f = Make(bad);
                f.Load(T0);
                Assert.False(f.WritesBlocked);
                Assert.False(File.Exists(bad));
                Assert.Contains(".corrupt-", Directory.GetFiles(dir.Path).Single());
            }
            using (var dir = new TempDir())
            {
                var path = dir.File("gallery.txt");
                File.WriteAllText(path, "jangseoru.gallery v2\nx\n");
                var before = File.ReadAllBytes(path);
                var f = Make(path);
                f.Load(T0);
                Assert.True(f.WritesBlocked);
                f.MarkDirty(T0);
                f.FlushNow();
                Assert.Equal(before, File.ReadAllBytes(path));
            }
        }
    }
}
