using System;

namespace Jangseoru.Core
{
    /// <summary>
    /// Disk persistence for <see cref="ByeolhoStore"/>. Never throws out of any public member and never
    /// overwrites a file it could not read or understand: in those cases <see cref="WritesBlocked"/> is set
    /// and the session keeps collecting in memory only. The behavior lives in <see cref="StoreFileCore{TStore}"/>.
    /// </summary>
    public sealed class StoreFile
    {
        private readonly StoreFileCore<ByeolhoStore> core;

        public StoreFile(string filePath, ILog log, TimeSpan debounce) : this(filePath, log, debounce, RealFileOps.Instance) { }

        internal StoreFile(string filePath, ILog log, TimeSpan debounce, IFileOps ops)
        {
            core = new StoreFileCore<ByeolhoStore>(filePath, log, debounce, ops,
                () => new ByeolhoStore(), ByeolhoStore.Parse, s => s.Serialize(), s => s.ReadOnly);
        }

        public ByeolhoStore Store => core.Store;

        /// <summary>The last write failed; retried every five minutes while still dirty.</summary>
        public bool Degraded => core.Degraded;

        /// <summary>Nothing will ever be written this session (unreadable/future/unquarantinable file or no valid path).</summary>
        public bool WritesBlocked => core.WritesBlocked;

        public void Load(DateTime utcNow) => core.Load(utcNow);

        public void MarkDirty(DateTime utcNow) => core.MarkDirty(utcNow);

        public void FlushIfDue(DateTime utcNow) => core.FlushIfDue(utcNow);

        public void FlushNow() => core.FlushNow(DateTime.UtcNow);

        internal void FlushNow(DateTime utcNow) => core.FlushNow(utcNow);
    }
}
