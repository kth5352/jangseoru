using System;
using Jangseoru.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Jangseoru.Plugin
{
    /// <summary>
    /// Background collection. The game's backgrounds are sprites on scene objects (story, battle and combat scenes) set without any
    /// method call a hook could use, so once a second this looks, read-only, at the active sprite renderers and records those whose
    /// sprite is one of the catalog's backgrounds. Nothing is patched, modified or kept. A failure switches it off for the session.
    /// </summary>
    internal static class BackgroundWatcher
    {
        private const float Interval = 1f;
        private static MediaRecorder recorder;
        private static ILog log;
        private static bool on;
        private static float next;
        // Each sprite object is checked once (its name read and matched); later scans skip it. Bounded.
        private static readonly SeenOnce checkedSprites = new SeenOnce(8192);

        public static void Install(MediaRecorder mediaRecorder, ILog logger)
        {
            recorder = mediaRecorder;
            log = logger;
            on = mediaRecorder != null;
            try { log?.Info("media self-check: background watcher=" + (on ? "ON" : "OFF")); } catch { }
        }

        public static void Disable() { on = false; }

        /// <summary>Called from the plugin's Update; does real work at most once a second.</summary>
        public static void Tick()
        {
            if (!on) return;
            try
            {
                var now = Time.unscaledTime;
                if (now < next) return;
                next = now + Interval;
                var stamp = DateTime.UtcNow;
                foreach (var renderer in Object.FindObjectsOfType<SpriteRenderer>())
                {
                    if (renderer == null || !renderer.enabled) continue;
                    var sprite = renderer.sprite;
                    if (sprite == null || !renderer.gameObject.activeInHierarchy) continue;
                    if (!checkedSprites.FirstTime(sprite.GetInstanceID())) continue; // already recorded or not a background
                    if (!MediaCatalog.Contains(MediaKind.Background, sprite.name)) continue;
                    recorder.OnShown(MediaKind.Background, sprite.name, stamp);
                }
            }
            catch (Exception e)
            {
                on = false;
                try { log?.Warn("background collection disabled: " + e.GetType().Name); } catch { }
            }
        }
    }
}
