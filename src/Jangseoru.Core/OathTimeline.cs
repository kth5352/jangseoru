using System;
using System.Collections.Generic;

namespace Jangseoru.Core
{
    public enum OathPhase
    {
        BackdropIn,  // the white background fades in
        ContentIn,   // picture and description fade in together
        Wait,        // everything visible; a click / Submit / Esc closes
        ContentOut,  // picture and description fade out
        BackdropOut, // the white background fades out
        Done
    }

    /// <summary>
    /// Timeline of the 맹세 (EndCg) detail presentation: the white background fades in over 1s, then the picture and
    /// the description fade in together over 1s, then it waits for a close; closing runs the same steps in reverse
    /// (content out 1s, background out 1s). Pure: the plugin feeds it frame times and reads alphas.
    /// Close requests are honoured only in <see cref="OathPhase.Wait"/>.
    /// </summary>
    public sealed class OathTimeline
    {
        public const float StepSeconds = 1f;

        /// <summary>Upper bound on description lines (one Text object each).</summary>
        public const int MaxLines = 64;

        private readonly int lines;
        private float elapsed; // time spent in the current phase

        public OathTimeline(int lineCount)
        {
            lines = Math.Max(0, lineCount);
        }

        public OathPhase Phase { get; private set; } = OathPhase.BackdropIn;

        public bool AcceptsClose => Phase == OathPhase.Wait;

        public bool Finished => Phase == OathPhase.Done;

        public float BackdropAlpha
        {
            get
            {
                switch (Phase)
                {
                    case OathPhase.BackdropIn: return Clamp01(elapsed / StepSeconds);
                    case OathPhase.BackdropOut: return 1f - Clamp01(elapsed / StepSeconds);
                    case OathPhase.Done: return 0f;
                    default: return 1f;
                }
            }
        }

        /// <summary>Alpha of the picture and the description.</summary>
        public float ContentAlpha
        {
            get
            {
                switch (Phase)
                {
                    case OathPhase.ContentIn: return Clamp01(elapsed / StepSeconds);
                    case OathPhase.Wait: return 1f;
                    case OathPhase.ContentOut: return 1f - Clamp01(elapsed / StepSeconds);
                    default: return 0f;
                }
            }
        }

        /// <summary>Alpha of description line <paramref name="index"/>: all lines fade with the picture.</summary>
        public float LineAlpha(int index) => index < 0 || index >= lines ? 0f : ContentAlpha;

        /// <summary>Moves time forward. Negative, NaN or infinite steps are ignored.</summary>
        public void Advance(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f) return;
            elapsed += seconds;
            while (true)
            {
                if (Phase == OathPhase.Wait || Phase == OathPhase.Done) { elapsed = 0f; return; }
                if (elapsed < StepSeconds) return;
                elapsed -= StepSeconds;
                Phase = Next(Phase);
            }
        }

        /// <summary>A click / Submit / Esc. True if it started the fade out.</summary>
        public bool RequestClose()
        {
            if (Phase != OathPhase.Wait) return false;
            Phase = OathPhase.ContentOut;
            elapsed = 0f;
            return true;
        }

        /// <summary>
        /// Splits the game's description into lines the way CgPanel does (on '\n'), tolerating "\r\n". Leading and trailing
        /// blank lines are dropped; inner blank lines become " " so they keep their height in a Unity Text.
        /// At most <see cref="MaxLines"/> lines.
        /// </summary>
        public static IReadOnlyList<string> SplitLines(string text)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(text)) return result;

            var raw = text.Replace("\r\n", "\n").Split('\n');
            int first = 0, last = raw.Length - 1;
            while (first <= last && IsBlank(raw[first])) first++;
            while (last >= first && IsBlank(raw[last])) last--;

            for (var i = first; i <= last && result.Count < MaxLines; i++)
            {
                var line = raw[i].TrimEnd('\r');
                result.Add(IsBlank(line) ? " " : line);
            }
            return result;
        }

        private static bool IsBlank(string s) => string.IsNullOrEmpty(s) || s.Trim().Length == 0;

        private static OathPhase Next(OathPhase phase)
        {
            switch (phase)
            {
                case OathPhase.BackdropIn: return OathPhase.ContentIn;
                case OathPhase.ContentIn: return OathPhase.Wait;
                case OathPhase.ContentOut: return OathPhase.BackdropOut;
                case OathPhase.BackdropOut: return OathPhase.Done;
                default: return phase;
            }
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
