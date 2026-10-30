using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Jangseoru.Core
{
    /// <summary>A rect in normalized anchor space of its frame: (0,0) bottom-left, (1,1) top-right, like Unity anchors.</summary>
    public struct NormRect
    {
        public float XMin, YMin, XMax, YMax;

        public NormRect(float xMin, float yMin, float xMax, float yMax)
        {
            XMin = xMin;
            YMin = yMin;
            XMax = xMax;
            YMax = yMax;
        }
    }

    /// <summary>Design size of a detail frame and where its parts go, as anchors inside the frame.</summary>
    public sealed class DetailGeometry
    {
        public float Width { get; }
        public float Height { get; }
        /// <summary>Left page: the event illustration.</summary>
        public NormRect Picture { get; }
        /// <summary>Right page: number above the group name.</summary>
        public NormRect Caption { get; }
        /// <summary>The book's printed name plate (cartouche); only meaningful when <see cref="HasPlate"/>.</summary>
        public NormRect Plate { get; }
        public bool HasPlate { get; }

        internal DetailGeometry(float width, float height, NormRect picture, NormRect caption, NormRect? plate)
        {
            Width = width;
            Height = height;
            Picture = picture;
            Caption = caption;
            HasPlate = plate.HasValue;
            Plate = plate ?? default(NormRect);
        }
    }

    /// <summary>
    /// Pure layout and text decisions of the 사건 (event illustration) detail, kept out of the Unity code so they can be tested.
    /// Two frames with the same parts: the game's open book sprite <c>pic_book_1</c> (1254x1080; page areas from
    /// docs/infomation/11: left x 90..610, right x 630..1130, name plate x 863..1097 / y 450..930), or two
    /// <c>pic_paper_002</c> sheets side by side (the verified Probe2 fallback, 620x860 each).
    /// </summary>
    public static class PictureDetailLayout
    {
        public const string LoadFailedText = "이미지를 불러오지 못했습니다";
        public const string PlateText = "사건";

        public const float BookWidth = 1254f;
        public const float BookHeight = 1080f;
        public const float PaperWidth = 620f;
        public const float PaperHeight = 860f;

        /// <summary>Share of the screen the frame may take (both directions).</summary>
        public const float ScreenShare = 0.92f;

        public static readonly DetailGeometry Book = new DetailGeometry(BookWidth, BookHeight,
            Normalize(BookWidth, BookHeight, 110f, 150f, 590f, 930f),
            Normalize(BookWidth, BookHeight, 650f, 150f, 1110f, 430f),
            Normalize(BookWidth, BookHeight, 863f, 450f, 1097f, 930f));

        public static readonly DetailGeometry Papers = new DetailGeometry(2 * PaperWidth, PaperHeight,
            Normalize(2 * PaperWidth, PaperHeight, 75f, 80f, 545f, 780f),
            Normalize(2 * PaperWidth, PaperHeight, 695f, 120f, 1165f, 740f),
            null);

        /// <summary>
        /// A pixel rect given from the frame's top-left corner (image coordinates) as anchors inside the frame. Edges are
        /// ordered and clamped to the frame; an unusable frame size gives the whole frame.
        /// </summary>
        public static NormRect Normalize(float frameW, float frameH, float left, float top, float right, float bottom)
        {
            if (!Usable(frameW) || !Usable(frameH)) return new NormRect(0f, 0f, 1f, 1f);
            var x0 = Clamp01(Math.Min(left, right) / frameW);
            var x1 = Clamp01(Math.Max(left, right) / frameW);
            var yTop = Clamp01(1f - Math.Min(top, bottom) / frameH);
            var yBottom = Clamp01(1f - Math.Max(top, bottom) / frameH);
            return new NormRect(x0, yBottom, x1, yTop);
        }

        /// <summary>
        /// Largest size with the source's aspect ratio inside the box (scales up as well as down). An unusable source fills
        /// the box; an unusable box gives zero.
        /// </summary>
        public static void Fit(float srcW, float srcH, float boxW, float boxH, out float w, out float h)
        {
            if (!Usable(boxW) || !Usable(boxH)) { w = h = 0f; return; }
            if (!Usable(srcW) || !Usable(srcH)) { w = boxW; h = boxH; return; }
            var scale = Math.Min(boxW / srcW, boxH / srcH);
            w = srcW * scale;
            h = srcH * scale;
        }

        /// <summary>
        /// Caption of an event card in the detail: its 1-based number within its group (empty for single-card groups) and the
        /// group name with underscores as spaces. Same numbering as <see cref="GalleryLayout.EventLabels"/>.
        /// </summary>
        public static bool TryCaption(IReadOnlyList<string> catalogKeys, string catalogKey, out string number, out string group)
        {
            number = string.Empty;
            group = string.Empty;
            if (catalogKeys == null || string.IsNullOrEmpty(catalogKey)) return false;

            var g = GroupName(catalogKey);
            int ordinal = 0, total = 0;
            var found = false;
            foreach (var k in catalogKeys)
            {
                if (GroupName(k) != g) continue;
                total++;
                if (!found)
                {
                    ordinal++;
                    if (string.Equals(k, catalogKey, StringComparison.Ordinal)) found = true;
                }
            }
            if (!found) return false;

            group = g;
            number = total > 1 ? ordinal.ToString(CultureInfo.InvariantCulture) : string.Empty;
            return true;
        }

        /// <summary>One character per line, for the book's vertical name plate.</summary>
        public static string Vertical(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var sb = new StringBuilder(text.Length * 2);
            for (var i = 0; i < text.Length; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(text[i]);
            }
            return sb.ToString();
        }

        private static string GroupName(string catalogKey) => PictureCatalog.Group(catalogKey).Replace('_', ' ').Trim();

        private static bool Usable(float v) => v > 0f && !float.IsInfinity(v) && !float.IsNaN(v);

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
