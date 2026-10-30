using System;

namespace Jangseoru.Core
{
    /// <summary>
    /// Turns "the game showed this background / big picture / video" into an entry of the seen-media file. Only keys of the
    /// game's own catalog are recorded (anything else, including another mod's key, is ignored). Memory change plus MarkDirty
    /// only; the debounced FlushIfDue does the disk write. Never throws.
    /// </summary>
    public sealed class MediaRecorder
    {
        private readonly MediaFile file;
        private readonly ILog log;

        public MediaRecorder(MediaFile file, ILog log)
        {
            this.file = file;
            this.log = log;
        }

        public void OnShown(MediaKind kind, string key, DateTime utcNow)
        {
            try
            {
                if (file == null || !MediaCatalog.Contains(kind, key)) return;
                if (file.Store.Add(kind, key)) file.MarkDirty(utcNow);
            }
            catch (Exception e)
            {
                try { log?.Warn("media record failed (" + e.GetType().Name + ")"); } catch { }
            }
        }
    }
}
