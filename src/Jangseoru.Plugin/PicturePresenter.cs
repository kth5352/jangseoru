using System;
using Jangseoru.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Jangseoru.Plugin
{
    /// <summary>
    /// The 사건 (event illustration) detail: a full-screen overlay with its own sorting canvas under our panel root, showing an
    /// open book with the illustration on the left page and the card's number and group name on the right page. The frame is
    /// the game's own <c>pic_book_1</c> when <see cref="BookSpriteKeeper"/> holds it (by reference only), otherwise two
    /// <c>pic_paper_002</c> sheets (the verified Probe2 layout); both behave the same. The illustration is loaded with
    /// Addressables through the shared <see cref="SpriteLoader"/> and its handle is released exactly once when the detail
    /// closes for any reason (an in-flight load is released too and its result never used). Closes on a click anywhere or
    /// Esc/Submit/Space; while open the library's close key binding is switched off. Per-frame work runs from
    /// <see cref="Tick"/> (our panel driver's Update). Every entry point is wrapped; a failure closes the overlay, releases the
    /// handle and the lock, and switches the detail off for the session. Game data is never changed.
    /// </summary>
    internal sealed class PicturePresenter
    {
        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.6f); // no white backdrop: the book is its own paper
        private static readonly Color InkColor = DetailOverlay.InkColor;

        private readonly GameAccess access;
        private readonly ILog log;
        private readonly SpriteLoader loader;
        private readonly Component library;
        private readonly Transform host;
        private readonly GameObject textSource;
        private readonly Sprite paperSprite;

        private GameObject overlay, bookGo, paperLeftGo, paperRightGo;
        private RectTransform overlayRt, frameRt;
        private Canvas canvas;
        private CanvasGroup group;
        private Image bookImage, picture;
        private Text notice, number, groupText, plate;

        private object handle;      // Addressables handle of the open illustration; released exactly once
        private bool applied;       // the handle finished and its result was applied (or failed)
        private bool locked, broken, keysOff;
        private string openKey;
        private int openFrame;
        private GameObject savedSelection;

        public PicturePresenter(GameAccess access, ILog log, SpriteLoader loader, Component library, Transform host,
            GameObject textSource, Sprite paperSprite)
        {
            this.access = access;
            this.log = log;
            this.loader = loader;
            this.library = library;
            this.host = host;
            this.textSource = textSource;
            this.paperSprite = paperSprite; // reference only
        }

        public bool IsOpen { get; private set; }

        // ================= entry points =================

        /// <summary>Opens the detail for an unlocked catalog key. Ignored while already open.</summary>
        public void Open(string catalogKey)
        {
            try
            {
                if (IsOpen)
                {
                    Info("picture detail " + catalogKey + ": ignored, " + openKey + " is already open");
                    return;
                }
                if (broken) return;
                if (host == null || library == null || loader == null || !host.gameObject.activeInHierarchy)
                {
                    Info("picture detail " + catalogKey + ": not opened (library tab not showing)");
                    return;
                }
                if (!PictureCatalog.Contains(catalogKey))
                {
                    Info("picture detail " + catalogKey + ": not in the catalog; not opened");
                    return;
                }

                BookSpriteKeeper.TryCapture(); // throttled; finds the book only if a Story scene left it loaded
                var book = BookSpriteKeeper.Book;

                EnsureBuilt();

                IsOpen = true;
                openKey = catalogKey;
                openFrame = Time.frameCount;
                applied = false;

                locked = access.SetLibraryInputClose(library, false);
                if (!locked) Warn("picture detail " + catalogKey + ": could not switch off the library close key");

                var es = EventSystem.current;
                savedSelection = es != null ? es.currentSelectedGameObject : null;
                if (es != null) es.SetSelectedGameObject(null); // Submit must not reach the card underneath

                group.alpha = 1f;
                group.blocksRaycasts = true; // Finish switches it off
                overlay.SetActive(true);
                DetailOverlay.FitToScreen(overlayRt, host);
                DetailOverlay.SortAboveLibrary(canvas, library, host);
                Layout(book);

                number.gameObject.SetActive(false); // no number / group caption on the pages
                groupText.gameObject.SetActive(false);

                picture.sprite = null;
                picture.enabled = false;
                notice.gameObject.SetActive(false);

                handle = loader.Load(catalogKey, "detail image");
                if (handle == null) ShowLoadFailed("the load could not start");

                Info("picture detail " + catalogKey + ": opened (frame " + FrameName(book) + ", live handles " + loader.Live + ")");
            }
            catch (Exception e) { Fail("open", e); }
        }

        /// <summary>Closes at once (tab hidden, library closed, teardown). Idempotent; never throws.</summary>
        public void Close() => Finish("closed early");

        /// <summary>Every frame while our panel is active: applies the finished load and watches the close keys.</summary>
        public void Tick()
        {
            if (!IsOpen) return;
            try
            {
                if (handle != null && !applied && loader.Poll(handle, out var sprite))
                {
                    applied = true;
                    if (sprite != null)
                    {
                        picture.sprite = sprite; // the handle's result; cleared before the handle is released
                        picture.enabled = true;
                    }
                    else ShowLoadFailed("the load did not succeed");
                }

                if (Time.frameCount > openFrame && CloseKeyPressed()) Finish("closed");
            }
            catch (Exception e) { Fail("frame", e); }
        }

        private void OnOverlayClick()
        {
            try
            {
                // The click that opened the detail never closes it (same frame).
                if (IsOpen && Time.frameCount > openFrame) Finish("closed");
            }
            catch (Exception e) { Fail("click", e); }
        }

        private bool CloseKeyPressed()
        {
            if (keysOff) return false;
            try
            {
                return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
                       || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton0) || Input.GetKeyDown(KeyCode.JoystickButton1);
            }
            catch (Exception e)
            {
                // Legacy input switched off in this build: the click still closes.
                keysOff = true;
                Warn("picture detail: keyboard close unavailable (" + e.GetType().Name + "); click to close");
                return false;
            }
        }

        private void ShowLoadFailed(string why)
        {
            applied = true;
            Warn("picture detail " + openKey + ": image could not be loaded (" + why + "); showing a notice");
            picture.sprite = null;
            picture.enabled = false;
            notice.text = Loc.T(UiText.LoadFailed);
            notice.gameObject.SetActive(true);
        }

        // ================= close =================

        private void Finish(string reason)
        {
            if (!IsOpen) return;
            IsOpen = false;
            var key = openKey;

            try
            {
                // Invisible and click-through first: a SetActive(false) refused while a parent is being disabled must not
                // leave an invisible overlay that still blocks the library.
                if (group != null)
                {
                    group.alpha = 0f;
                    group.blocksRaycasts = false;
                }
                if (picture != null)
                {
                    picture.sprite = null; // no reference to the result survives its release
                    picture.enabled = false;
                }
                if (overlay != null) overlay.SetActive(false);
            }
            catch { }

            // Exactly once: the field is cleared before the call, so no path can release it again.
            var h = handle;
            handle = null;
            applied = false;
            try { loader?.Release(h); } catch { }

            if (locked)
            {
                locked = false;
                try
                {
                    if (!access.SetLibraryInputClose(library, true)) Warn("picture detail " + key + ": could not switch the library close key back on");
                }
                catch { }
            }

            try
            {
                var es = EventSystem.current;
                var sel = savedSelection;
                savedSelection = null;
                if (es != null && sel != null && sel.activeInHierarchy) es.SetSelectedGameObject(sel);
            }
            catch { }

            Info("picture detail " + key + ": " + reason + " (live handles " + (loader?.Live ?? 0) + ")");
            openKey = null;
        }

        private void Fail(string where, Exception e)
        {
            broken = true;
            Warn("picture detail disabled: " + where + " threw " + e.GetType().Name + ": " + e.Message);
            Finish("error");
            try { if (overlay != null) overlay.SetActive(false); } catch { }
        }

        // ================= build =================

        private void EnsureBuilt()
        {
            if (overlay != null) return;
            if (host == null || textSource == null) throw new InvalidOperationException("host or text template missing");

            overlay = new GameObject("PictureDetail", typeof(RectTransform));
            overlay.SetActive(false);
            overlayRt = (RectTransform)overlay.transform;
            overlayRt.SetParent(host, false);
            overlayRt.anchorMin = overlayRt.anchorMax = overlayRt.pivot = new Vector2(0.5f, 0.5f);
            overlayRt.SetAsLastSibling();

            canvas = overlay.AddComponent<Canvas>();
            overlay.AddComponent<GraphicRaycaster>();
            group = overlay.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = true;
            group.blocksRaycasts = false;

            // The only raycast target: a click anywhere (on the book too) lands here and closes.
            var dimGo = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            var dimRt = (RectTransform)dimGo.transform;
            dimRt.SetParent(overlayRt, false);
            DetailOverlay.Stretch(dimRt, Vector2.zero, Vector2.one);
            dimRt.sizeDelta = new Vector2(64f, 64f); // a little past the edges
            var dim = dimGo.GetComponent<Image>();
            dim.color = DimColor;
            dim.raycastTarget = true;
            dimGo.AddComponent<OathOverlayClick>().OnClick = OnOverlayClick;

            var frameGo = new GameObject("Frame", typeof(RectTransform));
            frameRt = (RectTransform)frameGo.transform;
            frameRt.SetParent(overlayRt, false);
            frameRt.anchorMin = frameRt.anchorMax = frameRt.pivot = new Vector2(0.5f, 0.5f);
            frameRt.anchoredPosition = Vector2.zero;

            bookImage = NewImage("Book", null, Color.white);
            bookGo = bookImage.gameObject;
            DetailOverlay.Stretch(bookImage.rectTransform, Vector2.zero, Vector2.one);

            var left = NewImage("PageLeft", paperSprite, Color.white);
            paperLeftGo = left.gameObject;
            DetailOverlay.Stretch(left.rectTransform, Vector2.zero, new Vector2(0.5f, 1f));
            var right = NewImage("PageRight", paperSprite, Color.white);
            paperRightGo = right.gameObject;
            DetailOverlay.Stretch(right.rectTransform, new Vector2(0.5f, 0f), Vector2.one);
            right.rectTransform.localScale = new Vector3(-1f, 1f, 1f); // mirrored like the probe's right page

            picture = NewImage("Picture", null, Color.white);
            picture.preserveAspect = true;
            picture.enabled = false;

            notice = NewText("Notice", 36, TextAnchor.MiddleCenter);
            notice.text = Loc.T(UiText.LoadFailed);
            notice.gameObject.SetActive(false);

            number = NewText("Number", 72, TextAnchor.LowerCenter);
            groupText = NewText("Group", 44, TextAnchor.UpperCenter);
            plate = NewText("Plate", 56, TextAnchor.MiddleCenter);
            plate.text = PictureDetailLayout.Vertical(Loc.T(UiText.Event));
        }

        /// <summary>Sizes the frame to the screen and places every part for the book or the paper layout.</summary>
        private void Layout(Sprite book)
        {
            var g = book != null ? PictureDetailLayout.Book : PictureDetailLayout.Papers;
            var size = overlayRt.sizeDelta;
            PictureDetailLayout.Fit(g.Width, g.Height, size.x * PictureDetailLayout.ScreenShare, size.y * PictureDetailLayout.ScreenShare,
                out var w, out var h);
            frameRt.sizeDelta = new Vector2(w, h);
            frameRt.anchoredPosition = Vector2.zero;

            bookImage.sprite = book; // the game's sprite by reference; never modified or destroyed
            bookGo.SetActive(book != null);
            paperLeftGo.SetActive(book == null);
            paperRightGo.SetActive(book == null);

            Place(picture.rectTransform, g.Picture);
            Place(notice.rectTransform, g.Picture);

            var c = g.Caption;
            var split = c.YMin + (c.YMax - c.YMin) * 0.45f;
            Place(number.rectTransform, new NormRect(c.XMin, split, c.XMax, c.YMax));
            Place(groupText.rectTransform, new NormRect(c.XMin, c.YMin, c.XMax, split));

            plate.gameObject.SetActive(g.HasPlate);
            if (g.HasPlate) Place(plate.rectTransform, g.Plate);
        }

        private Image NewImage(string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(frameRt, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite; // reference only
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>A clone of a card text (keeps the game font and its LeanLocalizedTextFont, loses LeanLocalizedText).</summary>
        private Text NewText(string name, int maxSize, TextAnchor alignment)
        {
            var go = Object.Instantiate(textSource, frameRt, false);
            go.name = name;
            foreach (var c in go.GetComponents<Component>())
            {
                if (c == null) continue;
                if (access.IsLeanLocalizedText(c) || c is ContentSizeFitter || c is LayoutElement || c is AspectRatioFitter)
                    Object.DestroyImmediate(c);
            }
            DetailOverlay.StripTextEffects(go);
            var text = go.GetComponent<Text>();
            if (text == null) throw new InvalidOperationException("text template has no Text");
            text.text = "";
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 12;
            text.resizeTextMaxSize = maxSize;
            text.fontSize = maxSize;
            text.color = InkColor;
            text.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            go.SetActive(true);
            return text;
        }

        private static void Place(RectTransform rt, NormRect r)
        {
            DetailOverlay.Stretch(rt, new Vector2(r.XMin, r.YMin), new Vector2(r.XMax, r.YMax));
        }

        private static string FrameName(Sprite book)
        {
            if (book == null) return "paper";
            try
            {
                var r = book.rect;
                return GameContract.BookSpriteName + " " + (int)r.width + "x" + (int)r.height;
            }
            catch (Exception) { return GameContract.BookSpriteName; }
        }

        private void Info(string m) { try { log.Info(m); } catch { } }

        private void Warn(string m) { try { log.Warn(m); } catch { } }
    }
}
