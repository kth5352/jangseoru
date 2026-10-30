using System;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace Jangseoru.Core.Tests
{
    public class StoreFileTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 10, 20, 4, 0, 0, DateTimeKind.Utc);
        private static readonly TimeSpan Debounce = TimeSpan.FromSeconds(2);

        private static StoreFile Make(string path, ListLog log = null, IFileOps ops = null)
            => new StoreFile(path, log ?? new ListLog(), Debounce, ops ?? RealFileOps.Instance);

        private static string[] Files(string dir) => Directory.GetFiles(dir).Select(Path.GetFileName).OrderBy(n => n).ToArray();

        // ---- StorePaths ----

        [Fact]
        public void Resolve_returns_sibling_folder_of_persistentDataPath()
        {
            Assert.Equal(@"C:\x\Obb Studio\JangseoruMod\byeolho.txt", StorePaths.Resolve(@"C:\x\Obb Studio\Mortal"));
            Assert.Equal(@"C:\x\Obb Studio\JangseoruMod\byeolho.txt", StorePaths.Resolve(@"C:\x\Obb Studio\Mortal" + "\\"));
            Assert.Equal(@"C:\사용자\Obb Studio\JangseoruMod\byeolho.txt", StorePaths.Resolve(@"C:\사용자\Obb Studio\Mortal"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(@"C:\")]
        public void Resolve_without_a_parent_returns_null(string input)
        {
            Assert.Null(StorePaths.Resolve(input));
        }

        // ---- Load ----

        [Fact]
        public void Load_missing_file_gives_empty_store_and_allows_writes()
        {
            using (var dir = new TempDir())
            {
                var f = Make(dir.File("byeolho.txt"));
                f.Load(T0);

                Assert.Empty(f.Store.Collected);
                Assert.False(f.WritesBlocked);
                Assert.False(f.Degraded);
            }
        }

        [Fact]
        public void Load_valid_file_parses_it()
        {
            using (var dir = new TempDir())
            {
                var path = dir.File("byeolho.txt");
                var seed = new ByeolhoStore();
                seed.Collect(3, T0, true);
                AtomicFile.WriteAllText(path, seed.Serialize());

                var f = Make(path);
                f.Load(T0);

                Assert.Equal(new[] { 3 }, f.Store.Collected.ToArray());
                Assert.False(f.WritesBlocked);
            }
        }

        [Fact]
        public void Load_corrupt_file_is_quarantined_and_writes_are_allowed()
        {
            using (var dir = new TempDir())
            {
                var path = dir.File("byeolho.txt");
                File.WriteAllText(path, "this is not a store");
                var log = new ListLog();
                var f = Make(path, log);

                f.Load(T0);

                Assert.Empty(f.Store.Collected);
                Assert.False(f.WritesBlocked);
                Assert.False(File.Exists(path));
                var quarantined = Files(dir.Path).Single();
                Assert.Contains(".corrupt-", quarantined);
                Assert.Equal("this is not a store", File.ReadAllText(Path.Combine(dir.Path, quarantined)));
                Assert.NotEmpty(log.Warns);

                f.Store.Collect(4, T0, true);
                f.MarkDirty(T0);
                f.FlushNow();
                Assert.True(File.Exists(path));
            }
        }

        [Fact]
        public void Load_unreadable_file_blocks_writes_and_preserves_original()
        {
            using (var dir = new TempDir())
            {
                var path = dir.File("byeolho.txt");
                File.WriteAllText(path, "jangseoru.byeolho v1\nglobal 3 50\n");
                var before = File.ReadAllBytes(path);
                StoreFile f;
                using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    f = Make(path);
                    f.Load(T0);
                }

                Assert.True(f.WritesBlocked);
                Assert.Empty(f.Store.Collected);

                f.Store.Collect(9, T0, true);
                f.MarkDirty(T0);
                f.FlushNow();
                f.FlushIfDue(T0.AddHours(1));

                Assert.Equal(before, File.ReadAllBytes(path));
                Assert.Single(Files(dir.Path));
            }
        }

        [Fact]
        public void Quarantine_failure_blocks_writes_and_preserves_original_bytes()
        {
            using (var dir = new TempDir())
            {
                var path = dir.File("byeolho.txt");
                File.WriteAllText(path, "garbage");
                var before = File.ReadAllBytes(path);
                var ops = new InjectableFileOps { FailMove = true };
                var f = Make(path, ops: ops);

                f.Load(T0);

                Assert.True(f.WritesBlocked);
                f.Store.Collect(1, T0, true);
                f.MarkDirty(T0);
                f.FlushNow();

                Assert.Equal(before, File.ReadAllBytes(path));
                Assert.Equal(0, ops.WriteNewCalls);
                Assert.Single(Files(dir.Path));
            }
        }

        [Fact]
        public void Future_version_file_blocks_writes_and_is_never_rewritten()
        {
            using (var dir = new TempDir())
            {
                var path = dir.File("byeolho.txt");
                var text = "jangseoru.byeolho v2\nsomething from the future\n";
                File.WriteAllText(path, text);
                var f = Make(path);

                f.Load(T0);
                f.Store.Collect(1, T0, true);
                f.MarkDirty(T0);
                f.FlushNow();

                Assert.True(f.WritesBlocked);
                Assert.True(f.Store.ReadOnly);
                Assert.Equal(text, File.ReadAllText(path));
                Assert.Single(Files(dir.Path));
            }
        }

        [Fact]
        public void Invalid_path_blocks_writes_without_throwing()
        {
            var f = new StoreFile(null, new ListLog(), Debounce);

            f.Load(T0);
            f.Store.Collect(1, T0, true);
            f.MarkDirty(T0);
            f.FlushNow();
            f.FlushIfDue(T0.AddHours(1));

            Assert.True(f.WritesBlocked);
        }

        // ---- Flush ----

        [Fact]
        public void FlushIfDue_writes_only_after_the_debounce_since_the_last_MarkDirty()
        {
            using (var dir = new TempDir())
            {
                var path = dir.File("byeolho.txt");
                var f = Make(path);
                f.Load(T0);
                f.Store.Collect(1, T0, true);

                f.MarkDirty(T0);
                f.FlushIfDue(T0.AddSeconds(1));
                Assert.False(File.Exists(path));

                f.MarkDirty(T0.AddSeconds(1.5));
                f.FlushIfDue(T0.AddSeconds(2.5));
                Assert.False(File.Exists(path));

                f.FlushIfDue(T0.AddSeconds(3.5));
                Assert.True(File.Exists(path));
            }
        }

        [Fact]
        public void FlushIfDue_does_nothing_when_not_dirty()
        {
            using (var dir = new TempDir())
            {
                var path = dir.File("byeolho.txt");
                var f = Make(path);
                f.Load(T0);

                f.FlushIfDue(T0.AddHours(1));
                f.FlushNow();

                Assert.False(File.Exists(path));
            }
        }

        [Fact]
        public void Flush_failure_degrades_retries_after_five_minutes_and_recovers()
        {
            using (var dir = new TempDir())
            {
                var path = dir.File("byeolho.txt");
                var ops = new InjectableFileOps { FailWriteNew = true };
                var log = new ListLog();
                var f = Make(path, log, ops);
                f.Load(T0);
                f.Store.Collect(1, T0, true);
                f.MarkDirty(T0);

                f.FlushIfDue(T0.AddSeconds(3)); // fails
                Assert.True(f.Degraded);
                Assert.Equal(1, ops.WriteNewCalls);
                Assert.NotEmpty(log.Warns);

                f.FlushIfDue(T0.AddSeconds(3).AddMinutes(4)); // too early
                Assert.Equal(1, ops.WriteNewCalls);

                f.FlushIfDue(T0.AddSeconds(3).AddMinutes(5)); // retry, still failing
                Assert.Equal(2, ops.WriteNewCalls);
                Assert.True(f.Degraded);

                ops.FailWriteNew = false;
                f.FlushIfDue(T0.AddSeconds(3).AddMinutes(10)); // retry succeeds
                Assert.False(f.Degraded);
                Assert.True(File.Exists(path));

                var calls = ops.WriteNewCalls;
                f.FlushIfDue(T0.AddHours(1));
                Assert.Equal(calls, ops.WriteNewCalls); // no longer dirty
            }
        }

        [Fact]
        public void Replace_failure_on_an_existing_store_keeps_the_previous_file_intact()
        {
            using (var dir = new TempDir())
            {
                var path = dir.File("byeolho.txt");
                var ops = new InjectableFileOps();
                var f = Make(path, ops: ops);
                f.Load(T0);
                f.Store.Collect(1, T0, true);
                f.MarkDirty(T0);
                f.FlushNow();
                var good = File.ReadAllBytes(path);

                ops.FailReplace = true;
                f.Store.Collect(2, T0, true);
                f.MarkDirty(T0);
                f.FlushNow();

                Assert.True(f.Degraded);
                Assert.Equal(good, File.ReadAllBytes(path));
                Assert.Single(Files(dir.Path));
            }
        }

        [Fact]
        public void FlushNow_ignores_debounce_and_double_shutdown_flush_writes_once()
        {
            using (var dir = new TempDir())
            {
                var path = dir.File("byeolho.txt");
                var ops = new InjectableFileOps();
                var f = Make(path, ops: ops);
                f.Load(T0);
                f.Store.Collect(1, T0, true);
                f.MarkDirty(T0);

                f.FlushNow();
                f.FlushNow();

                Assert.True(File.Exists(path));
                Assert.Equal(1, ops.WriteNewCalls);
            }
        }

        [Fact]
        public void Large_store_warns_once()
        {
            using (var dir = new TempDir())
            {
                var path = dir.File("byeolho.txt");
                var big = "jangseoru.byeolho v1\nfuture-record " + new string('x', 1_100_000) + "\n";
                File.WriteAllText(path, big);
                var log = new ListLog();
                var f = Make(path, log);
                f.Load(T0);

                f.MarkDirty(T0);
                f.FlushNow();
                f.MarkDirty(T0);
                f.FlushNow();

                Assert.Equal(1, log.Warns.Count(w => w.Contains("store size")));
            }
        }

        [Fact]
        public void Flush_then_Load_roundtrips_in_a_new_instance_with_a_korean_path()
        {
            using (var dir = new TempDir())
            {
                var path = dir.File("모드 데이터\\byeolho.txt");
                var a = Make(path);
                a.Load(T0);
                a.Store.GameVersion = "release_1.0.6000.4";
                a.Store.Collect(2, T0, true);
                a.Store.RecordSnapshot(new SnapshotKey("사용자", "001", 99));
                a.MarkDirty(T0);
                a.FlushNow();

                var b = Make(path);
                b.Load(T0);

                Assert.Equal(new[] { 2 }, b.Store.Collected.ToArray());
                Assert.Equal("release_1.0.6000.4", b.Store.GameVersion);
                Assert.Equal(SlotMatch.Exact, b.Store.ActivateSnapshot(new SnapshotKey("사용자", "001", 99)));
            }
        }
    }
}
