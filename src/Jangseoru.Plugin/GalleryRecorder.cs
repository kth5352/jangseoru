using System;
using Jangseoru.Core;

namespace Jangseoru.Plugin
{
    /// <summary>
    /// Turns "the game showed event illustration X" into a gallery entry. Memory change plus MarkDirty only;
    /// the debounced FlushIfDue in Plugin.Update does the disk write. Never throws.
    /// </summary>
    internal sealed class GalleryRecorder
    {
        private readonly GalleryFile file;
        private readonly ILog log;

        public GalleryRecorder(GalleryFile file, ILog log)
        {
            this.file = file;
            this.log = log;
        }

        /// <summary>A story effect was started: records the picture of a bubble presentation frame. Same rules as pictures.</summary>
        public void OnEffect(string effectName, int play, DateTime utcNow)
        {
            try
            {
                if (file == null || !BubbleSets.TryFromEffect(effectName, play, out var catalogKey)) return;
                if (file.Store.AddPicture(catalogKey)) file.MarkDirty(utcNow);
            }
            catch (Exception e)
            {
                try { log?.Warn("bubble record failed (" + e.GetType().Name + ")"); } catch { }
            }
        }

        /// <summary>Records picture keys the game used by their catalog key (combat overlay images, story portraits).</summary>
        public void OnCatalogKeys(System.Collections.Generic.IEnumerable<string> catalogKeys, DateTime utcNow)
        {
            try
            {
                if (file == null || catalogKeys == null) return;
                var added = false;
                foreach (var key in catalogKeys)
                    if (file.Store.AddPicture(key)) added = true;
                if (added) file.MarkDirty(utcNow);
            }
            catch (Exception e)
            {
                try { log?.Warn("picture record failed (" + e.GetType().Name + ")"); } catch { }
            }
        }

        /// <summary>Records only catalog pictures; marks the file dirty only when the picture is new.</summary>
        public void OnPicture(string scriptKey, DateTime utcNow)
        {
            try
            {
                if (file == null || !PictureCatalog.TryNormalize(scriptKey, out var catalogKey)) return;
                if (file.Store.AddPicture(catalogKey)) file.MarkDirty(utcNow);
            }
            catch (Exception e)
            {
                try { log?.Warn("picture record failed (" + e.GetType().Name + ")"); } catch { }
            }
        }
    }
}
