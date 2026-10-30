using System;
using System.Collections.Generic;

namespace Jangseoru.Core
{
    /// <summary>One bubble of an event-bubble presentation: the story effect that shows it, its catalog key and where it sits.</summary>
    public sealed class BubbleFrame
    {
        /// <summary>Story effect name passed to StoryEffectsController.SetupEffect (e.g. "EventBubble_100").</summary>
        public string Effect { get; }
        /// <summary>Catalog key of its picture (e.g. "pic_event_bubble_100").</summary>
        public string CatalogKey { get; }
        /// <summary>World position in the story camera's units (x right, y up), as the story script gives it.</summary>
        public float X { get; }
        public float Y { get; }

        internal BubbleFrame(string effect, string catalogKey, float x, float y)
        {
            Effect = effect;
            CatalogKey = catalogKey;
            X = x;
            Y = y;
        }
    }

    /// <summary>A group of bubbles the story shows one after another on one scene.</summary>
    public sealed class BubbleSet
    {
        public string Id { get; }
        /// <summary>Card caption in the 사건 list.</summary>
        public string Label { get; }
        public IReadOnlyList<BubbleFrame> Frames { get; }
        /// <summary>Seconds between one bubble and the next in the replay.</summary>
        public float Interval { get; }
        /// <summary>The frames' pictures can be loaded with Addressables (catalog pictures); otherwise they ship with the mod.</summary>
        public bool UsesAddressables { get; }

        internal BubbleSet(string id, string label, float interval, bool usesAddressables, params BubbleFrame[] frames)
        {
            Id = id;
            Label = label;
            Interval = interval;
            UsesAddressables = usesAddressables;
            Frames = frames;
        }

        /// <summary>The set counts as seen once its last bubble appeared in a story.</summary>
        public string UnlockKey => Frames[Frames.Count - 1].CatalogKey;

        /// <summary>The card's representative picture.</summary>
        public string CardKey => Frames[0].CatalogKey;
    }

    /// <summary>
    /// The two event-bubble presentations of the game and the maths of their look. The story shows each bubble with a looping
    /// particle effect: one 6-unit-wide billboard that sways ±3° and breathes between 95% and 100% size every 2 seconds, in a
    /// camera 10.8 units tall (orthographic size 5.4). All of that is read from the game's particle prefabs.
    /// </summary>
    public static class BubbleSets
    {
        /// <summary>Camera height in world units (orthographic size 5.4 x 2).</summary>
        public const float WorldHeight = 10.8f;
        /// <summary>Particle size in world units.</summary>
        public const float BubbleSize = 6f;
        /// <summary>Seconds of one sway/breath cycle (the particle's lifetime; it respawns in the same phase).</summary>
        public const float CycleSeconds = 2f;
        public const float SwayDegrees = 3f;

        /// <summary>First set: four bubbles (scene S0208_03), pictures embedded in the Story scene.</summary>
        public static readonly BubbleSet Four = new BubbleSet("bubble4", "event bubble 1", 2f, false,
            new BubbleFrame("EventBubble_001", "pic_event_bubble_1", -5f, -2f),
            new BubbleFrame("EventBubble_002", "pic_event_bubble_2", -3f, 3f),
            new BubbleFrame("EventBubble_003", "pic_event_bubble_3", 3f, 3f),
            new BubbleFrame("EventBubble_004", "pic_event_bubble_4", 5f, -2f));

        /// <summary>Second set: six bubbles in a 3x2 grid (scene ch4_5_1), pictures are catalog pictures.</summary>
        public static readonly BubbleSet Six = new BubbleSet("bubble6", "event bubble 2", 2f, true,
            new BubbleFrame("EventBubble_100", "pic_event_bubble_100", -6f, 2.5f),
            new BubbleFrame("EventBubble_101", "pic_event_bubble_101", 0f, 2.5f),
            new BubbleFrame("EventBubble_102", "pic_event_bubble_102", 6f, 2.5f),
            new BubbleFrame("EventBubble_103", "pic_event_bubble_103", -6f, -2.5f),
            new BubbleFrame("EventBubble_104", "pic_event_bubble_104", 0f, -2.5f),
            new BubbleFrame("EventBubble_105", "pic_event_bubble_105", 6f, -2.5f));

