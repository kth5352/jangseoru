using System;
using System.IO;

namespace Jangseoru.Core.Tests
{
    /// <summary>Unique scratch directory (Korean + space in the name on purpose); never touches real saves.</summary>
    internal sealed class TempDir : IDisposable
    {
        public string Path { get; }

        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Jangseoru 테스트 " + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string File(string name) => System.IO.Path.Combine(Path, name);

        public void Dispose()
        {
            try { Directory.Delete(Path, true); } catch { /* best effort */ }
        }
    }
}
