using System;
using System.Collections.Generic;
using System.IO;

namespace Jangseoru.Core.Tests
{
    /// <summary>Captures log lines so tests can assert on warnings.</summary>
    internal sealed class ListLog : ILog
    {
        public readonly List<string> Infos = new List<string>();
        public readonly List<string> Warns = new List<string>();
        public readonly List<string> Errors = new List<string>();

        public void Info(string message) => Infos.Add(message);
        public void Warn(string message) => Warns.Add(message);
        public void Error(string message) => Errors.Add(message);
    }

    /// <summary>Real file operations with switchable failures and call counters.</summary>
    internal sealed class InjectableFileOps : IFileOps
    {
        private readonly IFileOps real = RealFileOps.Instance;

        public bool FailWriteNew;
        public bool FailReplace;
        public bool FailMove;
        public int WriteNewCalls;

        public void WriteNew(string path, byte[] data)
        {
            WriteNewCalls++;
            if (FailWriteNew) throw new IOException("injected write failure");
            real.WriteNew(path, data);
        }

        public bool Exists(string path) => real.Exists(path);

        public void Replace(string tempPath, string destPath)
        {
            if (FailReplace) throw new IOException("injected replace failure");
            real.Replace(tempPath, destPath);
        }

        public void Move(string source, string dest)
        {
            if (FailMove) throw new IOException("injected move failure");
            real.Move(source, dest);
        }

        public void Delete(string path) => real.Delete(path);
    }
}
