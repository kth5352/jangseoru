using System;
using System.Collections.Generic;
using Jangseoru.Core;
using UnityEngine;

namespace Jangseoru.Plugin
{
    /// <summary>
    /// The pieces of the game's bag-tab look (diamond frames, seal, brush stroke, list frame), shipped inside the plugin because the
    /// game keeps them in its Story scene, which the title never loads. They are decoded into textures and sprites this plugin owns,
    /// created on first use and destroyed in <see cref="Uninstall"/>; nothing of the game is touched. Never throws.
    /// </summary>
    internal static class ItemArt
    {
        public const string FrameOuter = "icon_item_back_2", FrameOrnate = "icon_item_back_3", Seal = "icon_seal_2",
            Brush = "icon_brush_2", ListFrame = "back_frame_small";

        private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        private static readonly List<Texture2D> textures = new List<Texture2D>();
        private static readonly HashSet<string> tried = new HashSet<string>();
        private static ILog log;

        public static void SetLog(ILog logger) => log = logger;

        /// <summary>The sprite of an embedded picture, or null when it cannot be decoded (the caller then draws a plain stand-in).</summary>
        public static Sprite Get(string name)
        {
            if (sprites.TryGetValue(name, out var found) && found != null) return found;
            if (!tried.Add(name)) return null;
            try
            {
                using (var st = typeof(ItemArt).Assembly.GetManifestResourceStream("Jangseoru." + name + ".png"))
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
                    tex.name = "jangseoru_" + name;
                    textures.Add(tex);
                    // The game's 9-slice border for the list frame is 25 on every side.
                    var border = name == ListFrame ? new Vector4(25f, 25f, 25f, 25f) : Vector4.zero;
                    var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
                    sprite.name = tex.name;
                    sprites[name] = sprite;
                    return sprite;
                }
            }
            catch (Exception e)
            {
                try { log?.Warn("item art " + name + " unavailable (" + e.GetType().Name + ")"); } catch { }
                return null;
            }
        }

        /// <summary>Destroys the sprites and textures we made. Idempotent; never throws.</summary>
        public static void Uninstall()
        {
            try
            {
                foreach (var s in sprites.Values) if (s != null) UnityEngine.Object.Destroy(s);
                foreach (var t in textures) if (t != null) UnityEngine.Object.Destroy(t);
            }
            catch (Exception) { }
            sprites.Clear();
            textures.Clear();
            tried.Clear();
        }
    }
}
