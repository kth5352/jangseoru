using System;
using System.Collections.Generic;
using Jangseoru.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Jangseoru.Plugin
{
    /// <summary>
    /// The 잡물 / 서적 / 보물 screen, built after the game's bag tab (status window, 行囊): the item picture in the ornate diamond frame
    /// with the seal naming its kind, the vertical name over a brush stroke, the scrolling description, and on the right the framed
    /// list whose rows brighten (with a warm glow) when hovered or chosen. Positions, sizes, colours and fonts follow the game's
    /// own objects (measured from its Story scene); the frame, seal and brush pictures are copies shipped in the plugin
    /// (<see cref="ItemArt"/>). Built under our own panel root over the area of the card grid (hidden meanwhile). Only our own
    /// objects are created, changed and destroyed; item pictures are the game's sprites, referenced and never modified or
    /// destroyed. Every entry point is wrapped; a failure closes the view and switches it off for the session.
    /// </summary>
    internal sealed class ItemView
    {
        // The game's design space: the left column x 10..480 and the list x 502..852 of its 1000 x 600 detail area.
        private const float DesignWidth = 870f, DesignHeight = 600f;

        private static readonly Color RowNormal = Hex(0xd8d5d5);
        private static readonly Color RowLocked = new Color(0.62f, 0.60f, 0.60f, 0.55f);
        private static readonly Color RowLit = Hex(0xfeefce);
        private static readonly Color RowGlow = new Color(1f, 0.60f, 0.43f, 0.78f);
        private static readonly Color TitleGold = Hex(0xd9b077);
        private static readonly Color SealRed = Hex(0x952424);

        private readonly GameAccess access;
        private readonly ILog log;
        private readonly Transform host;
        private readonly RectTransform areaSource;
        private readonly GameObject textSource;

        private GameObject root;
        private RectTransform rootRt, designRt, listContent, descContent;
        private ScrollRect listScroll, descScroll;
        private Image picture;
        private Text title, desc, seal;
        private GameObject detailGroup;

        private IReadOnlyList<ItemRow> rows;
        private IReadOnlyDictionary<string, ItemEntry> entries;
        private Func<string, string> localize;
        private readonly List<RowView> rowViews = new List<RowView>();
        private int selected = -1, hovered = -1;

        private sealed class RowView
        {
            public Text Label;
            public Shadow Glow;
            public bool Unlocked;
        }

        /// <summary>Pointer enter/exit/click only: no scroll or drag handlers, so the mouse wheel still reaches the list's ScrollRect.</summary>
        private sealed class RowInput : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
        {
            public Action OnEnter, OnExit, OnClick;
            public void OnPointerEnter(PointerEventData e) => Run(OnEnter);
            public void OnPointerExit(PointerEventData e) => Run(OnExit);
            public void OnPointerClick(PointerEventData e) => Run(OnClick);
            private static void Run(Action a) { try { a?.Invoke(); } catch (Exception) { } }
        }

        public ItemView(GameAccess access, ILog log, Transform host, RectTransform areaSource, GameObject textSource)
        {
            this.access = access;
            this.log = log;
            this.host = host;
            this.areaSource = areaSource;
            this.textSource = textSource;
        }

        public bool IsOpen => root != null && root.activeSelf;

        public bool Broken { get; private set; }

        private static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);

        /// <summary>
        /// Shows the rows of one tab. <paramref name="entries"/> maps an item id to what the left side needs;
        /// <paramref name="localize"/> turns a game text key into the current language (null when it cannot).
        /// </summary>
        public void Open(IReadOnlyList<ItemRow> itemRows, IReadOnlyDictionary<string, ItemEntry> itemEntries, Func<string, string> localizer)
        {
            try
            {
                if (Broken) return;
                EnsureBuilt();
                rows = itemRows ?? new ItemRow[0];
                entries = itemEntries;
                localize = localizer;

                CopyArea();
                FitDesign();
                root.SetActive(true);
                BuildRows();
                listContent.anchoredPosition = new Vector2(listContent.anchoredPosition.x, 0f);
                listScroll.StopMovement();

                var first = -1;
                for (var i = 0; i < rows.Count; i++)
                    if (rows[i].Unlocked) { first = i; break; }
                Select(first);
                ScrollToRow(first);
            }
            catch (Exception e) { Fail("open", e); }
        }

        /// <summary>Hides the view and drops its references. Idempotent; never throws.</summary>
        public void Close()
        {
            try
            {
                if (picture != null) picture.sprite = null; // the game's sprite is only referenced
                if (root != null && root.activeSelf) root.SetActive(false);
                rows = null;
                entries = null;
                localize = null;
                selected = hovered = -1;
            }
            catch (Exception) { }
        }

        private void Fail(string where, Exception e)
        {
            Broken = true;
            try { log.Warn("item view disabled: " + where + " threw " + e.GetType().Name + ": " + e.Message); } catch { }
            Close();
        }

        // ================= selection =================

        private void Select(int index)
        {
            selected = index;
            RefreshRows();

            var row = index >= 0 && rows != null && index < rows.Count ? rows[index] : null;
            ItemEntry entry = null;
            if (row != null && row.Unlocked && entries != null) entries.TryGetValue(row.Id, out entry);

            if (entry == null)
            {
                // Locked or nothing chosen: nothing of the item is shown (no picture, title or description).
                picture.sprite = null;
                picture.enabled = false;
                title.text = "";
                desc.text = "";
                seal.text = "";
                detailGroup.SetActive(false);
                return;
            }

            detailGroup.SetActive(true);
            picture.sprite = entry.Image; // reference only
            picture.enabled = entry.Image != null;
            title.text = PictureDetailLayout.Vertical(Localize(entry.TitleKey) ?? Localize(entry.NameKey) ?? "");
            BrushFont.Apply(title); // the game's brush font for the name
            seal.text = SealText(entry.Kind);
            if (!BrushFont.Use(seal, "Bakudai-Medium")) BrushFont.Apply(seal, true);
            desc.text = Localize(entry.DescKey) ?? "";
            descContent.anchoredPosition = new Vector2(descContent.anchoredPosition.x, 0f);
            descScroll.StopMovement();
        }

        /// <summary>Brings a row into the middle of the list's view (rows are 35 high with 3 between them).</summary>
        private void ScrollToRow(int index)
        {
            if (index < 0 || rows == null) return;
            const float step = 38f, view = 440f;
            var total = rows.Count * step - 3f;
            var y = Mathf.Clamp(index * step - (view - 35f) / 2f, 0f, Mathf.Max(0f, total - view));
            listScroll.StopMovement();
            listContent.anchoredPosition = new Vector2(listContent.anchoredPosition.x, y);
        }

        /// <summary>The kind written on the seal, as the game writes it (書物 / 雜物 / 寶物).</summary>
        private static string SealText(ItemKind kind)
            => kind == ItemKind.Book ? "書物" : kind == ItemKind.Misc ? "雜物" : "寶物";

        /// <summary>A row is lit while hovered or chosen, like the game's list toggles.</summary>
        private void RefreshRows()
        {
            for (var i = 0; i < rowViews.Count; i++)
            {
                var view = rowViews[i];
                if (view == null || view.Label == null) continue;
                var lit = view.Unlocked && (i == selected || i == hovered);
                view.Label.color = !view.Unlocked ? RowLocked : lit ? RowLit : RowNormal;
                if (view.Glow != null) view.Glow.enabled = lit;
            }
        }

        private string Localize(string key)
        {
            try { return string.IsNullOrEmpty(key) ? null : localize?.Invoke(key); }
            catch (Exception) { return null; }
        }

        // ================= build =================

        private void BuildRows()
        {
            for (var i = listContent.childCount - 1; i >= 0; i--)
            {
                var child = listContent.GetChild(i).gameObject;
                child.SetActive(false);
                Object.Destroy(child);
            }
            rowViews.Clear();
            hovered = -1;

            for (var i = 0; i < rows.Count; i++)
            {
                var index = i;
                var go = new GameObject("Row" + i, typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(RowInput));
                go.transform.SetParent(listContent, false);
                go.GetComponent<LayoutElement>().preferredHeight = 35f; // the game's row height

                var hit = go.GetComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f);
                hit.raycastTarget = true;
                var input = go.GetComponent<RowInput>();
                input.OnEnter = () => { hovered = index; RefreshRows(); };
                input.OnExit = () => { if (hovered == index) hovered = -1; RefreshRows(); };
                input.OnClick = () =>
                {
                    try { Select(index); }
                    catch (Exception e) { Fail("row click", e); }
                };

                // Best fit: a long name shrinks to the row instead of being cut at the row's edge.
                var text = DetailOverlay.CloneText(access, textSource, go.transform, "Name", 20, TextAnchor.MiddleLeft, RowNormal, true);
                text.resizeTextMinSize = 10;
                text.text = rows[i].Label;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                var rt = text.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(10f, 0f);
                rt.offsetMax = new Vector2(-10f, 0f);

                // The game's lit row: warm orange glow behind the light text.
                var glow = text.gameObject.AddComponent<Shadow>();
                glow.effectColor = RowGlow;
                glow.effectDistance = new Vector2(1f, -1f);
                glow.enabled = false;
                rowViews.Add(new RowView { Label = text, Glow = glow, Unlocked = rows[i].Unlocked });
            }
            RefreshRows();
        }

        private void CopyArea()
        {
            rootRt.anchorMin = areaSource.anchorMin;
            rootRt.anchorMax = areaSource.anchorMax;
            rootRt.pivot = areaSource.pivot;
            rootRt.sizeDelta = areaSource.sizeDelta;
            rootRt.anchoredPosition = areaSource.anchoredPosition;
        }

        /// <summary>Scales the game-sized layout to the area it is shown in (never beyond the game's own size).</summary>
        private void FitDesign()
        {
            var size = rootRt.rect.size;
            var scale = size.x > 1f && size.y > 1f ? Mathf.Min(1f, Mathf.Min(size.x / DesignWidth, size.y / DesignHeight)) : 1f;
            designRt.localScale = new Vector3(scale, scale, 1f);
            try { log.Info("item view: area " + Mathf.RoundToInt(size.x) + "x" + Mathf.RoundToInt(size.y) + ", layout scale " + scale.ToString("0.00")); } catch { }
        }

        private void EnsureBuilt()
        {
            if (root != null) return;
            if (host == null || textSource == null || areaSource == null) throw new InvalidOperationException("host, text template or area missing");

            root = new GameObject("ItemView", typeof(RectTransform));
            root.SetActive(false);
            rootRt = (RectTransform)root.transform;
            rootRt.SetParent(host, false);

            designRt = Box("Design", rootRt, 0f, 0f, DesignWidth, DesignHeight);
            designRt.anchorMin = designRt.anchorMax = designRt.pivot = new Vector2(0.5f, 0.5f);
            designRt.anchoredPosition = Vector2.zero;

            BuildList();
            BuildDetail();
        }

        /// <summary>The game's list: the small frame (9-sliced), a 300 x 440 scroll view, 5-wide scrollbar.</summary>
        private void BuildList()
        {
            var frame = Box("ListFrame", designRt, 502f, 45f, 350f, 515f);
            var frameImg = frame.gameObject.AddComponent<Image>();
            var frameSprite = ItemArt.Get(ItemArt.ListFrame);
            if (frameSprite != null) { frameImg.sprite = frameSprite; frameImg.type = Image.Type.Sliced; frameImg.color = Color.white; }
            else frameImg.color = new Color(0f, 0f, 0f, 0.33f);
            frameImg.raycastTarget = false;

            var viewport = Box("ListViewport", designRt, 535.6f, 81f, 300f, 440f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var wheel = viewport.gameObject.AddComponent<Image>(); // an invisible target so the wheel over the gaps reaches the ScrollRect
            wheel.color = new Color(0f, 0f, 0f, 0f);
            wheel.raycastTarget = true;
            listContent = NewRect("Content", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(-12f, 0f));
            listContent.pivot = new Vector2(0.5f, 1f);
            var vlg = listContent.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 3f; // the game's row spacing
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            listContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            listScroll = viewport.gameObject.AddComponent<ScrollRect>();
            ConfigureScroll(listScroll, listContent, viewport, 15f);
            AddScrollbar(listScroll, viewport);
        }

        /// <summary>The game's detail column: frame with picture and seal, vertical name over the brush, description.</summary>
        private void BuildDetail()
        {
            detailGroup = new GameObject("Detail", typeof(RectTransform));
            var detail = (RectTransform)detailGroup.transform;
            detail.SetParent(designRt, false);
            detail.anchorMin = Vector2.zero;
            detail.anchorMax = Vector2.one;
            detail.offsetMin = detail.offsetMax = Vector2.zero;

            // frames: the plain outer diamond (back_2) behind the ornate one (back_3), both centred on the picture
            AddArt(detail, "FrameOuter", ItemArt.FrameOuter, 283f - 178f, 222f - 181f, 356f, 362f);
            AddArt(detail, "FrameOrnate", ItemArt.FrameOrnate, 113f, 52f, 340f, 340f);
            var pic = Box("Picture", detail, 148f, 87f, 270f, 270f);
            picture = pic.gameObject.AddComponent<Image>();
            picture.preserveAspect = true;
            picture.raycastTarget = false;
            picture.enabled = false;

            // the seal at the frame's lower right, naming the kind in red
            var sealBox = AddArt(detail, "Seal", ItemArt.Seal, 392.4f, 321.4f, 26f, 64f);
            seal = DetailOverlay.CloneText(access, textSource, sealBox, "SealText", 25, TextAnchor.UpperCenter, SealRed, false);
            var sealRt = seal.rectTransform;
            sealRt.anchorMin = sealRt.anchorMax = sealRt.pivot = new Vector2(0.5f, 1f);
            sealRt.anchoredPosition = new Vector2(0f, -7.2f);
            sealRt.sizeDelta = new Vector2(26f, 54.2f);
            seal.horizontalOverflow = HorizontalWrapMode.Wrap;
            seal.verticalOverflow = VerticalWrapMode.Overflow;

            // the vertical name on its brush stroke
            AddArt(detail, "Brush", ItemArt.Brush, 10.4f, 20.6f, 80f, 417f);
            var titleBox = Box("Title", detail, 39.2f, 77.4f, 30f, 300f);
            title = DetailOverlay.CloneText(access, textSource, titleBox, "TitleText", 36, TextAnchor.UpperLeft, TitleGold, true);
            title.resizeTextMinSize = 14;
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            title.verticalOverflow = VerticalWrapMode.Truncate;
            Stretch(title.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var titleShadow = title.gameObject.AddComponent<Shadow>();
            titleShadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
            titleShadow.effectDistance = new Vector2(2f, -1f);

            // the description: 360 x 190 scroll view, 330 wide text, thin bar
            var descView = Box("DescViewport", detail, 119f, 400f, 360f, 190f);
            descView.gameObject.AddComponent<RectMask2D>();
            // An invisible target so the mouse wheel over the description reaches its ScrollRect.
            var descHit = descView.gameObject.AddComponent<Image>();
            descHit.color = new Color(0f, 0f, 0f, 0f);
            descHit.raycastTarget = true;
            descContent = NewRect("DescContent", descView, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, 0f), new Vector2(-20f, 0f));
            descContent.pivot = new Vector2(0.5f, 1f);
            desc = DetailOverlay.CloneText(access, textSource, descContent, "Desc", 18, TextAnchor.UpperLeft, RowNormal, false);
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            desc.verticalOverflow = VerticalWrapMode.Overflow;
            desc.resizeTextForBestFit = false;
            desc.fontSize = 18;
            desc.lineSpacing = 1.1f; // the game's description line spacing
            // The layout group gives the content the text's preferred height; a zero-height rect would be culled by the mask.
            var descLayout = descContent.gameObject.AddComponent<VerticalLayoutGroup>();
            descLayout.childControlWidth = true;
            descLayout.childControlHeight = true;
            descLayout.childForceExpandWidth = true;
            descLayout.childForceExpandHeight = false;
            descContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            descScroll = descView.gameObject.AddComponent<ScrollRect>();
            ConfigureScroll(descScroll, descContent, descView, 15f);
            AddScrollbar(descScroll, descView);
        }

        /// <summary>A picture of ours at a game-space position; a transparent stand-in when the picture cannot be decoded.</summary>
        private static RectTransform AddArt(RectTransform parent, string name, string art, float x, float y, float w, float h)
        {
            var rt = Box(name, parent, x, y, w, h);
            var image = rt.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            var sprite = ItemArt.Get(art);
            if (sprite != null) image.sprite = sprite;
            else image.color = new Color(0f, 0f, 0f, 0f);
            return rt;
        }

        /// <summary>The game's slim scrollbar: 5 wide at the view's right edge, pale track and handle; hidden when nothing scrolls.</summary>
        private static void AddScrollbar(ScrollRect scroll, RectTransform view)
        {
            var barRt = NewRect("Scrollbar", view, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-5f, 8.5f), new Vector2(0f, -8.5f));
            var track = barRt.gameObject.AddComponent<Image>();
            track.color = new Color(0.847f, 0.835f, 0.835f, 0.33f);
            var area = NewRect("Area", barRt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var handleRt = NewRect("Handle", area, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var handle = handleRt.gameObject.AddComponent<Image>();
            handle.color = new Color(0.847f, 0.835f, 0.835f, 1f);
            var bar = barRt.gameObject.AddComponent<Scrollbar>();
            bar.handleRect = handleRt;
            bar.targetGraphic = handle;
            bar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            scroll.verticalScrollbarSpacing = 0f;
        }

        private static void ConfigureScroll(ScrollRect scroll, RectTransform content, RectTransform viewport, float sensitivity)
        {
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.scrollSensitivity = sensitivity;
        }

        /// <summary>A rect at a top-left position (x right, y down) and size, in game-space units.</summary>
        private static RectTransform Box(string name, Transform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        private static RectTransform NewRect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        private static void Stretch(RectTransform rt, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }
    }
}
