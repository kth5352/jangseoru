using System;
using Jangseoru.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Jangseoru.Plugin
{
    /// <summary>
    /// Holds a reference to the game's open-book sprite <c>pic_book_1</c> (only loaded while a Story scene is alive) so the
    /// 사건 detail in the title library can use it as its frame. The sprite is the game's: it is never modified or destroyed,
    /// only referenced. Captured after each scene load (a Story scene has it loaded with its presentation object), from the
    /// ShowPicture postfix and when the detail opens. Scans of the loaded sprites are throttled (only while nothing is held, at most once per
    /// <see cref="ScanInterval"/> seconds), so calling <see cref="TryCapture"/> from a hot path is cheap. Never throws.
    /// </summary>
    internal static class BookSpriteKeeper
    {
        public const float ScanInterval = 10f;

        private static Sprite held;
        private static Sprite own;      // decoded from the embedded copy; ours, destroyed in Uninstall
        private static Texture2D ownTex;
        private static bool ownTried;
        private static float nextScanAt = float.NegativeInfinity;
        private static bool captureLogged, scanWarned;
        private static ILog log;

        private static bool installed;

        public static void SetLog(ILog logger) => log = logger;

        /// <summary>The 사건 detail that uses the book is installed (scene capture on); hooks capture only then.</summary>
        public static bool CaptureInstalled => installed;

        /// <summary>
        /// Listens to scene loads (a Unity event, not a game patch). Only called once the library tab and its 사건 detail are
        /// actually installed; undone by <see cref="Uninstall"/>. Never throws.
        /// </summary>
        public static void InstallSceneCapture()
        {
            try
            {
                if (installed) return;
                SceneManager.sceneLoaded += OnSceneLoaded;
                installed = true;
            }
            catch (Exception e)
            {
                try { log?.Warn("book frame: scene-load capture unavailable (" + e.GetType().Name + ")"); } catch { }
            }
        }

        /// <summary>Removes the scene-load listener (plugin destroyed). Idempotent; never throws.</summary>
        public static void Uninstall()
        {
            try
            {
                if (!installed) return;
                SceneManager.sceneLoaded -= OnSceneLoaded;
                installed = false;
            }
            catch (Exception) { }
            try
            {
                if (own != null) UnityEngine.Object.Destroy(own);
                if (ownTex != null) UnityEngine.Object.Destroy(ownTex);
            }
            catch (Exception) { }
            own = null;
            ownTex = null;
            ownTried = false;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            try
            {
                if (held != null) return;
                nextScanAt = float.NegativeInfinity; // one scan per scene load, whatever the throttle says
                TryCapture();
            }
            catch (Exception) { }
        }

        /// <summary>The held book sprite, or null when none was captured or the held one has been destroyed.</summary>
        public static Sprite Book
        {
            get
            {
                try
                {
                    if (held != null) return held; // Unity null: a destroyed sprite reads as null
                    return Own();
                }
                catch (Exception) { return null; }
            }
        }

        /// <summary>The embedded copy of the book as a sprite we own (created once; null if it cannot be decoded).</summary>
        private static Sprite Own()
        {
            if (own != null) return own;
            if (ownTried) return null;
            ownTried = true;
            try
            {
                using (var st = typeof(BookSpriteKeeper).Assembly.GetManifestResourceStream("Jangseoru.pic_book_1.png"))
                {
                    if (st == null) return null;
                    var bytes = new byte[st.Length];
                    var n = 0;
                    while (n < bytes.Length)
                    {
                        var r = st.Read(bytes, n, bytes.Length - n);
                        if (r <= 0) break;
                        n += r;
                    }
                    ownTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!ImageConversion.LoadImage(ownTex, bytes, false)) return null;
                    ownTex.name = "jangseoru_book";
                    own = Sprite.Create(ownTex, new Rect(0, 0, ownTex.width, ownTex.height), new Vector2(0.5f, 0.5f), 100f);
                    own.name = "jangseoru_book";
                    Info("book frame: using the embedded copy (" + ownTex.width + "x" + ownTex.height + ")");
                }
            }
            catch (Exception e)
            {
                own = null;
                try { log?.Warn("book frame: embedded copy unavailable (" + e.GetType().Name + "); the 사건 detail uses paper pages"); } catch { }
            }
            return own;
        }

        /// <summary>Looks for a loaded sprite named exactly pic_book_1 unless one is already held; throttled.</summary>
        public static void TryCapture()
        {
            try
            {
                if (held != null) return;
                held = null; // drop a destroyed reference so the next scan can replace it

                var now = Time.realtimeSinceStartup;
                if (now < nextScanAt) return;
                nextScanAt = now + ScanInterval;

                foreach (var s in Resources.FindObjectsOfTypeAll<Sprite>())
                {
                    if (s == null || !string.Equals(s.name, GameContract.BookSpriteName, StringComparison.Ordinal)) continue;
                    held = s;
                    break;
                }

                if (held != null)
                {
                    var r = held.rect;
                    Info("book frame " + GameContract.BookSpriteName + " captured (" + (int)r.width + "x" + (int)r.height + ")"
                         + (captureLogged ? " again" : ""));
                    captureLogged = true;
                }
            }
            catch (Exception e)
            {
                held = null;
                if (!scanWarned)
                {
                    scanWarned = true;
                    try { log?.Warn("book frame lookup failed (" + e.GetType().Name + "); the 사건 detail uses paper pages"); } catch { }
                }
            }
        }

        private static void Info(string m) { try { log?.Info(m); } catch { } }
    }
}