        public static readonly IReadOnlyList<BubbleSet> All = new[] { Four, Six };

        /// <summary>The set that owns a catalog key (any of its frames), or null.</summary>
        public static BubbleSet ForCatalogKey(string catalogKey)
        {
            if (catalogKey == null) return null;
            foreach (var set in All)
                foreach (var f in set.Frames)
                    if (string.Equals(f.CatalogKey, catalogKey, StringComparison.Ordinal)) return set;
            return null;
        }

        /// <summary>The catalog key a story effect stands for, when the effect is one of the bubbles and was started (play = 1).</summary>
        public static bool TryFromEffect(string effectName, int play, out string catalogKey)
        {
            catalogKey = null;
            if (play != 1 || string.IsNullOrEmpty(effectName)) return false;
            foreach (var set in All)
                foreach (var f in set.Frames)
                    if (string.Equals(f.Effect, effectName, StringComparison.Ordinal))
                    {
                        catalogKey = f.CatalogKey;
                        return true;
                    }
            return false;
        }

        /// <summary>
        /// True when the key is a frame of a set that is shown as one card but is not the card's representative (the list
        /// leaves these out).
        /// </summary>
        public static bool IsHiddenFrame(string catalogKey)
        {
            var set = ForCatalogKey(catalogKey);
            return set != null && !string.Equals(set.CardKey, catalogKey, StringComparison.Ordinal);
        }

        /// <summary>Rotation in degrees (counter-clockwise) and scale of a bubble <paramref name="seconds"/> after it appeared.</summary>
        public static void Sway(float seconds, out float degrees, out float scale)
        {
            degrees = 0f;
            scale = 1f;
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f) seconds = 0f;
            var u = (float)((seconds % CycleSeconds) / CycleSeconds);

            // Rotation curve: -1 at 0, +1 at 0.5, -1 at 1, flat tangents (game's curve, scaled to ±3 degrees).
            degrees = SwayDegrees * Hermite(u, -1f, 0f, 1f, 0f, 1f, 0f, -1f, 0f);
            // Size curve: 0.95 (slope 0.1) -> 1.0 (slope 0) -> 0.95 (slope -0.1).
            scale = Hermite(u, 0.95f, 0.1f, 1f, 0f, 1f, 0f, 0.95f, -0.1f);
        }

        /// <summary>Pixels per world unit for a screen of the given height.</summary>
        public static float PixelsPerUnit(float screenHeight)
            => screenHeight > 0f && !float.IsInfinity(screenHeight) && !float.IsNaN(screenHeight) ? screenHeight / WorldHeight : 0f;

        // Two cubic Hermite segments over u in [0, 1]: (0 -> 0.5) then (0.5 -> 1), with key values and slopes per key.
        private static float Hermite(float u, float v0, float s0, float v1, float s1, float v1b, float s1b, float v2, float s2)
        {
            if (u < 0f) u = 0f;
            if (u > 1f) u = 1f;
            return u <= 0.5f
                ? Segment(u / 0.5f, v0, s0 * 0.5f, v1, s1 * 0.5f)
                : Segment((u - 0.5f) / 0.5f, v1b, s1b * 0.5f, v2, s2 * 0.5f);
        }

        private static float Segment(float t, float p0, float m0, float p1, float m1)
        {
            var t2 = t * t;
            var t3 = t2 * t;
            return (2f * t3 - 3f * t2 + 1f) * p0 + (t3 - 2f * t2 + t) * m0 + (-2f * t3 + 3f * t2) * p1 + (t3 - t2) * m1;
        }
    }
}
