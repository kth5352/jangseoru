using System;

namespace Jangseoru.Core
{
    /// <summary>
    /// Disk persistence for <see cref="MediaStore"/> (<c>media.txt</c>) with the same guarantees as <see cref="GalleryFile"/>
    /// (never throws, never overwrites a file it could not read or understand). Shares <see cref="StoreFileCore{TStore}"/>.
    /// </summary>
    public sealed class MediaFile
    {
        private readonly StoreFileCore<MediaStore> core;

        public MediaFile(string path, ILog log, TimeSpan debounce) : this(path, log, debounce, RealFileOps.Instance) { }

        internal MediaFile(string path, ILog log, TimeSpan debounce, IFileOps ops)
        {
            core = new StoreFileCore<MediaStore>(path, log, debounce, ops,
                () => new MediaStore(), MediaStore.Parse, s => s.Serialize(), s => s.ReadOnly);
        }

        public MediaStore Store => core.Store;

        public bool Degraded => core.Degraded;

        public bool WritesBlocked => core.WritesBlocked;

        public void Load(DateTime utcNow) => core.Load(utcNow);

        public void MarkDirty(DateTime utcNow) => core.MarkDirty(utcNow);

        public void FlushIfDue(DateTime utcNow) => core.FlushIfDue(utcNow);

        public void FlushNow() => core.FlushNow(DateTime.UtcNow);

        internal void FlushNow(DateTime utcNow) => core.FlushNow(utcNow);
    }
}
