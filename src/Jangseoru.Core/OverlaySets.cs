using System;
using System.Collections.Generic;

namespace Jangseoru.Core
{
    public enum OverlayLayout
    {
        /// <summary>Every picture covers the whole screen (transparent layers stacked on each other).</summary>
        FullScreen,
        /// <summary>Tall pictures side by side, in order from left to right.</summary>
        Strips,
    }

    /// <summary>
    /// A presentation that shows several catalog pictures one after another on a dark backdrop: the two combat effects
    /// that overlay full-screen layers (their timings are read from the game's C_Effect_106 assets) and the four strips of
    /// the story's "cut in". Pure data and timeline maths; the Unity side only applies the numbers.
    /// </summary>
    public sealed class OverlaySet
    {
        public string Id { get; }
        public string Label { get; }
        public OverlayLayout Layout { get; }
        /// <summary>Catalog keys in the order the presentation shows them.</summary>
        public IReadOnlyList<string> Keys { get; }
        /// <summary>Seconds a picture takes to fade in.</summary>
        public float FadeIn { get; }
        /// <summary>Seconds between a picture's fade-in end and the next picture's start.</summary>
        public float Interval { get; }
        /// <summary>Seconds all pictures stay once the last one is in; zero or less keeps them until the view closes.</summary>
        public float Hold { get; }
        public float FadeOut { get; }

        internal OverlaySet(string id, string label, OverlayLayout layout, float fadeIn, float interval, float hold, float fadeOut, params string[] keys)
        {
            Id = id;
            Label = label;
            Layout = layout;
            FadeIn = fadeIn;
            Interval = interval;
            Hold = hold;
            FadeOut = fadeOut;
            Keys = keys;
        }

        /// <summary>The key of the card in the 사건 list.</summary>
        public string CardKey => Keys[0];

        /// <summary>The set counts as seen when its own first picture was shown (the others may be shared with another set).</summary>
        public string UnlockKey => Keys[0];

        /// <summary>When picture <paramref name="index"/> starts fading in, in seconds from the start.</summary>
        public float StartOf(int index) => index * (FadeIn + Interval);

        /// <summary>When the last picture is fully in.</summary>
        public float AllInAt => Keys.Count == 0 ? 0f : StartOf(Keys.Count - 1) + FadeIn;

        /// <summary>Length of one play: all in, hold, fade out; a set that is held lasts until closed.</summary>
        public float PlayLength => Hold > 0f ? AllInAt + Hold + FadeOut : AllInAt;

        /// <summary>Opacity of a picture <paramref name="seconds"/> into one play (0..1).</summary>
        public float Alpha(int index, float seconds)
        {
            if (float.IsNaN(seconds) || seconds < 0f) seconds = 0f;
            var inFade = FadeIn > 0f ? Clamp01((seconds - StartOf(index)) / FadeIn) : (seconds >= StartOf(index) ? 1f : 0f);
            if (Hold <= 0f) return inFade;
            var outStart = AllInAt + Hold;
            if (seconds <= outStart) return inFade;
            var outFade = FadeOut > 0f ? Clamp01((seconds - outStart) / FadeOut) : 1f;
            return inFade * (1f - outFade);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }

    public static class OverlaySets
    {
        /// <summary>Pause between two plays of a timed set while the view is open.</summary>
        public const float ReplayPause = 1f;

        /// <summary>Width of a strip as a fraction of the screen height (438 x 1080).</summary>
        public const float StripAspect = 438f / 1080f;

        public static readonly OverlaySet CombatA = new OverlaySet("combat1", "combat event 1", OverlayLayout.FullScreen, 0.1f, 0.1f, 1.5f, 0.5f,
            "pic_combat_event_001", "pic_combat_event_002", "pic_combat_event_003");

        public static readonly OverlaySet CombatB = new OverlaySet("combat2", "combat event 2", OverlayLayout.FullScreen, 0.1f, 0.1f, 1.5f, 0.5f,
            "pic_combat_event_004", "pic_combat_event_002", "pic_combat_event_003");

        public static readonly OverlaySet CutIn = new OverlaySet("cutin", "cut in", OverlayLayout.Strips, 0f, 0.1f, 0f, 0f,
            "pic_cut_in_001", "pic_cut_in_002", "pic_cut_in_003", "pic_cut_in_004");

        public static readonly IReadOnlyList<OverlaySet> All = new[] { CombatA, CombatB, CutIn };

        /// <summary>The set whose card is this key, or null.</summary>
        public static OverlaySet ForCardKey(string catalogKey)
        {
            if (catalogKey == null) return null;
            foreach (var set in All)
                if (string.Equals(set.CardKey, catalogKey, StringComparison.Ordinal)) return set;
            return null;
        }

        /// <summary>True for a picture that belongs to a set but is not any set's card (the list leaves it out).</summary>
        public static bool IsHiddenFrame(string catalogKey)
        {
            if (catalogKey == null || ForCardKey(catalogKey) != null) return false;
            foreach (var set in All)
                foreach (var k in set.Keys)
                    if (string.Equals(k, catalogKey, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>Horizontal centre of strip <paramref name="index"/> of <paramref name="count"/>, in screen heights from the centre.</summary>
        public static float StripCenter(int index, int count)
            => count <= 0 ? 0f : (index - (count - 1) * 0.5f) * StripAspect;
    }
}
