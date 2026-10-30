using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Jangseoru.Core
{
    public enum FileReadStatus
    {
        Missing,
        Success,
        Failed
    }

    /// <summary>File primitives behind <see cref="AtomicFile"/>; the seam used to inject failures in tests.</summary>
    internal interface IFileOps
    {
        /// <summary>Creates a new file (must not exist), writes all bytes and flushes to disk.</summary>
        void WriteNew(string path, byte[] data);
        bool Exists(string path);
        /// <summary>Atomically swaps <paramref name="tempPath"/> over the existing <paramref name="destPath"/>.</summary>
        void Replace(string tempPath, string destPath);
        void Move(string source, string dest);
        void Delete(string path);
    }

    internal sealed class RealFileOps : IFileOps
    {
        public static readonly RealFileOps Instance = new RealFileOps();

        public void WriteNew(string path, byte[] data)
        {
            using (var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                fs.Write(data, 0, data.Length);
                fs.Flush(true);
            }
        }

        public bool Exists(string path) => File.Exists(path);
        public void Replace(string tempPath, string destPath) => File.Replace(tempPath, destPath, null);
        public void Move(string source, string dest) => File.Move(source, dest);
        public void Delete(string path) => File.Delete(path);
    }

    /// <summary>
    /// Mod-owned file I/O that never destroys the original: writes go to a sibling temp file and are
    /// swapped in; there is deliberately no delete-then-move fallback.
    /// </summary>
    public static class AtomicFile
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        /// <summary>Never throws. The status comes from what opening the file actually did.</summary>
        public static FileReadStatus ReadAllText(string path, out string text)
        {
            text = null;
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var reader = new StreamReader(fs, Utf8, true))
                {
                    text = reader.ReadToEnd();
                    return FileReadStatus.Success;
                }
            }
            catch (FileNotFoundException) { return FileReadStatus.Missing; }
            catch (DirectoryNotFoundException) { return FileReadStatus.Missing; }
            catch (Exception) { text = null; return FileReadStatus.Failed; }
        }

        /// <summary>Throws on failure; on failure the existing file is left untouched.</summary>
        public static void WriteAllText(string path, string text) => WriteAllText(path, text, RealFileOps.Instance);

        internal static void WriteAllText(string path, string text, IFileOps ops)
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                ops.WriteNew(temp, Utf8.GetBytes(text ?? string.Empty));
                if (ops.Exists(path)) ops.Replace(temp, path);
                else ops.Move(temp, path);
            }
            catch
            {
                // Only our own temp file is cleaned up; the destination is never deleted here.
                try { if (ops.Exists(temp)) ops.Delete(temp); } catch { }
                throw;
            }
        }

        /// <summary>
        /// Moves <paramref name="path"/> to a unique <c>.corrupt-&lt;UTC&gt;-&lt;suffix&gt;</c> name. Returns the new path,
        /// or null if the file is missing or the move fails. Never throws, never overwrites.
        /// </summary>
        public static string Quarantine(string path, DateTime utcNow) => Quarantine(path, utcNow, null, RealFileOps.Instance);

        internal static string Quarantine(string path, DateTime utcNow, Func<string> suffix) => Quarantine(path, utcNow, suffix, RealFileOps.Instance);

        internal static string Quarantine(string path, DateTime utcNow, Func<string> suffix, IFileOps ops)
        {
            try
            {
                if (!ops.Exists(path)) return null;

                var stamp = utcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
                var unique = suffix != null ? suffix() : Guid.NewGuid().ToString("N").Substring(0, 8);
                var baseName = path + ".corrupt-" + stamp + "-" + unique;

                for (var attempt = 0; attempt < 100; attempt++)
                {
                    var candidate = attempt == 0 ? baseName : baseName + "-" + attempt.ToString(CultureInfo.InvariantCulture);
                    if (ops.Exists(candidate)) continue;
                    ops.Move(path, candidate);
                    return candidate;
                }
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
