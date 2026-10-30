using System;

namespace Jangseoru.Core
{
    /// <summary>
    /// Turns "the game set an item's owned count" into an entry of the obtained-items file. Memory change plus MarkDirty only;
    /// the debounced FlushIfDue does the disk write. A count of zero or less (consumed, reset) records nothing and never
    /// removes an unlock. Never throws.
    /// </summary>
    public sealed class ItemRecorder
    {
        private readonly ItemFile file;
        private readonly ILog log;

        public ItemRecorder(ItemFile file, ILog log)
        {
            this.file = file;
            this.log = log;
        }

        public void OnCount(ItemKind kind, string id, int count, DateTime utcNow)
        {
            try
            {
                if (file == null || count <= 0) return;
                if (file.Store.Add(kind, id)) file.MarkDirty(utcNow);
            }
            catch (Exception e)
            {
                try { log?.Warn("item record failed (" + e.GetType().Name + ")"); } catch { }
            }
        }
    }
}
