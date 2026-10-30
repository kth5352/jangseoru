using System;
using System.IO;
using Xunit;

namespace Jangseoru.Core.Tests
{
    public class ItemFileTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 10, 21, 4, 0, 0, DateTimeKind.Utc);
        private static readonly TimeSpan Debounce = TimeSpan.FromSeconds(2);

        private static ItemFile Make(string path, ListLog log = null, IFileOps ops = null)
            => new ItemFile(path, log ?? new ListLog(), Debounce, ops ?? RealFileOps.Instance);

        [Fact]
        public void Missing_file_gives_empty_store_and_does_not_block()
        {
            using (var dir = new TempDir())
            {
                var f = Make(dir.File("items.txt"));
                f.Load(T0);
                Assert.Equal(0, f.Store.Count);
                Assert.False(f.WritesBlocked);
                Assert.False(f.Degraded);
            }
        }

        [Fact]
        public void Flush_waits_for_the_debounce_then_writes_once()
        {
            using (var dir = new TempDir())
            {
                var path = dir.File("items.txt");
                var ops = new InjectableFileOps();
                var f = Make(path, null, ops);
                f.Load(T0);
                f.Store.Add(ItemKind.Book, "1000");
                f.MarkDirty(T0);

                f.FlushIfDue(T0.AddSeconds(1));
                Assert.False(File.Exists(path));
                f.FlushIfDue(T0.AddSeconds(2));
                Assert.True(File.Exists(path));
                f.FlushIfDue(T0.AddSeconds(10)); // not dirty any more
                Assert.Equal(1, ops.WriteNewCalls);

                var g = Make(path);
                g.Load(T0);
                Assert.True(g.Store.Has(ItemKind.Book, "1000"));
            }
        }

        [Fact]
        public void Corrupt_file_is_set_aside_not_overwritten()
        {
            using (var dir = new TempDir())
            {
                var path = dir.File("items.txt");
                File.WriteAllText(path, "this is not an items file\n");
                var log = new ListLog();
                var f = Make(path, log);
                f.Load(T0);
                Assert.Equal(0, f.Store.Count);
                Assert.Contains(log.Warns, w => w.Contains("corrupt"));
                // the unreadable original still exists, under another name
                Assert.Contains(Directory.GetFiles(dir.Path), p => File.ReadAllText(p) == "this is not an items file\n");

                f.Store.Add(ItemKind.Misc, "0001");
                f.MarkDirty(T0);
                f.FlushIfDue(T0.AddSeconds(30));
                Assert.Contains(Directory.GetFiles(dir.Path), p => File.ReadAllText(p) == "this is not an items file\n");
                Assert.StartsWith(ItemStore.Header, File.ReadAllText(path));
            }
        }

        [Fact]
        public void Newer_version_file_is_never_modified()
        {
            using (var dir = new TempDir())
            {
                var path = dir.File("items.txt");
                File.WriteAllText(path, "jangseoru.items v9\nBook:1000\n");
                var f = Make(path);
                f.Load(T0);
                f.MarkDirty(T0);
                f.FlushIfDue(T0.AddSeconds(30));
                Assert.Equal("jangseoru.items v9\nBook:1000\n", File.ReadAllText(path));
            }
        }
    }
}
