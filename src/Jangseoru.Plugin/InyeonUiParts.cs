using System;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Jangseoru.Plugin
{
    /// <summary>Own objects only; the game's font and sprites remain borrowed references.</summary>
    internal sealed class InyeonUiParts
    {
        private readonly GameAccess access;
        private readonly GameObject textTemplate, headingTemplate;
        public static readonly Color Ink = new Color(.94f, .9f, .79f, 1f);
        public static readonly Color Gold = new Color(.80f, .64f, .38f, 1f);
        public static readonly Color Muted = new Color(.64f, .60f, .50f, 1f);
        public static readonly Color Panel = new Color(.03f, .03f, .03f, .98f); // near black
        public static readonly Color Edge = new Color(.48f - 30f / 255f, .39f - 30f / 255f, .25f - 30f / 255f, .85f); // 30/255 darker per channel than before
        /// <summary><paramref name="headingTemplate"/> is the library title text (the game's brush-style heading font); optional.</summary>
        public InyeonUiParts(GameAccess access, GameObject textTemplate, GameObject headingTemplate = null)
        { this.access = access; this.textTemplate = textTemplate; this.headingTemplate = headingTemplate; }

        public static RectTransform Rect(string name, Transform parent, float x0, float y0, float x1, float y1)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rt.SetParent(parent, false);
            Place(rt, x0, y0, x1, y1);
            return rt;
        }
        public static void Place(RectTransform rt, float x0, float y0, float x1, float y1)
        {
            rt.anchorMin = new Vector2(x0, y0);
            rt.anchorMax = new Vector2(x1, y1);
            rt.pivot = new Vector2(.5f, .5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }
        public static Image Image(string name, Transform parent, float x0, float y0, float x1, float y1, Color color)
        {
            var image = Rect(name, parent, x0, y0, x1, y1).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
        public static void Rule(Transform parent, float left, float right, float y, Color color)
        {
            var line = Image("Rule", parent, left, y, right, y, color);
            line.rectTransform.sizeDelta = new Vector2(0, 1f);
        }
        public static RectTransform Frame(string name, Transform parent, float x0, float y0, float x1, float y1, Color background)
        {
            var frame = Image(name, parent, x0, y0, x1, y1, background).rectTransform;
            Border(frame, Edge, 1f);
            // Restrained right-angle corners echo the game's wood-and-brass frames.
            foreach (var corner in new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one })
            {
                var horizontal = Image("CornerH", frame, corner.x, corner.y, corner.x, corner.y, Gold).rectTransform;
                horizontal.pivot = corner;
                horizontal.sizeDelta = new Vector2(14f, 2f);
                var vertical = Image("CornerV", frame, corner.x, corner.y, corner.x, corner.y, Gold).rectTransform;
                vertical.pivot = corner;
                vertical.sizeDelta = new Vector2(2f, 14f);
            }
            return frame;
        }
        /// <summary>
        /// A panel drawn with the game's small ornate frame (9-sliced, shipped as <see cref="ItemArt.ListFrame"/>), the same frame the item
        /// tabs' list uses. Falls back to the plain bordered <see cref="Frame"/> when the picture cannot be decoded.
        /// </summary>
        public static RectTransform ArtFrame(string name, Transform parent, float x0, float y0, float x1, float y1, Color fallback)
        {
            var sprite = ItemArt.Get(ItemArt.ListFrame);
            if (sprite == null) return Frame(name, parent, x0, y0, x1, y1, fallback);
            var image = Image(name, parent, x0, y0, x1, y1, Color.white);
            image.sprite = sprite;
            image.type = UnityEngine.UI.Image.Type.Sliced;
            return image.rectTransform;
        }
        public static void Border(Transform parent, Color color, float thickness = 1f)
        {
            foreach (var x in new[] { 0f, 1f })
                Image("EdgeV", parent, x, 0, x, 1, color).rectTransform.sizeDelta = new Vector2(thickness, 0);
            foreach (var y in new[] { 0f, 1f })
                Image("EdgeH", parent, 0, y, 1, y, color).rectTransform.sizeDelta = new Vector2(0, thickness);
        }
        public Text Text(string name, Transform parent, float x0, float y0, float x1, float y1,
            string value, int size = 24, TextAnchor alignment = TextAnchor.MiddleLeft, bool heading = false)
        {
            var go = Object.Instantiate(heading && headingTemplate != null ? headingTemplate : textTemplate, parent, false);
            go.name = name;
            foreach (var c in go.GetComponents<Component>())
                if (c != null && (access.IsLeanLocalizedText(c) || c is ContentSizeFitter || c is LayoutElement || c is AspectRatioFitter)) Object.DestroyImmediate(c);
            DetailOverlay.StripTextEffects(go); // the card label's Shadow/Outline glow blurs light text; only our clone is changed
            var text = go.GetComponent<Text>();
            Place((RectTransform)go.transform, x0, y0, x1, y1);
            text.text = value;
            text.fontSize = size;
            text.resizeTextForBestFit = false;
            text.color = Ink;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            // The game font's line is taller than its size; Truncate would drop a one-line label whose rect is a little low.
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            go.SetActive(true);
            return text;
        }
        public Button Button(string name, Transform parent, float x0, float y0, float x1, float y1,
            string label, Action action, int size = 28)
        {
            var image = Image(name, parent, x0, y0, x1, y1, new Color(.08f, .075f, .07f, 1f));
            Border(image.transform, Edge);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.4f, 1.3f, 1.15f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            button.onClick.AddListener(() => action());
            Text("Label", image.transform, 0, 0, 1, 1, label, size, TextAnchor.MiddleCenter);
            return button;
        }
        public Button TextButton(string name, Transform parent, float x0, float y0, float x1, float y1,
            string label, Action action, int size = 28, bool heading = false)
        {
            // Match the working category tabs: a full rectangular hit target independent of the cloned font mesh.
            var hit = Image(name, parent, x0, y0, x1, y1, Color.clear);
            hit.raycastTarget = true;
            var button = hit.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => action());
            Text("Label", hit.transform, 0, 0, 1, 1, label, size, TextAnchor.MiddleCenter, heading);
            return button;
        }
        public static void ButtonState(Button button, bool enabled, bool selected = false)
        {
            button.interactable = enabled;
            var label = button.GetComponentInChildren<Text>();
            if (label != null) label.color = !enabled ? Muted * new Color(1, 1, 1, .45f) : selected ? Gold : Ink;
            var mark = button.transform.Find("Selected");
            if (mark != null) mark.gameObject.SetActive(selected);
        }
        public static ScrollRect Scroll(string name, Transform parent, float x0, float y0, float x1, float y1,
            out RectTransform content)
        {
            var root = Rect(name, parent, x0, y0, x1, y1);
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            var viewport = Rect("Viewport", root, 0, 0, .96f, 1);
            viewport.gameObject.AddComponent<RectMask2D>();
            var hit = viewport.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            content = Rect("Content", viewport, 0, 1, 1, 1);
            content.pivot = new Vector2(.5f, 1);
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 36;
            var track = Image("Scrollbar", root, .985f, 0, 1, 1, new Color(.04f, .035f, .025f, .9f));
            track.raycastTarget = true;
            var sliding = Rect("Sliding Area", track.transform, 0, 0, 1, 1);
            var handle = Image("Handle", sliding, 0, 0, 1, 1, Gold);
            handle.raycastTarget = true;
            var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.handleRect = handle.rectTransform;
            bar.targetGraphic = handle;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return scroll;
        }

        /// <summary>A horizontally scrolling row: content is left-anchored and as wide as its cells; the wheel and a bar below scroll it.</summary>
        public static ScrollRect HorizontalScroll(string name, Transform parent, float x0, float y0, float x1, float y1,
            out RectTransform content)
        {
            var root = Rect(name, parent, x0, y0, x1, y1);
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            var viewport = Rect("Viewport", root, 0, .09f, 1, 1);
            viewport.gameObject.AddComponent<RectMask2D>();
            var hit = viewport.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            content = Rect("Content", viewport, 0, 0, 0, 1);
            content.pivot = new Vector2(0, .5f);
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = -40; // a sideways row: wheel down moves toward the end, like a page turn
            var track = Image("Scrollbar", root, 0, 0, 1, .035f, new Color(.04f, .035f, .025f, .9f));
            track.raycastTarget = true;
            var sliding = Rect("Sliding Area", track.transform, 0, 0, 1, 1);
            var handle = Image("Handle", sliding, 0, 0, 1, 1, Gold);
            handle.raycastTarget = true;
            var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.LeftToRight;
            bar.handleRect = handle.rectTransform;
            bar.targetGraphic = handle;
            scroll.horizontalScrollbar = bar;
            scroll.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return scroll;
        }
    }
}
