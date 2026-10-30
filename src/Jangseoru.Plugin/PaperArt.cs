using System;
using Jangseoru.Core;
using UnityEngine;

namespace Jangseoru.Plugin
{
    /// <summary>
    /// The paper background of the oath cards (<c>fix_pic_paper_002.png</c>), shipped inside the plugin and decoded into a texture and
    /// sprite this plugin owns. Created on first use, destroyed in <see cref="Uninstall"/>; the game's own sprite is untouched.
    /// Never throws; a missing or undecodable picture simply leaves the game's card background.
    /// </summary>
    internal static class PaperArt
    {
        private static Sprite sprite;
        private static Texture2D texture;
        private static bool tried;
        private static ILog log;

        public static void SetLog(ILog logger) => log = logger;

        public static Sprite Card
        {
            get
            {
                if (sprite != null) return sprite;
                if (tried) return null;
                tried = true;
                try
                {
                    using (var st = typeof(PaperArt).Assembly.GetManifestResourceStream("Jangseoru.fix_pic_paper_002.png"))
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
                        tex.name = "jangseoru_fix_pic_paper_002";
                        texture = tex;
                        sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                        sprite.name = tex.name;
                    }
                }
                catch (Exception e)
                {
                    sprite = null;
                    try { log?.Warn("card paper picture unavailable (" + e.GetType().Name + ")"); } catch { }
                }
                return sprite;
            }
        }

        /// <summary>Destroys the sprite and texture we made. Idempotent; never throws.</summary>
        public static void Uninstall()
        {
            try
            {
                if (sprite != null) UnityEngine.Object.Destroy(sprite);
                if (texture != null) UnityEngine.Object.Destroy(texture);
            }
            catch (Exception) { }
            sprite = null;
            texture = null;
            tried = false;
        }
    }
}
