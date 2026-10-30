using System;
using System.Reflection;
using HarmonyLib;
using Jangseoru.Core;

namespace Jangseoru.Plugin
{
    /// <summary>
    /// Big picture and video collection (the 큰 그림 / 영상 tabs), independent of every other feature: Postfix on the two iterator
    /// methods that show them; only the script key is read. The key the game loads is the script key with a fixed prefix
    /// ("pic_" / "video_"), which is how the catalog names them. Each hook switches itself off on its first exception.
    /// </summary>
    internal static class MediaHooks
    {
        private static MediaRecorder recorder;
        private static ILog log;
        private static volatile bool bigOn, videoOn;

        public static void Disable() { bigOn = videoOn = false; }

        public static void Install(Harmony harmony, MediaRecorder mediaRecorder, GameAccess access, ILog logger)
        {
            recorder = mediaRecorder;
            log = logger;
            bigOn = recorder != null && Patch(harmony, access.ShowBigPicture, nameof(BigPicturePostfix), "big picture collection");
            videoOn = recorder != null && Patch(harmony, access.VideoOpen, nameof(VideoPostfix), "video collection");
            try { log.Info("media self-check: bigPicture=" + (bigOn ? "ON" : "OFF") + " video=" + (videoOn ? "ON" : "OFF")); } catch { }
        }

        private static bool Patch(Harmony harmony, MethodBase target, string postfix, string feature)
            => SafePatcher.TryPostfix(harmony, target, typeof(MediaHooks).GetMethod(postfix, BindingFlags.Static | BindingFlags.NonPublic), feature, log);

        // StoryMainUI.ShowBigPicture(key, color) is an iterator: this runs when it is created, which is enough to know the key.
        private static void BigPicturePostfix(string key)
        {
            try
            {
                if (!bigOn || string.IsNullOrEmpty(key)) return;
                recorder.OnShown(MediaKind.BigPicture, "pic_" + key, DateTime.UtcNow);
            }
            catch (Exception e) { bigOn = false; Warn("big picture", e); }
        }

        // CgVideoPanel.Open(key, fadeDuration): an iterator too.
        private static void VideoPostfix(string key)
        {
            try
            {
                if (!videoOn || string.IsNullOrEmpty(key)) return;
                recorder.OnShown(MediaKind.Video, "video_" + key, DateTime.UtcNow);
            }
            catch (Exception e) { videoOn = false; Warn("video", e); }
        }

        private static void Warn(string what, Exception e)
        {
            try { log?.Warn(what + " collection disabled: hook threw " + e.GetType().Name); } catch { }
        }
    }
}
