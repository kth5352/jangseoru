using System;
using Jangseoru.Core;
using UnityEngine;

namespace Jangseoru.Plugin
{
    /// <summary>
    /// The four bubble pictures of the first bubble presentation (<c>pic_event_bubble_1..4</c>). The game keeps them inside its
    /// Story scene, which the title never loads, so copies ship with the mod and are decoded into textures and sprites this
    /// plugin owns. They are created on first use and destroyed in <see cref="Uninstall"/>; nothing of the game is touched.
    /// Never throws.
    /// </summary>
    internal static class BubbleArt
    {
        private static readonly Sprite[] sprites = new Sprite[BubbleSets.Four.Frames.Count];
        private static readonly Texture2D[] textures = new Texture2D[BubbleSets.Four.Frames.Count];
        private static readonly bool[] tried = new bool[BubbleSets.Four.Frames.Count];
        private static ILog log;

        public static void SetLog(ILog logger) => log = logger;

        /// <summary>The sprite of a catalog key of the four-bubble set, or null when the key is not one of them or cannot be decoded.</summary>
        public static Sprite For(string catalogKey)
        {
            try
            {
                var frames = BubbleSets.Four.Frames;
                for (var i = 0; i < frames.Count; i++)
                    if (string.Equals(frames[i].CatalogKey, catalogKey, StringComparison.Ordinal)) return Get(i);
            }
            catch (Exception) { }
            return null;
        }

        private static Sprite Get(int index)
        {
            if (sprites[index] != null) return sprites[index];
            if (tried[index]) return null;
            tried[index] = true;
            try
            {
                using (var st = typeof(BubbleArt).Assembly.GetManifestResourceStream("Jangseoru.pic_event_bubble_" + (index + 1) + ".png"))
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
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!ImageConversion.LoadImage(tex, bytes, false))
                    {
                        UnityEngine.Object.Destroy(tex);
                        return null;
                    }
                    tex.name = "jangseoru_bubble_" + (index + 1);
                    textures[index] = tex;
                    var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                    sprite.name = tex.name;
                    sprites[index] = sprite;
                }
            }
            catch (Exception e)
            {
                sprites[index] = null;
                try { log?.Warn("bubble picture " + (index + 1) + " unavailable (" + e.GetType().Name + ")"); } catch { }
            }
            return sprites[index];
        }

        /// <summary>Destroys the sprites and textures we made. Idempotent; never throws.</summary>
        public static void Uninstall()
        {
            for (var i = 0; i < sprites.Length; i++)
            {
                try
                {
                    if (sprites[i] != null) UnityEngine.Object.Destroy(sprites[i]);
                    if (textures[i] != null) UnityEngine.Object.Destroy(textures[i]);
                }
                catch (Exception) { }
                sprites[i] = null;
                textures[i] = null;
                tried[i] = false;
            }
        }
    }
}
