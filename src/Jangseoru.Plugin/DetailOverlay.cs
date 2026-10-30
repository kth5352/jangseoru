using System;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Jangseoru.Plugin
{
    /// <summary>Placement helpers shared by the full-screen detail overlays (맹세, 사건). Only our own objects are changed.</summary>
    internal static class DetailOverlay
    {
        /// <summary>Opaque white full-screen backdrop: nothing of the library shows through (user feedback, readability).</summary>
        public static readonly Color BackdropColor = new Color(1f, 1f, 1f, 1f);

        /// <summary>Dark brown ink for every text on the white backdrop or on the book/paper pages (as in Probe2's detail).</summary>
        public static readonly Color InkColor = new Color(0.2f, 0.12f, 0.06f, 1f);

        /// <summary>
        /// Removes Shadow/Outline effects from our own text clone: the card label's light-on-dark styling would blur dark ink
        /// on white. Only components of our clone are touched.
        /// </summary>
        public static void StripTextEffects(GameObject textClone)
        {
            foreach (var effect in textClone.GetComponents<Shadow>()) // Outline derives from Shadow
                if (effect != null) Object.DestroyImmediate(effect);
        }

        /// <summary>
        /// A clone of a card text under <paramref name="parent"/> (keeps the game font and its LeanLocalizedTextFont, loses
        /// LeanLocalizedText, size fitters and effects). Only the clone is changed.
        /// </summary>
        public static Text CloneText(GameAccess access, GameObject textSource, Transform parent, string name, int size, TextAnchor alignment,
            Color color, bool bestFit)
        {
            var go = Object.Instantiate(textSource, parent, false);
            go.name = name;
            foreach (var c in go.GetComponents<Component>())
            {
                if (c == null) continue;
                if (access.IsLeanLocalizedText(c) || c is ContentSizeFitter || c is LayoutElement || c is AspectRatioFitter)
                    Object.DestroyImmediate(c);
            }
            StripTextEffects(go);
            var text = go.GetComponent<Text>();
            if (text == null) throw new InvalidOperationException("text template has no Text");
            text.text = "";
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = bestFit;
            text.resizeTextMinSize = 12;
            text.resizeTextMaxSize = size;
            text.fontSize = size;
            text.color = color;
            text.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            go.SetActive(true);
            return text;
        }

        /// <summary>Covers the whole screen: centre and size of the root canvas, whatever rect our panel has.</summary>
        public static void FitToScreen(RectTransform overlayRt, Transform host)
        {
            var parentCanvas = host.GetComponentInParent<Canvas>();
            var top = parentCanvas != null ? parentCanvas.rootCanvas : null;
            if (top == null || !(top.transform is RectTransform crt)) throw new InvalidOperationException("no root canvas");
            overlayRt.position = crt.TransformPoint(crt.rect.center);
            var parentScale = host.lossyScale.x;
            var s = Math.Abs(parentScale) > 1e-6f ? crt.lossyScale.x / parentScale : 1f;
            overlayRt.sizeDelta = crt.rect.size * s;
        }

        /// <summary>One above every canvas of the library and its ancestors, on the root canvas's sorting layer.</summary>
        public static void SortAboveLibrary(Canvas canvas, Component library, Transform host)
        {
            var highest = 0;
            foreach (var c in library.GetComponentsInParent<Canvas>(true))
                if (c != null) highest = Math.Max(highest, c.sortingOrder);
            foreach (var c in library.GetComponentsInChildren<Canvas>(true))
                if (c != null && c != canvas) highest = Math.Max(highest, c.sortingOrder);

            var root = host.GetComponentInParent<Canvas>()?.rootCanvas;
            canvas.overrideSorting = true;
            if (root != null) canvas.sortingLayerID = root.sortingLayerID;
            canvas.sortingOrder = Math.Min(highest + 1, short.MaxValue);
        }

        public static void Stretch(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }
    }
}
