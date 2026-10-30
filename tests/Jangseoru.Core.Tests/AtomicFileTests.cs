using System;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace Jangseoru.Core.Tests
{
    public class AtomicFileTests
    {
        [Fact]
        public void WriteAllText_then_ReadAllText_roundtrips_unicode_in_korean_spaced_path()
        {
            using (var dir = new TempDir())
            {
                var path = dir.File("하위 폴더\\별호 데이터.txt");
                AtomicFile.WriteAllText(path, "별호\n당문졸개");

                Assert.Equal(FileReadStatus.Success, AtomicFile.ReadAllText(path, out var text));
                Assert.Equal("별호\n당문졸개", text);
            }
        }

        [Fact]
        public void WriteAllText_replaces_existing_content()
        {
            using (var dir = new TempDir())
            {
                var path = dir.File("a.txt");
                AtomicFile.WriteAllText(path, "first");
                AtomicFile.WriteAllText(path, "second");

                AtomicFile.ReadAllText(path, out var text);
                Assert.Equal("second", text);
            }
        }

        [Fact]
        public void WriteAllText_leaves_no_temp_files()
        {
            using (var dir = new TempDir())
            {
                var path = dir.File("a.txt");
                AtomicFile.WriteAllText(path, "one");
                AtomicFile.WriteAllText(path, "two");

                Assert.Equal(new[] { "a.txt" }, Directory.GetFiles(dir.Path).Select(Path.GetFileName).ToArray());
            }
        }

        [Fact]
        public void ReadAllText_missing_file_returns_Missing()
        {
            using (var dir = new TempDir())
            {
                Assert.Equal(FileReadStatus.Missing, AtomicFile.ReadAllText(dir.File("nope.txt"), out var text));
                Assert.Null(text);
            }
        }

        [Fact]
        public void ReadAllText_missing_directory_returns_Missing()
        {
            using (var dir = new TempDir())
            {
                Assert.Equal(FileReadStatus.Missing, AtomicFile.ReadAllText(dir.File("no dir\\x.txt"), out _));
            }
        }

        [Fact]
        public void ReadAllText_locked_file_returns_Failed_not_Missing()
        {
            using (var dir = new TempDir())
            {
                var path = dir.File("locked.txt");
                File.WriteAllText(path, "data");
                using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    Assert.Equal(FileReadStatus.Failed, AtomicFile.ReadAllText(path, out var text));
                    Assert.Null(text);
                }
            }
        }

        [Fact]
        public void Replace_failure_preserves_original_bytes_and_cleans_own_temp()
        {
            using (var dir = new TempDir())
            {
                var path = dir.File("a.txt");
                File.WriteAllBytes(path, Encoding.UTF8.GetBytes("original"));
                var ops = new FailingOps { FailReplace = true };

                Assert.Throws<IOException>(() => AtomicFile.WriteAllText(path, "new", ops));

                Assert.Equal("original", File.ReadAllText(path, Encoding.UTF8));
                Assert.Equal(new[] { "a.txt" }, Directory.GetFiles(dir.Path).Select(Path.GetFileName).ToArray());
            }
        }

        [Fact]
        public void Unsupported_replace_does_not_fall_back_to_delete_and_move()
        {
            using (var dir = new TempDir())
            {
                var path = dir.File("a.txt");
                File.WriteAllBytes(path, Encoding.UTF8.GetBytes("original"));
                var ops = new FailingOps { ReplaceThrowsNotSupported = true };

                Assert.Throws<PlatformNotSupportedException>(() => AtomicFile.WriteAllText(path, "new", ops));

                Assert.Equal("original", File.ReadAllText(path, Encoding.UTF8));
                Assert.DoesNotContain(path, ops.Deleted);
                Assert.Equal(0, ops.MoveCalls);
            }
        }

        [Fact]
        public void Quarantine_moves_file_and_returns_new_path()
        {
            using (var dir = new TempDir())
            {
                var path = dir.File("a.txt");
                File.WriteAllText(path, "bad");

                var moved = AtomicFile.Quarantine(path, new DateTime(2026, 10, 20, 4, 15, 30, 123, DateTimeKind.Utc));

                Assert.NotNull(moved);
                Assert.False(File.Exists(path));
                Assert.True(File.Exists(moved));
                Assert.StartsWith(path + ".corrupt-20261020041530123-", moved);
                Assert.Equal("bad", File.ReadAllText(moved));
            }
        }

        [Fact]
        public void Quarantine_missing_file_returns_null_without_throwing()
        {
            using (var dir = new TempDir())
            {
                Assert.Null(AtomicFile.Quarantine(dir.File("nope.txt"), DateTime.UtcNow));
            }
        }

        [Fact]
        public void Quarantine_never_overwrites_an_existing_quarantine_file()
        {
            using (var dir = new TempDir())
            {
                var path = dir.File("a.txt");
                var now = new DateTime(2026, 10, 20, 4, 15, 30, DateTimeKind.Utc);

                File.WriteAllText(path, "first");
                var first = AtomicFile.Quarantine(path, now, () => "same");
                File.WriteAllText(path, "second");
                var second = AtomicFile.Quarantine(path, now, () => "same");

                Assert.NotEqual(first, second);
                Assert.Equal("first", File.ReadAllText(first));
                Assert.Equal("second", File.ReadAllText(second));
            }
        }

        [Fact]
        public void Quarantine_move_failure_returns_null_without_throwing_and_keeps_original()
        {
            using (var dir = new TempDir())
            {
                var path = dir.File("a.txt");
                File.WriteAllText(path, "bad");
                var ops = new FailingOps { FailMove = true };

                Assert.Null(AtomicFile.Quarantine(path, DateTime.UtcNow, null, ops));
                Assert.True(File.Exists(path));
            }
        }

        /// <summary>Real file operations with injectable failures; records what was deleted/moved.</summary>
        private sealed class FailingOps : IFileOps
        {
            private readonly IFileOps real = RealFileOps.Instance;
            public bool FailReplace;
            public bool ReplaceThrowsNotSupported;
            public bool FailMove;
            public System.Collections.Generic.List<string> Deleted = new System.Collections.Generic.List<string>();
            public int MoveCalls;

            public void WriteNew(string path, byte[] data) => real.WriteNew(path, data);
            public bool Exists(string path) => real.Exists(path);

            public void Replace(string tempPath, string destPath)
            {
                if (ReplaceThrowsNotSupported) throw new PlatformNotSupportedException("injected");
                if (FailReplace) throw new IOException("injected");
                real.Replace(tempPath, destPath);
            }

            public void Move(string source, string dest)
            {
                MoveCalls++;
                if (FailMove) throw new IOException("injected");
                real.Move(source, dest);
            }

            public void Delete(string path)
            {
                Deleted.Add(path);
                real.Delete(path);
            }
        }
    }
}
