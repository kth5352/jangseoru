using System;

namespace Jangseoru.Core
{
    /// <summary>
    /// Disk persistence for <see cref="OathStore"/> with the same guarantees as <see cref="GalleryFile"/>
    /// (never throws, never overwrites a file it could not read or understand). Shares <see cref="StoreFileCore{TStore}"/>.
    /// </summary>
    public sealed class OathFile
    {
        private readonly StoreFileCore<OathStore> core;

        public OathFile(string path, ILog log, TimeSpan debounce) : this(path, log, debounce, RealFileOps.Instance) { }

        internal OathFile(string path, ILog log, TimeSpan debounce, IFileOps ops)
        {
            core = new StoreFileCore<OathStore>(path, log, debounce, ops,
                () => new OathStore(), OathStore.Parse, s => s.Serialize(), s => s.ReadOnly);
        }

        public OathStore Store => core.Store;

        public bool Degraded => core.Degraded;

        public bool WritesBlocked => core.WritesBlocked;

        public void Load(DateTime utcNow) => core.Load(utcNow);

        public void MarkDirty(DateTime utcNow) => core.MarkDirty(utcNow);

        public void FlushIfDue(DateTime utcNow) => core.FlushIfDue(utcNow);

        public void FlushNow() => core.FlushNow(DateTime.UtcNow);

        internal void FlushNow(DateTime utcNow) => core.FlushNow(utcNow);
    }
}
