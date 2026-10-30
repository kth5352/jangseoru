using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Jangseoru.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Jangseoru.Plugin
{
    internal enum GalleryCategory
    {
        Oath,
        Event,
        Inyeon,
        Misc,
        Book,
        Special,
        Background,
        BigPicture,
        Video
    }

    /// <summary>Forwards Unity callbacks of our panel root to <see cref="GalleryUi"/>. Lives only on our own object.</summary>
    internal sealed class GalleryUiDriver : MonoBehaviour
    {
        internal GalleryUi Owner;

        private void Update() => Owner?.Tick();

        // Library closed (hierarchy deactivated) or our panel hidden: close the details (its coroutine runs here and stops
        // with this object) and give every Addressables handle back.
        private void OnDisable() => Owner?.OnDriverDisabled();

        private void OnDestroy() => Owner?.OnRootDestroyed(this);
    }

    /// <summary>
    /// The "장서루" tab of the library (도감): a menu button cloned from the Legend button and a panel <c>Panels/Jangseoru</c>
    /// assembled from clones of the EndGame panel's title, scroll view, number and bar (docs/infomation/22, probe Probe2).
    /// 맹세/사건 use the card grid; 인연 and 잡물/서적/보물 use dedicated views over the same area.
    /// Addressables thumbnails are loaded only for visible rows. Everything here creates, changes and destroys our own objects;
    /// every entry point is wrapped, and any failure tears our objects down and leaves the game's four tabs untouched.
    /// </summary>
    internal sealed class GalleryUi
    {
        private const string PanelName = "Jangseoru";
        private const string ButtonName = "MenuButton_Library_Jangseoru";

        private const float TabStripHeight = 56f;
        private const float TabWidth = 150f;
        private const float TabHeight = 50f;
        private const int TabFontSize = 34;
        private const int MarginRows = 1;
        private const int FallbackColumns = 4;
        private const float MediaCardAspect = 164f / 235f; // fix_pic_paper_003.png

        private sealed class Card
        {
            public GameObject Root;
            public Image Picture;
            public string Key;       // oath id or event catalog key
            public bool Unlocked;
            public object Handle;    // Addressables handle (event cards only)
            public bool Applied;     // handle finished and was applied (or failed)
            public bool LoadFailed;  // Load returned null: do not retry while the list is open
            public bool Embedded;    // thumbnail is a sprite we own (no Addressables handle)
        }

        private sealed class CardParts
        {
            public GameObject Back, Question, Index, Border;
            public Vector2 PicAnchorMin, PicAnchorMax, PicPivot, PicSize, PicPos;
        }

        private readonly GalleryFile file;
        private readonly ItemFile itemFile;
        private readonly GameAccess access;
        private readonly ILog log;
        private readonly SpriteLoader loader;
        private readonly InyeonFile inyeonFile;
        private readonly InyeonGameAccess inyeonAccess;
        private InyeonPanel inyeonPanel;

        /// <summary>Clicked an unlocked oath card; argument is the EndCg id (e.g. "40002"). Task 7 wires the detail view here.</summary>
        public Action<string> OnOathCardClick;

        /// <summary>Clicked an unlocked event card; argument is the catalog key (e.g. "pic_walk_001"). Task 8 wires the detail view here.</summary>
        public Action<string> OnEventCardClick;

        private Component library;
        private GameObject tabButton, root, partsHolder;
        private GalleryUiDriver driver;
        private OathPresenter oathPresenter;
        private PicturePresenter picturePresenter;
        private BubblePresenter bubblePresenter;
        private OverlayPresenter overlayPresenter;
        private CanvasGroup rootGroup;
        private RectTransform content, viewport;
        private GridLayoutGroup grid;
        private ScrollRect scroll;
        private Text numberText;
        private Image bar;
        private RectTransform processRt;
        private float processBaseY;
        private CardParts parts;

        private readonly Dictionary<GalleryCategory, Text> tabTexts = new Dictionary<GalleryCategory, Text>();
        private readonly Dictionary<GalleryCategory, GameObject> tabUnderlines = new Dictionary<GalleryCategory, GameObject>();
        private ItemView itemView;
        private bool itemTabs, itemWarned;
        private readonly MediaFile mediaFile;
        private readonly OathFile oathFile;
        private readonly ClipLoader clipLoader;
        private MediaPresenter mediaPresenter;
        // The game card grid as found (4 columns); the media tabs reshape it to 3 wider cards and it is restored for the other tabs.
        private bool gridSaved;
        private Vector2 gridCell0, gridSpacing0;
        private RectOffset gridPadding0;
        private GridLayoutGroup.Constraint gridConstraint0;
        private int gridCount0;
        private Color tabColor = Color.white;
        private int tabFontSize = TabFontSize;
        private UiLanguage builtLanguage = UiLanguage.Korean;

        private GalleryCategory category = GalleryCategory.Oath;
        private readonly List<Card> cards = new List<Card>();
        private readonly List<int> pending = new List<int>();
        private int windowFirst = -2, windowLast = -2;

        private bool broken, skipLogged, endCgWarned, tickWarned;

        public GalleryUi(GalleryFile file, ItemFile itemFile, GameAccess access, ILog log, InyeonFile inyeonFile = null, InyeonGameAccess inyeonAccess = null,
            MediaFile mediaFile = null, OathFile oathFile = null)
        {
            this.mediaFile = mediaFile;
            this.oathFile = oathFile;
            clipLoader = ClipLoader.Create(access, log);
            this.file = file;
            this.itemFile = itemFile;
            this.access = access;
            this.log = log;
            this.inyeonFile = inyeonFile;
            this.inyeonAccess = inyeonAccess;
            loader = SpriteLoader.Create(access, log);
            OnOathCardClick = OpenOath;
            OnEventCardClick = OpenPicture;
        }

        public GalleryCategory Category => category;

        /// <summary>The library panel our tab lives in (null before the first build or after a teardown).</summary>
        public Component Library => broken ? null : library;

        /// <summary>The records changed underneath us (F11 popup): refill the showing tab. Never throws.</summary>
        public void RefreshAfterDataChange()
        {
            try { if (!broken && root != null && root.activeInHierarchy) Refill(); }
            catch (Exception e) { Disable("refresh", e); }
        }

        /// <summary>The 사건 detail can be created (the shared Addressables loader was bound).</summary>
        public bool PictureDetailCreatable => loader != null;

        /// <summary>The 맹세 detail is showing (null presenter = detail unavailable).</summary>
        public bool OathDetailOpen => oathPresenter != null && oathPresenter.IsOpen;

        /// <summary>The 사건 detail is showing (null presenter = detail unavailable).</summary>
        public bool PictureDetailOpen => (picturePresenter != null && picturePresenter.IsOpen) || (bubblePresenter != null && bubblePresenter.IsOpen)
                                                     || (overlayPresenter != null && overlayPresenter.IsOpen) || (inyeonPanel != null && inyeonPanel.ZoomOpen)
                                                     || (mediaPresenter != null && mediaPresenter.IsOpen);

        // ================= entry points (called from Hooks; each one is wrapped) =================

        /// <summary>LibraryPanel.OnPanelOpen postfix: builds our objects the first time, afterwards only hides our panel.</summary>
        public void OnLibraryOpen(Component libraryPanel)
        {
            if (broken || libraryPanel == null) return;
            try
            {
                var language = UiLanguages.FromGame(access.ReadLanguage());
                if (root != null && library == libraryPanel)
                {
                    if (language == builtLanguage)
                    {
                        // Reopened: the game has already shown 한청서 (OpenEndPanel runs before this postfix).
                        HideOurs();
                        return;
                    }
                    // The display language changed since we built: rebuild every text in the new language. The old objects are
                    // renamed first so the name check below does not see them while their Destroy is still pending.
                    Info("display language changed (" + builtLanguage + " -> " + language + "); rebuilding the tab");
                    if (root != null) root.name = PanelName + "_retired";
                    if (tabButton != null) tabButton.name = ButtonName + "_retired";
                }
                Loc.Current = language;
                builtLanguage = language;

                // A different LibraryPanel instance (scene reload): our old objects belong to the old one.
                DestroyOurObjects();

                var libT = libraryPanel.transform;
                if (libT.Find("Panels/" + PanelName) != null || libT.Find("Buttons/" + ButtonName) != null)
                {
                    SkipOnce("objects named " + PanelName + " already exist under the library panel");
                    return;
                }
                if (!Validate(libraryPanel, out var reason))
                {
                    SkipOnce(reason);
                    return;
                }

                Build(libraryPanel);
            }
            catch (Exception e) { Disable("build", e); }
        }

        /// <summary>Another tab opened (Open*Panel postfix) or the library reopened: hide our panel and release thumbnails.</summary>
        public void HideOurs()
        {
            try
            {
                CloseDetails();
                if (root != null && root.activeSelf) root.SetActive(false); // OnDisable releases as well
                ReleaseThumbnails("hide");
            }
            catch (Exception e) { Disable("hide", e); }
        }

        /// <summary>Our root went inactive (panel hidden or library closed): close the detail and release thumbnails.</summary>
        public void OnDriverDisabled()
        {
            CloseDetails();
            ReleaseThumbnails("hidden");
        }

        private void OpenOath(string id)
        {
            if (oathPresenter == null) { Info("oath card clicked: " + id + " (detail unavailable)"); return; }
            oathPresenter.Open(id); // wrapped inside
        }

        private void OpenPicture(string key)
        {
            var overlaySet = OverlaySets.ForCardKey(key);
            if (overlaySet != null)
            {
                if (overlayPresenter == null) { Info("overlay card clicked: " + key + " (view unavailable)"); return; }
                overlayPresenter.Open(overlaySet); // wrapped inside
                return;
            }
            var set = BubbleSets.ForCatalogKey(key);
            if (set != null)
            {
                if (bubblePresenter == null) { Info("bubble card clicked: " + key + " (view unavailable)"); return; }
                bubblePresenter.Open(set); // wrapped inside
                return;
            }
            if (picturePresenter == null) { Info("event card clicked: " + key + " (detail unavailable)"); return; }
            picturePresenter.Open(key); // wrapped inside
        }

        /// <summary>Closes whichever detail is open (both are idempotent and never throw).</summary>
        private void CloseDetails()
        {
            try { inyeonPanel?.Hide(); } catch { }
            try { oathPresenter?.Close(); } catch { }
            try { picturePresenter?.Close(); } catch { }
            try { bubblePresenter?.Close(); } catch { }
            try { overlayPresenter?.Close(); } catch { }
            try { itemView?.Close(); } catch { }
            try { mediaPresenter?.Close(); } catch { }
        }

        /// <summary>LibraryPanel.CloseButtonClick postfix.</summary>
        public void OnLibraryClosed() => HideOurs();

        /// <summary>Tears our objects down for the rest of the session. The game's own tabs are never touched.</summary>
        public void Disable(string where, Exception e)
        {
            if (broken) return;
            broken = true;
            try { log.Warn("jangseoru tab disabled: " + where + " threw " + e.GetType().Name + ": " + e.Message); } catch { }
            try
            {
                var wasShowing = root != null && root.activeInHierarchy;
                DestroyOurObjects();
                if (wasShowing) access.ShowEndPanel(library); // never leave the library with no panel showing
            }
            catch { }
        }

        // ================= build =================

        private bool Validate(Component lib, out string reason)
        {
            reason = null;
            var t = lib.transform;
            string[] required =
            {
                "Buttons", "Buttons/MenuButton_Library_Legend", "Panels", "Panels/EndGame", "Panels/EndGame/Title",
                "Panels/EndGame/Title/Text", "Panels/EndGame/Title/Line1", "Panels/EndGame/Scroll View",
                "Panels/EndGame/Scroll View/Viewport", "Panels/EndGame/Scroll View/Viewport/Content",
                "Panels/EndGame/Number", "Panels/EndGame/Process", "Panels/EndGame/Process/Bar",
            };
            foreach (var path in required)
            {
                if (t.Find(path) == null) { reason = "missing " + path; return false; }
            }

            if (t.Find("Buttons/MenuButton_Library_Legend").GetComponent<Button>() == null) { reason = "Legend button has no Button"; return false; }
            if (t.Find("Panels/EndGame/Title/Text").GetComponent<Text>() == null) { reason = "EndGame title has no Text"; return false; }
            if (t.Find("Panels/EndGame/Title/Line1").GetComponent<Image>() == null) { reason = "EndGame title line has no Image"; return false; }
            if (t.Find("Panels/EndGame/Scroll View").GetComponent<ScrollRect>() == null) { reason = "EndGame scroll view has no ScrollRect"; return false; }
            if (t.Find("Panels/EndGame/Scroll View/Viewport/Content").GetComponent<GridLayoutGroup>() == null) { reason = "EndGame content has no GridLayoutGroup"; return false; }
            if (t.Find("Panels/EndGame/Number").GetComponent<Text>() == null) { reason = "EndGame number has no Text"; return false; }
            if (t.Find("Panels/EndGame/Process/Bar").GetComponent<Image>() == null) { reason = "EndGame bar has no Image"; return false; }

            var gameContent = t.Find("Panels/EndGame/Scroll View/Viewport/Content");
            if (gameContent.childCount == 0) { reason = "missing Panels/EndGame/Scroll View/Viewport/Content first child"; return false; }
            var templ = gameContent.GetChild(0);
            if (templ.Find("Back")?.GetComponent<Image>() == null) { reason = "card template has no Back image"; return false; }
            if (templ.Find("NotAchieveText") == null) { reason = "card template has no NotAchieveText"; return false; }
            if (!(templ.Find("Picture") is RectTransform)) { reason = "card template has no Picture"; return false; }
            if (templ.Find("Index")?.GetComponent<Text>() == null) { reason = "card template has no Index text"; return false; }
            if (templ.Find("Border")?.GetComponent<Image>() == null) { reason = "card template has no Border image"; return false; }

            if (!access.LibraryPanelsPresent(lib)) { reason = "LibraryPanel fields (_endPanel, _deadPanel, _achievementPanel, _legendPanel, _fatePanel, _inputCloseButton) not set"; return false; }
            return true;
        }

        private void Build(Component lib)
        {
            library = lib;
            var libT = lib.transform;
            var panels = libT.Find("Panels");
            var endGame = panels.Find("EndGame");

            // Root first and inactive: cloned game components under it never wake up before we have trimmed them.
            root = new GameObject(PanelName, typeof(RectTransform), typeof(CanvasGroup));
            root.SetActive(false);
            var rt = (RectTransform)root.transform;
            rt.SetParent(panels, false);
            CopyRect((RectTransform)endGame, rt);
            rootGroup = root.GetComponent<CanvasGroup>();

            foreach (var n in new[] { "Title", "Scroll View", "Number", "Process" })
                Object.Instantiate(endGame.Find(n).gameObject, root.transform, false).name = n;

            BrushFont.Report = Info;
            var titleText = root.transform.Find("Title/Text").GetComponent<Text>();
            SetFixedText(titleText, Loc.T(UiText.TabTitle));

            numberText = root.transform.Find("Number").GetComponent<Text>();
            StripLocalization(numberText.gameObject);
            bar = root.transform.Find("Process/Bar").GetComponent<Image>();
            processRt = (RectTransform)root.transform.Find("Process");
            processBaseY = processRt.anchoredPosition.y;

            var scrollT = (RectTransform)root.transform.Find("Scroll View");
            scroll = scrollT.GetComponent<ScrollRect>();
            viewport = (RectTransform)scrollT.Find("Viewport");
            content = (RectTransform)viewport.Find("Content");
            if (scroll.content != content) scroll.content = content;
            grid = content.GetComponent<GridLayoutGroup>();
            for (var i = content.childCount - 1; i >= 0; i--) Object.DestroyImmediate(content.GetChild(i).gameObject); // clones of the game's cards

            BuildCardParts(endGame.Find("Scroll View/Viewport/Content").GetChild(0));
            itemTabs = DetectItemTabs();
            BuildCategoryTabs(scrollT, titleText, root.transform.Find("Title/Line1").GetComponent<Image>());
            inyeonPanel = new InyeonPanel(inyeonFile, access, inyeonAccess, loader, log, root.transform, scrollT, parts.Index, lib, titleText.gameObject);

            driver = root.AddComponent<GalleryUiDriver>();
            driver.Owner = this;

            // The 맹세 detail lives under our root (built on first open) and runs its timeline on our driver.
            oathPresenter = access.OathDetailResolved ? new OathPresenter(access, log, lib, root.transform, parts.Index, driver) : null;
            if (oathPresenter == null) Warn("oath detail readers missing; 맹세 cards will not open");

            // The 사건 detail: same host, ticked from our driver; it needs the Addressables loader.
            picturePresenter = loader != null
                ? new PicturePresenter(access, log, loader, lib, root.transform, parts.Index, parts.Back.GetComponent<Image>().sprite)
                : null;
            if (picturePresenter == null) Warn("Addressables loader missing; 사건 cards will not open");

            // The bubble presentations (two cards of the 사건 list): own view, same host and driver.
            bubblePresenter = new BubblePresenter(access, log, loader, lib, root.transform);

            // The combat event effects and the cut in: layered pictures on a dark view.
            overlayPresenter = loader != null ? new OverlayPresenter(access, log, loader, lib, root.transform) : null;

            // The 배경/큰 그림/영상 full-screen view.
            mediaPresenter = new MediaPresenter(access, log, loader, clipLoader, lib, root.transform, parts.Index);

            // The 잡물/서적/보물 screen (only when the game's item lists could be read): over the card grid's area.
            itemView = itemTabs ? new ItemView(access, log, root.transform, scrollT, parts.Index) : null;

            // The menu button last, so a failure above never leaves a visible button behind.
            BuildMenuButton(libT.Find("Buttons"), libT.Find("Buttons/MenuButton_Library_Legend"));

            Info("jangseoru tab built (catalog " + PictureCatalog.Count + " pictures, thumbnails " + (loader != null ? "on" : "off") + ")");
        }

        private void BuildMenuButton(Transform buttons, Transform legend)
        {
            // Same as the verified probe (P1): clone in place, replace the persistent onClick, fix the texts.
            tabButton = Object.Instantiate(legend.gameObject, buttons, false);
            tabButton.name = ButtonName;
            var button = tabButton.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(OnTabClick);

            foreach (var c in tabButton.GetComponents<Component>())
            {
                if (access.DisableKeyBindingIfAny(c, out var path))
                    Info("cloned menu button key binding '" + path + "' switched off");
            }
            foreach (var text in tabButton.GetComponentsInChildren<Text>(true))
                SetFixedText(text, Loc.T(UiText.TabTitle));
        }

        private void BuildCardParts(Transform templ)
        {
            // Our own copies of the card pieces, so later refills never depend on the game's card objects staying alive.
            partsHolder = new GameObject("CardParts", typeof(RectTransform));
            partsHolder.SetActive(false);
            partsHolder.transform.SetParent(root.transform, false);

            parts = new CardParts
            {
                Back = Object.Instantiate(templ.Find("Back").gameObject, partsHolder.transform, false),
                Question = Object.Instantiate(templ.Find("NotAchieveText").gameObject, partsHolder.transform, false),
                Index = Object.Instantiate(templ.Find("Index").gameObject, partsHolder.transform, false),
                Border = Object.Instantiate(templ.Find("Border").gameObject, partsHolder.transform, false),
            };
            parts.Back.name = "Back";
            parts.Question.name = "NotAchieveText";
            parts.Index.name = "Index";
            parts.Border.name = "Border";
            StripLocalization(parts.Question);
            StripLocalization(parts.Index);
            parts.Back.GetComponent<Image>().raycastTarget = true; // the original's Back does not raycast; clicks would fall through
            parts.Border.SetActive(false);

            var pic = (RectTransform)templ.Find("Picture");
            parts.PicAnchorMin = pic.anchorMin;
            parts.PicAnchorMax = pic.anchorMax;
            parts.PicPivot = pic.pivot;
            parts.PicSize = pic.sizeDelta;
            parts.PicPos = pic.anchoredPosition;

            var index = parts.Index.GetComponent<Text>();
            index.resizeTextForBestFit = true;
            index.resizeTextMinSize = Math.Min(12, index.fontSize);
            index.resizeTextMaxSize = index.fontSize;
        }

        private void BuildCategoryTabs(RectTransform scrollT, Text titleText, Image line)
        {
            // Free a strip at the top of the scroll view (its bottom stays where it was) for categories and progress.
            var oldTop = scrollT.anchoredPosition.y + (1f - scrollT.pivot.y) * scrollT.sizeDelta.y;
            var left = scrollT.anchoredPosition.x - scrollT.pivot.x * scrollT.sizeDelta.x;
            GalleryLayout.InsetTop(scrollT.anchoredPosition.y, scrollT.sizeDelta.y, scrollT.pivot.y, TabStripHeight, out var newY, out var newH);
            scrollT.anchoredPosition = new Vector2(scrollT.anchoredPosition.x, newY);
            scrollT.sizeDelta = new Vector2(scrollT.sizeDelta.x, newH);

            var strip = new GameObject("Categories", typeof(RectTransform));
            var srt = (RectTransform)strip.transform;
            srt.SetParent(root.transform, false);
            srt.anchorMin = scrollT.anchorMin;
            srt.anchorMax = scrollT.anchorMax;
            srt.pivot = new Vector2(0f, 1f);
            srt.sizeDelta = new Vector2(scrollT.sizeDelta.x, TabStripHeight);
            srt.anchoredPosition = new Vector2(left, oldTop);

            tabColor = titleText.color;
            var tabs = new List<KeyValuePair<GalleryCategory, string>>
            {
                new KeyValuePair<GalleryCategory, string>(GalleryCategory.Oath, Loc.T(UiText.Oath)),
                new KeyValuePair<GalleryCategory, string>(GalleryCategory.Event, Loc.T(UiText.Event)),
                new KeyValuePair<GalleryCategory, string>(GalleryCategory.Inyeon, Loc.T(UiText.Inyeon)),
            };
            if (itemTabs)
            {
                tabs.Add(new KeyValuePair<GalleryCategory, string>(GalleryCategory.Misc, ItemKinds.TabLabel(ItemKind.Misc, Loc.Current)));
                tabs.Add(new KeyValuePair<GalleryCategory, string>(GalleryCategory.Book, ItemKinds.TabLabel(ItemKind.Book, Loc.Current)));
                tabs.Add(new KeyValuePair<GalleryCategory, string>(GalleryCategory.Special, ItemKinds.TabLabel(ItemKind.Special, Loc.Current)));
            }
            tabs.Add(new KeyValuePair<GalleryCategory, string>(GalleryCategory.Background, MediaKinds.TabLabel(MediaKind.Background, Loc.Current)));
            tabs.Add(new KeyValuePair<GalleryCategory, string>(GalleryCategory.BigPicture, MediaKinds.TabLabel(MediaKind.BigPicture, Loc.Current)));
            tabs.Add(new KeyValuePair<GalleryCategory, string>(GalleryCategory.Video, MediaKinds.TabLabel(MediaKind.Video, Loc.Current)));
            // The original strip is 908.4 wide; six fixed 150-wide tabs would overflow it.
            // Place our cloned counter explicitly so its original pivot cannot overlap the last button.
            const float counterWidth = 130f, counterGap = 16f, tabGap = 10f;
            var counter = numberText.rectTransform;
            counter.SetParent(srt, false);
            counter.anchorMin = counter.anchorMax = new Vector2(1f, 1f);
            counter.pivot = new Vector2(1f, 1f);
            counter.sizeDelta = new Vector2(counterWidth, TabHeight);
            counter.anchoredPosition = new Vector2(0f, -(TabStripHeight - TabHeight) / 2f);
            numberText.alignment = TextAnchor.MiddleRight;
            numberText.raycastTarget = false;
            var width = GalleryLayout.CategoryTabWidth(srt.rect.width, tabs.Count, counterWidth + counterGap, tabGap, TabWidth);
            // Nine tabs are narrower than two glyphs at the usual size: one smaller size for all, set by the longest label.
            tabFontSize = GalleryLayout.TabFontSize(width, tabs.Max(t => t.Value.Length), TabFontSize, 18);
            for (var i = 0; i < tabs.Count; i++)
            {
                var cat = tabs[i].Key;
                MakeCategoryTab(srt, tabs[i].Value, i * (width + tabGap), width, titleText, line, () => SelectCategory(cat), out var text, out var underline);
                tabTexts[cat] = text;
                tabUnderlines[cat] = underline;
            }
            UpdateTabVisuals();
        }

        private void MakeCategoryTab(RectTransform parent, string label, float x, float width, Text textSource, Image lineSource, Action onClick,
            out Text text, out GameObject underline)
        {
            var go = new GameObject("Tab_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(width, TabHeight);
            rt.anchoredPosition = new Vector2(x, -(TabStripHeight - TabHeight) / 2f);

            var hit = go.GetComponent<Image>();
            hit.sprite = lineSource.sprite; // reference only
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;

            var button = go.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hit;
            button.onClick.AddListener(() =>
            {
                try { onClick(); }
                catch (Exception e) { Disable("category tab", e); }
            });

            var textGo = Object.Instantiate(textSource.gameObject, go.transform, false);
            textGo.name = "Text";
            text = textGo.GetComponent<Text>();
            SetFixedText(text, label);
            text.fontSize = tabFontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.raycastTarget = false;
            var trt = (RectTransform)textGo.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.pivot = new Vector2(0.5f, 0.5f);
            trt.sizeDelta = Vector2.zero;
            trt.anchoredPosition = Vector2.zero;

            underline = new GameObject("Underline", typeof(RectTransform), typeof(Image));
            var urt = (RectTransform)underline.transform;
            urt.SetParent(go.transform, false);
            urt.anchorMin = new Vector2(0.15f, 0f);
            urt.anchorMax = new Vector2(0.85f, 0f);
            urt.pivot = new Vector2(0.5f, 0f);
            urt.sizeDelta = new Vector2(0f, 3f);
            urt.anchoredPosition = Vector2.zero;
            var uimg = underline.GetComponent<Image>();
            uimg.sprite = lineSource.sprite;
            uimg.color = lineSource.color;
            uimg.raycastTarget = false;
        }

        // ================= tab and categories =================

        private void OnTabClick()
        {
            try
            {
                if (broken || root == null) return;
                if (!access.HideLibraryPanels(library))
                {
                    Warn("could not hide the game's library panels; not showing ours");
                    return;
                }
                var fate = access.GetFatePanel(library) as GameObject;
                if (fate != null) fate.SetActive(true); // as the game's own tabs leave it (probe P2)
                root.SetActive(true);
                rootGroup.alpha = 1f;
                rootGroup.interactable = true;
                rootGroup.blocksRaycasts = true;
                Refill(); // always fresh: unlocks may have changed since the last visit
            }
            catch (Exception e) { Disable("tab click", e); }
        }

        private void SelectCategory(GalleryCategory next)
        {
            if (broken || root == null) return;
            if (next == category && cards.Count > 0) return;
            category = next;
            Refill();
        }

        private void UpdateTabVisuals()
        {
            var dim = new Color(tabColor.r, tabColor.g, tabColor.b, tabColor.a * 0.45f);
            foreach (var pair in tabTexts)
                if (pair.Value != null) pair.Value.color = pair.Key == category ? tabColor : dim;
            foreach (var pair in tabUnderlines)
                if (pair.Value != null) pair.Value.SetActive(pair.Key == category);
        }

        private static bool IsMediaCategory(GalleryCategory c)
            => c == GalleryCategory.Background || c == GalleryCategory.BigPicture || c == GalleryCategory.Video;

        private static MediaKind MediaKindOf(GalleryCategory c)
            => c == GalleryCategory.Background ? MediaKind.Background : c == GalleryCategory.BigPicture ? MediaKind.BigPicture : MediaKind.Video;

        /// <summary>Gives the card grid the media layout (3 wide paper cards per row) or restores the game's own layout.</summary>
        private void ConfigureGrid(bool media)
        {
            if (grid == null) return;
            if (!gridSaved)
            {
                gridSaved = true;
                gridCell0 = grid.cellSize;
                gridSpacing0 = grid.spacing;
                gridPadding0 = new RectOffset(grid.padding.left, grid.padding.right, grid.padding.top, grid.padding.bottom);
                gridConstraint0 = grid.constraint;
                gridCount0 = grid.constraintCount;
            }
            if (!media)
            {
                grid.cellSize = gridCell0;
                grid.spacing = gridSpacing0;
                grid.padding = new RectOffset(gridPadding0.left, gridPadding0.right, gridPadding0.top, gridPadding0.bottom);
                grid.constraint = gridConstraint0;
                grid.constraintCount = gridCount0;
                return;
            }
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            var usable = viewport != null ? viewport.rect.width : 0f;
            var width = usable > 100f ? (usable - gridPadding0.horizontal - gridSpacing0.x * 2f - 16f) / 3f : gridCell0.x * 4f / 3f;
            grid.cellSize = new Vector2(width, width * MediaCardAspect);
        }

        private static bool IsItemCategory(GalleryCategory c)
            => c == GalleryCategory.Misc || c == GalleryCategory.Book || c == GalleryCategory.Special;

        private static ItemKind KindOf(GalleryCategory c)
            => c == GalleryCategory.Misc ? ItemKind.Misc : c == GalleryCategory.Book ? ItemKind.Book : ItemKind.Special;

        /// <summary>
        /// Reads the game's item lists once when the tab is built and logs what was found. The three item tabs exist only when
        /// the lists and the text readers work and at least one item was found; otherwise the other tabs are unaffected.
        /// </summary>
        private bool DetectItemTabs()
        {
            try
            {
                if (!access.ItemReadersResolved || !access.OathDetailResolved)
                {
                    Info("item tabs off: item members or text readers missing");
                    return false;
                }
                int[] all = new int[3], withImage = new int[3];
                var kinds = new[] { ItemKind.Book, ItemKind.Misc, ItemKind.Special };
                for (var i = 0; i < kinds.Length; i++)
                {
                    if (!access.TryReadItems(kinds[i], out var list)) continue;
                    all[i] = list.Count;
                    withImage[i] = list.Count(e => e.Image != null);
                }
                Info("item catalog: books=" + all[0] + " miscs=" + all[1] + " special=" + all[2]
                     + " (with image: " + withImage[0] + "/" + withImage[1] + "/" + withImage[2] + ")");
                if (all[0] + all[1] + all[2] == 0)
                {
                    Warn("item tabs off: the item lists are empty or unreadable here");
                    return false;
                }
                return true;
            }
            catch (Exception e)
            {
                Warn("item tabs off: " + e.GetType().Name);
                return false;
            }
        }

        private void ShowItems(GalleryCategory cat)
        {
            var kind = KindOf(cat);
            numberText.text = OathModel.Progress(0, 0);
            bar.fillAmount = 0f;
            if (itemView == null || itemView.Broken || !access.TryReadItems(kind, out var read))
            {
                if (!itemWarned) { itemWarned = true; Warn("the item list could not be shown"); }
                return;
            }

            Func<string, string> localize = key => access.TryGetLocalizedString(key, out var t) ? t : null;
            var sources = read.Select(e => new ItemSource { Kind = e.Kind, Id = e.Id, HasImage = e.Image != null, Name = localize(e.NameKey) }).ToList();
            var rows = ItemList.Build(kind, sources, itemFile?.Store);
            var byId = new Dictionary<string, ItemEntry>(StringComparer.Ordinal);
            foreach (var e in read)
                if (!byId.ContainsKey(e.Id)) byId[e.Id] = e;

            itemView.Open(rows, byId, localize);
            numberText.text = ItemList.Progress(rows);
            bar.fillAmount = rows.Count == 0 ? 0f : (float)rows.Count(r => r.Unlocked) / rows.Count;
        }

        private void Refill()
        {
            CloseDetails();
            var isItem = IsItemCategory(category);
            // The 인연 tab extends the content area downwards for its picture row: the progress bar makes room.
            if (processRt != null)
                processRt.anchoredPosition = new Vector2(processRt.anchoredPosition.x,
                    processBaseY - (category == GalleryCategory.Inyeon ? InyeonPanel.BarShift : 0f));
            ReleaseThumbnails("refill");
            foreach (var card in cards)
            {
                if (card.Root == null) continue;
                card.Root.SetActive(false); // out of the grid this frame; destroyed at the end of it
                Object.Destroy(card.Root);
            }
            cards.Clear();
            pending.Clear();
            windowFirst = windowLast = -2;

            ConfigureGrid(IsMediaCategory(category));
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0f);
            scroll.StopMovement();

            // Both dedicated views replace the card grid; close the previous view before opening the next.
            scroll.gameObject.SetActive(!isItem && category != GalleryCategory.Inyeon);
            if (category == GalleryCategory.Inyeon)
            {
                var known = InyeonCatalog.Characters.Count(c => inyeonFile?.Store.IsKnown(c.Id) ?? false);
                numberText.text = OathModel.Progress(known, InyeonCatalog.Characters.Count);
                bar.fillAmount = InyeonCatalog.Characters.Count == 0 ? 0f : (float)known / InyeonCatalog.Characters.Count;
                UpdateTabVisuals();
                inyeonPanel?.Show();
                return;
            }

            if (isItem)
            {
                try { ShowItems(category); }
                catch (Exception e) { Warn("item tab failed (" + e.GetType().Name + ": " + e.Message + ")"); }
                UpdateTabVisuals();
                return;
            }

            int unlocked = 0, total;
            if (IsMediaCategory(category))
            {
                var kind = MediaKindOf(category);
                var rows = MediaList.Build(kind, mediaFile?.Store);
                foreach (var row in rows)
                {
                    AddMediaCard(row);
                    if (row.Unlocked) unlocked++;
                }
                total = rows.Count;
            }
            else if (category == GalleryCategory.Oath)
            {
                if (!access.TryReadEndCg(out var entries) && !endCgWarned)
                {
                    endCgWarned = true;
                    Warn("could not read LibrarySystem.EndCg; 맹세 shows no cards");
                }
                var pictures = new Dictionary<string, Sprite>(StringComparer.Ordinal);
                foreach (var e in entries)
                    if (e.Id != null && !pictures.ContainsKey(e.Id)) pictures[e.Id] = e.Picture;

                var model = OathModel.Build(entries.Select(e => new KeyValuePair<string, int>(e.Id, e.Count)), oathFile?.Store);
                foreach (var oath in model)
                {
                    pictures.TryGetValue(oath.Id, out var sprite);
                    var shown = oath.Unlocked && sprite != null;
                    AddCard(oath.Id, oath.Unlocked, oath.Number.ToString(CultureInfo.InvariantCulture), shown ? sprite : null, false, PaperArt.Card);
                    if (oath.Unlocked) unlocked++;
                }
                total = model.Count;
            }
            else
            {
                var entries = GalleryLayout.EventEntries(PictureCatalog.Keys);
                var store = file?.Store;
                foreach (var entry in entries)
                {
                    var has = store != null && store.HasPicture(entry.UnlockKey);
                    var embedded = has && entry.Set != null && !entry.Set.UsesAddressables;
                    AddCard(entry.Key, has, has ? entry.Label : "???", embedded ? BubbleArt.For(entry.Key) : null, embedded);
                    if (has) unlocked++;
                }
                total = entries.Count;
            }

            numberText.text = OathModel.Progress(unlocked, total);
            bar.fillAmount = total == 0 ? 0f : (float)unlocked / total;
            UpdateTabVisuals();
        }

        private void AddCard(string key, bool unlocked, string label, Sprite sprite, bool embedded = false, Sprite paper = null)
        {
            var go = new GameObject("JCard_" + key, typeof(RectTransform));
            go.transform.SetParent(content, false);

            var back = Object.Instantiate(parts.Back, go.transform, false);
            back.name = "Back";
            if (paper != null) { var backImage = back.GetComponent<Image>(); if (backImage != null) backImage.sprite = paper; } // oath cards: our own paper picture
            var question = Object.Instantiate(parts.Question, go.transform, false);
            question.name = "NotAchieveText";
            question.SetActive(!unlocked);

            var picGo = new GameObject("Picture", typeof(RectTransform), typeof(Image));
            var prt = (RectTransform)picGo.transform;
            prt.SetParent(go.transform, false);
            prt.anchorMin = parts.PicAnchorMin;
            prt.anchorMax = parts.PicAnchorMax;
            prt.pivot = parts.PicPivot;
            prt.sizeDelta = parts.PicSize;
            prt.anchoredPosition = parts.PicPos;
            var picture = picGo.GetComponent<Image>();
            picture.preserveAspect = true;
            picture.raycastTarget = false;
            picture.sprite = sprite; // reference only; never modified or destroyed
            picGo.SetActive(sprite != null);

            var index = Object.Instantiate(parts.Index, go.transform, false);
            index.name = "Index";
            index.GetComponent<Text>().text = label;
            var border = Object.Instantiate(parts.Border, go.transform, false);
            border.name = "Border";
            border.SetActive(false);

            var button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = back.GetComponent<Image>();
            var cat = category;
            button.onClick.AddListener(() => OnCardClick(cat, key, unlocked));

            // Not EventTrigger: it would swallow scroll/drag and the wheel would stop working over cards.
            var hover = go.AddComponent<CardHover>();
            hover.OnSelected = () => { if (border != null) border.SetActive(true); };
            hover.OnDeselected = () => { if (border != null) border.SetActive(false); };
            hover.OnEnter = () =>
            {
                var es = EventSystem.current;
                if (es != null && go != null) es.SetSelectedGameObject(go);
            };

            cards.Add(new Card { Root = go, Picture = picture, Key = key, Unlocked = unlocked, Embedded = embedded && sprite != null });
        }

        /// <summary>A wide paper card of a media tab: the picture (or the video's first scene) in the paper's frame, its number below.</summary>
        private void AddMediaCard(MediaRow row)
        {
            var go = new GameObject("JCard_" + row.Key, typeof(RectTransform));
            go.transform.SetParent(content, false);

            var backGo = new GameObject("Back", typeof(RectTransform), typeof(Image));
            var backRt = (RectTransform)backGo.transform;
            backRt.SetParent(go.transform, false);
            DetailOverlay.Stretch(backRt, Vector2.zero, Vector2.one);
            var back = backGo.GetComponent<Image>();
            var paper = ItemArt.Get("fix_pic_paper_003");
            if (paper != null) back.sprite = paper;
            else back.color = new Color(0.82f, 0.76f, 0.62f, 1f);
            back.raycastTarget = true;

            // Videos show the first scene we ship; pictures get theirs from Addressables while visible.
            var embedded = row.Unlocked && row.Kind == MediaKind.Video;
            var thumb = embedded ? ItemArt.Get("thumb_" + row.Key) : null;
            embedded = embedded && thumb != null;

            var picGo = new GameObject("Picture", typeof(RectTransform), typeof(Image));
            var prt = (RectTransform)picGo.transform;
            prt.SetParent(go.transform, false);
            DetailOverlay.Stretch(prt, new Vector2(0.105f, 0.17f), new Vector2(0.9f, 0.86f));
            var picture = picGo.GetComponent<Image>();
            picture.preserveAspect = true;
            picture.raycastTarget = false;
            picture.sprite = thumb;
            picGo.SetActive(thumb != null);

            var question = Object.Instantiate(parts.Question, go.transform, false);
            question.name = "NotAchieveText";
            question.SetActive(!row.Unlocked);
            StretchObject(question, new Vector2(0.105f, 0.17f), new Vector2(0.9f, 0.86f));
            var mark = question.GetComponent<Text>(); // the game's "?" is sized for its own tall card: refit it to the wide one
            if (mark != null)
            {
                mark.alignment = TextAnchor.MiddleCenter;
                mark.resizeTextForBestFit = true;
                mark.resizeTextMinSize = 20;
                mark.resizeTextMaxSize = 90;
                mark.horizontalOverflow = HorizontalWrapMode.Overflow;
                mark.verticalOverflow = VerticalWrapMode.Overflow;
            }

            var index = Object.Instantiate(parts.Index, go.transform, false);
            index.name = "Index";
            index.GetComponent<Text>().text = row.Label;
            StretchObject(index, new Vector2(0f, 0f), new Vector2(1f, 0.17f));

            var border = Object.Instantiate(parts.Border, go.transform, false);
            border.name = "Border";
            StretchObject(border, Vector2.zero, Vector2.one);
            border.SetActive(false);

            var button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = back;
            button.onClick.AddListener(() =>
            {
                try
                {
                    if (!row.Unlocked || broken) return;
                    mediaPresenter?.Open(row);
                }
                catch (Exception e) { Warn("media card click threw " + e.GetType().Name + ": " + e.Message); }
            });

            // Not EventTrigger: it would swallow scroll/drag and the wheel would stop working over cards.
            var hover = go.AddComponent<CardHover>();
            hover.OnSelected = () => { if (border != null) border.SetActive(true); };
            hover.OnDeselected = () => { if (border != null) border.SetActive(false); };
            hover.OnEnter = () =>
            {
                var es = EventSystem.current;
                if (es != null && go != null) es.SetSelectedGameObject(go);
            };

            // The thumbnail window loads cards by Key; for pictures that is the Addressables address.
            cards.Add(new Card { Root = go, Picture = picture, Key = row.Address, Unlocked = row.Unlocked, Embedded = embedded });
        }

        private static void StretchObject(GameObject go, Vector2 min, Vector2 max)
        {
            var rt = (RectTransform)go.transform;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one;
        }

        private void OnCardClick(GalleryCategory cat, string key, bool unlocked)
        {
            try
            {
                if (!unlocked || broken) return;
                if (cat == GalleryCategory.Oath) OnOathCardClick?.Invoke(key);
                else OnEventCardClick?.Invoke(key);
            }
            catch (Exception e) { Warn("card click handler threw " + e.GetType().Name + ": " + e.Message); }
        }

        // ================= thumbnails =================

        /// <summary>Every frame while our panel is active (driver Update): keep thumbnails loaded for the visible rows only.</summary>
        public void Tick()
        {
            if (!broken) inyeonPanel?.Tick();
            if (!broken) picturePresenter?.Tick(); // wrapped inside
            if (!broken) bubblePresenter?.Tick(); // wrapped inside
            if (!broken) overlayPresenter?.Tick(); // wrapped inside
            if (!broken) mediaPresenter?.Tick(); // wrapped inside
            var loadsThumbnails = category == GalleryCategory.Event || category == GalleryCategory.Background || category == GalleryCategory.BigPicture;
            if (broken || root == null || !loadsThumbnails || loader == null || cards.Count == 0) return;
            try
            {
                var padding = grid.padding;
                var columns = GalleryLayout.Columns(grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount, grid.constraintCount,
                    content.rect.width, grid.cellSize.x, grid.spacing.x, padding.horizontal, FallbackColumns);
                var rowHeight = grid.cellSize.y + grid.spacing.y;
                var scrollOffset = Math.Max(0f, content.anchoredPosition.y - padding.top);
                GalleryLayout.ThumbnailWindow(cards.Count, columns, rowHeight, viewport.rect.height, scrollOffset, MarginRows,
                    out var first, out var last);

                if (first != windowFirst || last != windowLast) ApplyWindow(first, last);
                PollPending();
            }
            catch (Exception e)
            {
                // Thumbnails only: release what we hold, keep the list usable.
                if (!tickWarned) { tickWarned = true; Warn("thumbnail update threw " + e.GetType().Name + ": " + e.Message); }
                ReleaseThumbnails("error");
                windowFirst = windowLast = int.MinValue; // stop retrying every frame until the list is refilled
            }
        }

        private void ApplyWindow(int first, int last)
        {
            if (windowFirst == int.MinValue) return; // disabled after an error
            windowFirst = first;
            windowLast = last;

            int released = 0, started = 0;
            for (var i = 0; i < cards.Count; i++)
            {
                if (cards[i].Handle != null && (i < first || i > last)) { ReleaseCard(cards[i]); released++; }
            }
            pending.RemoveAll(i => cards[i].Handle == null);

            for (var i = Math.Max(0, first); i <= last && i < cards.Count; i++)
            {
                var card = cards[i];
                if (!card.Unlocked || card.Embedded || card.Handle != null || card.LoadFailed) continue;
                card.Handle = loader.Load(card.Key, "thumbnail");
                if (card.Handle == null) { card.LoadFailed = true; continue; }
                card.Applied = false;
                pending.Add(i);
                started++;
            }

            if (started > 0 || released > 0)
                Info("event thumbnails cards " + first + ".." + last + ": +" + started + " -" + released + ", live handles " + loader.Live);
        }

        private void PollPending()
        {
            for (var p = pending.Count - 1; p >= 0; p--)
            {
                var card = cards[pending[p]];
                if (card.Handle == null) { pending.RemoveAt(p); continue; }
                if (!loader.Poll(card.Handle, out var sprite)) continue;
                card.Applied = true;
                pending.RemoveAt(p);
                if (sprite != null && card.Picture != null)
                {
                    card.Picture.sprite = sprite;
                    card.Picture.gameObject.SetActive(true);
                }
            }
        }

        private void ReleaseCard(Card card)
        {
            var handle = card.Handle;
            card.Handle = null;
            card.Applied = false;
            try
            {
                if (card.Picture != null)
                {
                    card.Picture.sprite = null;
                    card.Picture.gameObject.SetActive(false);
                }
            }
            catch { }
            loader?.Release(handle);
        }

        /// <summary>Releases every Addressables handle we hold. Idempotent; never throws.</summary>
        public void ReleaseThumbnails(string reason)
        {
            try
            {
                var released = 0;
                foreach (var card in cards)
                {
                    if (card.Handle == null) continue;
                    ReleaseCard(card);
                    released++;
                }
                pending.Clear();
                if (windowFirst != int.MinValue) windowFirst = windowLast = -2;
                foreach (var card in cards) card.LoadFailed = false;
                if (released > 0) Info("event thumbnails released (" + reason + "): -" + released + ", live handles " + (loader?.Live ?? 0));
            }
            catch { }
        }

        /// <summary>Our root was destroyed (library destroyed with the scene): release and forget everything.</summary>
        public void OnRootDestroyed(GalleryUiDriver from)
        {
            if (from != driver) return;
            CloseDetails();
            ReleaseThumbnails("destroyed");
            cards.Clear();
            pending.Clear();
            ForgetObjects();
        }

        // ================= helpers =================

        private void DestroyOurObjects()
        {
            CloseDetails(); // releases the close-key lock while the library is still there
            ReleaseThumbnails("teardown");
            cards.Clear();
            pending.Clear();
            var b = tabButton;
            var r = root;
            if (driver != null) driver.Owner = null; // its deferred OnDisable/OnDestroy must not touch a rebuilt UI
            ForgetObjects();
            if (b != null) Object.Destroy(b);
            if (r != null) Object.Destroy(r);
        }

        private void ForgetObjects()
        {
            inyeonPanel?.Destroy();
            inyeonPanel = null;
            driver = null;
            oathPresenter = null;
            picturePresenter = null;
            bubblePresenter = null;
            overlayPresenter = null;
            mediaPresenter = null;
            tabButton = root = partsHolder = null;
            rootGroup = null;
            content = viewport = null;
            grid = null;
            scroll = null;
            numberText = null;
            bar = null;
            processRt = null;
            parts = null;
            tabTexts.Clear();
            tabUnderlines.Clear();
            itemView = null;
            library = null;
            windowFirst = windowLast = -2;
        }

        private void SetFixedText(Text text, string value)
        {
            StripLocalization(text.gameObject);
            text.text = value;
        }

        /// <summary>Removes LeanLocalizedText (it would overwrite our text) from our clone; LeanLocalizedTextFont stays.</summary>
        private void StripLocalization(GameObject go)
        {
            foreach (var c in go.GetComponents<Component>())
                if (c != null && access.IsLeanLocalizedText(c)) Object.Destroy(c);
        }

        private static void CopyRect(RectTransform from, RectTransform to)
        {
            to.anchorMin = from.anchorMin;
            to.anchorMax = from.anchorMax;
            to.pivot = from.pivot;
            to.sizeDelta = from.sizeDelta;
            to.anchoredPosition = from.anchoredPosition;
        }

        private void SkipOnce(string reason)
        {
            if (skipLogged) return;
            skipLogged = true;
            Warn("jangseoru tab skipped: " + reason);
        }

        private void Info(string m) { try { log.Info(m); } catch { } }

        private void Warn(string m) { try { log.Warn(m); } catch { } }
    }
}
