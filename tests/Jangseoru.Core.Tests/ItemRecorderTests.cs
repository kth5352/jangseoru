using System;
using System.IO;
using Xunit;

namespace Jangseoru.Core.Tests
{
    public class ItemRecorderTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 10, 21, 5, 0, 0, DateTimeKind.Utc);
        private static readonly TimeSpan Debounce = TimeSpan.FromSeconds(2);

        private static ItemFile Open(string path, IFileOps ops = null)
        {
            var f = new ItemFile(path, new ListLog(), Debounce, ops ?? RealFileOps.Instance);
            f.Load(T0);
            return f;
        }

        [Fact]
        public void Positive_count_records_once_and_marks_dirty()
        {
            using (var dir = new TempDir())
            {
                var path = dir.File("items.txt");
                var ops = new InjectableFileOps();
                var f = Open(path, ops);
                var r = new ItemRecorder(f, new ListLog());

                r.OnCount(ItemKind.Book, "1000", 1, T0);
                r.OnCount(ItemKind.Book, "1000", 3, T0);
                Assert.True(f.Store.Has(ItemKind.Book, "1000"));
                Assert.Equal(1, f.Store.Count);

                f.FlushIfDue(T0.AddSeconds(2));
                Assert.Equal(1, ops.WriteNewCalls);
                f.FlushIfDue(T0.AddSeconds(9)); // the second call changed nothing, so nothing is dirty again
                Assert.Equal(1, ops.WriteNewCalls);
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(int.MinValue)]
        public void Zero_or_negative_count_records_nothing(int count)
        {
            using (var dir = new TempDir())
            {
                var f = Open(dir.File("items.txt"));
                var r = new ItemRecorder(f, new ListLog());
                r.OnCount(ItemKind.Misc, "0001", count, T0);
                Assert.Equal(0, f.Store.Count);
                f.FlushIfDue(T0.AddSeconds(60));
                Assert.False(File.Exists(dir.File("items.txt")));
            }
        }

        [Fact]
        public void Zero_count_never_removes_an_existing_unlock()
        {
            using (var dir = new TempDir())
            {
                var f = Open(dir.File("items.txt"));
                var r = new ItemRecorder(f, new ListLog());
                r.OnCount(ItemKind.Special, "1001", 2, T0);
                r.OnCount(ItemKind.Special, "1001", 0, T0);   // consumed / Reset
                r.OnCount(ItemKind.Special, "1001", -4, T0);
                Assert.True(f.Store.Has(ItemKind.Special, "1001"));
            }
        }

        [Fact]
        public void Load_storm_writes_the_file_once_after_the_debounce()
        {
            using (var dir = new TempDir())
            {
                var path = dir.File("items.txt");
                var ops = new InjectableFileOps();
                var f = Open(path, ops);
                var r = new ItemRecorder(f, new ListLog());

                // a save load calls SetCount for every item, owned or not
                for (var i = 0; i < 300; i++)
                {
                    r.OnCount(ItemKind.Book, (1000 + i % 100).ToString(), i % 3 == 0 ? 0 : 1, T0.AddMilliseconds(i));
                    f.FlushIfDue(T0.AddMilliseconds(i));
                }
                Assert.Equal(0, ops.WriteNewCalls); // still inside the debounce window
                f.FlushIfDue(T0.AddSeconds(3));
                Assert.Equal(1, ops.WriteNewCalls);
                Assert.True(f.Store.Count > 0);
            }
        }

        [Fact]
        public void Null_file_or_bad_id_never_throws()
        {
            var none = new ItemRecorder(null, new ListLog());
            none.OnCount(ItemKind.Book, "1000", 1, T0);

            using (var dir = new TempDir())
            {
                var f = Open(dir.File("items.txt"));
                var r = new ItemRecorder(f, null);
                r.OnCount(ItemKind.Book, null, 1, T0);
                r.OnCount(ItemKind.Book, "bad id/with:chars", 1, T0);
                r.OnCount(ItemKind.Book, "", 1, T0);
                Assert.Equal(0, f.Store.Count);
            }
        }
    }
}
