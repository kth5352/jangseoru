using System;

namespace Jangseoru.Core
{
    /// <summary>
    /// Disk persistence for <see cref="ItemStore"/> with the same guarantees as <see cref="GalleryFile"/>
    /// (never throws, never overwrites a file it could not read or understand). Shares <see cref="StoreFileCore{TStore}"/>.
    /// </summary>
    public sealed class ItemFile
    {
        private readonly StoreFileCore<ItemStore> core;

        public ItemFile(string path, ILog log, TimeSpan debounce) : this(path, log, debounce, RealFileOps.Instance) { }

        internal ItemFile(string path, ILog log, TimeSpan debounce, IFileOps ops)
        {
            core = new StoreFileCore<ItemStore>(path, log, debounce, ops,
                () => new ItemStore(), ItemStore.Parse, s => s.Serialize(), s => s.ReadOnly);
        }

        public ItemStore Store => core.Store;

        public bool Degraded => core.Degraded;

        public bool WritesBlocked => core.WritesBlocked;

        public void Load(DateTime utcNow) => core.Load(utcNow);

        public void MarkDirty(DateTime utcNow) => core.MarkDirty(utcNow);

        public void FlushIfDue(DateTime utcNow) => core.FlushIfDue(utcNow);

        public void FlushNow() => core.FlushNow(DateTime.UtcNow);

        internal void FlushNow(DateTime utcNow) => core.FlushNow(utcNow);
    }
}
