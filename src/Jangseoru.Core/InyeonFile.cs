using System;

namespace Jangseoru.Core
{
    public sealed class InyeonFile
    {
        private readonly StoreFileCore<InyeonStore> core;
        public InyeonFile(string path, ILog log, TimeSpan debounce)
        {
            core = new StoreFileCore<InyeonStore>(path, log, debounce, RealFileOps.Instance,
                () => new InyeonStore(), InyeonStore.Parse, s => s.Serialize(), s => s.ReadOnly);
        }
        public InyeonStore Store => core.Store;
        public bool WritesBlocked => core.WritesBlocked;
        public bool Degraded => core.Degraded;
        public void Load(DateTime now) => core.Load(now);
        public void MarkDirty(DateTime now) => core.MarkDirty(now);
        public void FlushIfDue(DateTime now) => core.FlushIfDue(now);
        public void FlushNow() => core.FlushNow(DateTime.UtcNow);
    }
}
