using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace Jangseoru.Plugin
{
    /// <summary>
    /// The game's brush-style fonts (the status window's 열전 heading) are already loaded by the game; this only references them
    /// by name and sets our own Text objects' font. Nothing is copied, modified or destroyed.
    /// </summary>
    internal static class BrushFont
    {
        public const string HangulName = "dimibang", HanName = "DFXingShuStd-W5";
        private static Font hangul, han;
        private static bool searched;
        private static readonly ConditionalWeakTable<Text, Font> original = new ConditionalWeakTable<Text, Font>();
        /// <summary>Optional one-line log sink (set by the gallery UI).</summary>
        public static Action<string> Report;

        private static void Find()
        {
            if (searched) return;
            searched = true;
            try
            {
                foreach (var f in Resources.FindObjectsOfTypeAll<Font>())
                {
                    if (f == null) continue;
                    if (hangul == null && f.name == HangulName) hangul = f;
                    else if (han == null && f.name == HanName) han = f;
                }
                Report?.Invoke("brush fonts: " + HangulName + "=" + (hangul != null ? "found" : "missing") + " " + HanName + "=" + (han != null ? "found" : "missing"));
            }
            catch { }
        }

        /// <summary>
        /// A loaded game font for texts we create from scratch: the game's serif (wide coverage), else a brush font, else any loaded
        /// font. <see cref="Apply"/> then swaps in a brush font where it has every character, and falls back to this one otherwise.
        /// </summary>
        public static Font Fallback()
        {
            Find();
            try
            {
                Font any = null;
                foreach (var f in Resources.FindObjectsOfTypeAll<Font>())
                {
                    if (f == null) continue;
                    if (f.name == "SourceHanSerifTC-Bold" || f.name == "SourceHanSerif-Bold") return f;
                    if (any == null) any = f;
                }
                return hangul ?? han ?? any;
            }
            catch { return hangul ?? han; }
        }

        /// <summary>
        /// Sets one of the game's loaded fonts by exact name on our own Text when that font has every character of its text;
        /// returns whether it did. Our clone's localized-font component is dropped so the game cannot swap the font back.
        /// </summary>
        public static bool Use(Text text, string fontName)
        {
            try
            {
                if (text == null) return false;
                Font font = null;
                foreach (var f in Resources.FindObjectsOfTypeAll<Font>())
                    if (f != null && f.name == fontName) { font = f; break; }
                if (font == null || !Covers(font, text.text ?? "")) return false;
                foreach (var c in text.GetComponents<Component>())
                    if (c != null && c.GetType().Name == "LeanLocalizedTextFont") UnityEngine.Object.DestroyImmediate(c);
                text.font = font;
                return true;
            }
            catch { return false; }
        }

        private static bool Covers(Font font, string text)
        {
            try
            {
                foreach (var c in text)
                    if (c != '\n' && c != ' ' && !font.HasCharacter(c)) return false;
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// Gives <paramref name="text"/> the first brush font that has every character of its current text; leaves the game font
        /// when none does. <paramref name="hanFirst"/> prefers the hanja (xingshu) brush for Chinese-numeral labels. The localized-font component of our clone is dropped so the game cannot swap the font back.
        /// </summary>
        public static void Apply(Text text, bool hanFirst = false)
        {
            try
            {
                if (text == null) return;
                Find();
                var value = text.text ?? "";
                var first = hanFirst ? han : hangul;
                var second = hanFirst ? hangul : han;
                var font = first != null && Covers(first, value) ? first : second != null && Covers(second, value) ? second : null;
                if (!original.TryGetValue(text, out var before)) { before = text.font; original.Add(text, before); }
                if (font == null) { text.font = before; if (value.Length <= 4) Report?.Invoke("brush font none covers: \"" + value + "\" (dimibang " + (hangul != null && Covers(hangul, value)) + ", xingshu " + (han != null && Covers(han, value)) + ")"); return; } // no brush font covers this text: back to the game font
                foreach (var c in text.GetComponents<Component>())
                    if (c != null && c.GetType().Name == "LeanLocalizedTextFont") UnityEngine.Object.DestroyImmediate(c);
                text.font = font;
                if (value.Length <= 4) Report?.Invoke("brush font applied: \"" + value.Replace("\n", "/") + "\" -> " + font.name);
            }
            catch { }
        }
    }
}
