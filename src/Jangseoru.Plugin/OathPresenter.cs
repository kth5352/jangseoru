using System;
using System.Collections;
using System.Collections.Generic;
using Jangseoru.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Jangseoru.Plugin
{
    /// <summary>Full-screen click catcher of the 맹세 detail. Only the click handler: no scroll/drag handlers to swallow.</summary>
    internal sealed class OathOverlayClick : MonoBehaviour, IPointerClickHandler
    {
        public Action OnClick;

        public void OnPointerClick(PointerEventData eventData)
        {
            try { OnClick?.Invoke(); }
            catch (Exception) { }
        }
    }

    /// <summary>
    /// The 맹세 (EndCg) detail: a re-creation of the game's CgPanel presentation inside the title library, built only from
    /// our own objects (a full-screen overlay with its own sorting canvas under our panel root, the EndCg picture by
    /// reference, and the description in clones of a card text). The game's CgPanel / EndGamePanel are never called
    /// (their Open increments Count and saves); the entry is only read, and Count is logged before and after.
    /// Timeline (<see cref="OathTimeline"/>): white background fades in 1s, picture and description fade in 1s, wait for
    /// a click / Submit / Esc, then the reverse (content out 1s, white out 1s). Horizontal layout for every language (the game's Chinese vertical layout is
    /// not re-created). While open the library's close key binding is switched off, as the game does for its own detail.
    /// Every entry point is wrapped; a failure closes the overlay, releases the lock and switches the detail off.
    /// </summary>
    internal sealed class OathPresenter
    {
        private const int StartFontSize = 34;
        private const int MinFontSize = 18;
        private static readonly Color DimColor = DetailOverlay.BackdropColor;
        private static readonly Color TextColor = DetailOverlay.InkColor;

        private readonly GameAccess access;
        private readonly ILog log;
        private readonly Component library;
        private readonly Transform host;
        private readonly GameObject textSource;
        private readonly MonoBehaviour runner;

        private GameObject overlay;
        private RectTransform overlayRt, desc;
        private Canvas canvas;
        private CanvasGroup group;
        private Image picture, backdrop;
        private GameObject lineTemplate;
        private readonly List<Text> lines = new List<Text>();
        private readonly List<float> lineAlphas = new List<float>();

        private OathTimeline timeline;
        private Coroutine routine;
        private int generation;
        private bool closeClicked, locked, broken, keysOff;
        private string openId;
        private int countBefore = -1;
        private GameObject savedSelection;

        public OathPresenter(GameAccess access, ILog log, Component library, Transform host, GameObject textSource, MonoBehaviour runner)
        {
            this.access = access;
            this.log = log;
            this.library = library;
            this.host = host;
            this.textSource = textSource;
            this.runner = runner;
        }

        public bool IsOpen { get; private set; }

        // ================= entry points =================

        /// <summary>Opens the detail for an unlocked EndCg id. Ignored while already open; a locked or unknown id only logs.</summary>
        public void Open(string id)
        {
            try
            {
                if (IsOpen)
                {
                    Info("oath detail " + id + ": ignored, " + openId + " is already open");
                    return;
                }
                if (broken) return;
                if (host == null || runner == null || library == null || !runner.isActiveAndEnabled)
                {
                    Info("oath detail " + id + ": not opened (library tab not showing)");
                    return;
                }
                if (!access.TryReadOath(id, out var entry, out var descKey))
                {
                    Info("oath detail " + id + ": not found in EndCg; not opened");
                    return;
                }
                if (entry.Count <= 0)
                {
                    Info("oath detail " + id + ": locked (count " + entry.Count + "); not opened");
                    return;
                }

                string text = null;
                if (string.IsNullOrEmpty(descKey) || !access.TryGetLocalizedString(descKey, out text) || string.IsNullOrEmpty(text))
                    Warn("oath detail " + id + ": no description text for '" + descKey + "'; showing the picture only");
                var split = OathTimeline.SplitLines(text);

                EnsureBuilt();

                IsOpen = true;
                openId = id;
                countBefore = entry.Count;
                closeClicked = false;

                locked = access.SetLibraryInputClose(library, false);
                if (!locked) Warn("oath detail " + id + ": could not switch off the library close key");

                var es = EventSystem.current;
                savedSelection = es != null ? es.currentSelectedGameObject : null;
                if (es != null) es.SetSelectedGameObject(null); // Submit must not reach the card underneath

                group.blocksRaycasts = true; // Finish switches it off
                overlay.SetActive(true);
                DetailOverlay.FitToScreen(overlayRt, host);
                DetailOverlay.SortAboveLibrary(canvas, library, host);

                picture.sprite = entry.Picture; // reference only; never modified or destroyed
                picture.enabled = entry.Picture != null;

                FillLines(split);
                timeline = new OathTimeline(lines.Count);
                Apply();

                var gen = ++generation;
                routine = runner.StartCoroutine(Run(gen));
                Info("oath detail " + id + ": opened (" + lines.Count + " lines, count " + countBefore + ")");
            }
            catch (Exception e) { Fail("open", e); }
        }

        /// <summary>Closes at once (tab hidden, library closed, teardown). Idempotent; never throws.</summary>
        public void Close() => Finish("closed early", false);

        // ================= timeline =================

        private IEnumerator Run(int gen)
        {
            var dt = 0f;
            while (true)
            {
                bool go;
                try { go = gen == generation && IsOpen && Step(dt); }
                catch (Exception e) { go = false; Fail("frame", e); }
                if (!go) yield break;
                yield return null;
                dt = Time.unscaledDeltaTime;
            }
        }

        private bool Step(float dt)
        {
            timeline.Advance(dt);
            if (timeline.AcceptsClose && (closeClicked || CloseKeyPressed())) timeline.RequestClose();
            closeClicked = false;
            Apply();
            if (!timeline.Finished) return true;
            Finish("closed", true);
            return false;
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
                Warn("oath detail: keyboard close unavailable (" + e.GetType().Name + "); click to close");
                return false;
            }
        }

        private void OnOverlayClick()
        {
            // Clicks before the wait are swallowed by the overlay and dropped (no card underneath gets them either).
            if (IsOpen && timeline != null && timeline.AcceptsClose) closeClicked = true;
        }

        private void Apply()
        {
            // The group stays opaque and each part fades by itself: the white first, then the picture and the text on top.
            group.alpha = timeline.Finished ? 0f : 1f;
            if (backdrop != null)
            {
                var b = DimColor;
                b.a *= timeline.BackdropAlpha;
                backdrop.color = b;
            }
            if (picture != null) picture.color = new Color(1f, 1f, 1f, timeline.ContentAlpha);
            for (var i = 0; i < lines.Count; i++)
            {
                var a = timeline.LineAlpha(i);
                if (lines[i] == null || Math.Abs(lineAlphas[i] - a) < 0.002f) continue;
                lineAlphas[i] = a;
                var c = TextColor;
                c.a *= a;
                lines[i].color = c;
            }
        }

        // ================= close =================

        private void Finish(string reason, bool fromRoutine)
        {
            if (!IsOpen) return;
            IsOpen = false;
            generation++; // any coroutine still scheduled stops at its next step
            var id = openId;

            if (!fromRoutine)
            {
                try { if (routine != null && runner != null) runner.StopCoroutine(routine); } catch { }
            }
            routine = null;
            timeline = null;
            closeClicked = false;

            try
            {
                // Invisible and click-through first: a SetActive(false) refused while a parent is being disabled must
                // not leave an invisible overlay that still blocks the library.
                if (group != null)
                {
                    group.alpha = 0f;
                    group.blocksRaycasts = false;
                }
                if (picture != null) picture.sprite = null;
                ClearLines();
                if (overlay != null) overlay.SetActive(false);
            }
            catch { }

            if (locked)
            {
                locked = false;
                try
                {
                    if (!access.SetLibraryInputClose(library, true)) Warn("oath detail " + id + ": could not switch the library close key back on");
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

            try
            {
                var after = access.TryReadOathCount(id, out var c) ? c : -1;
                var line = "oath detail " + id + ": count " + countBefore + " -> " + after + " (" + reason + ")";
                if (after == countBefore) Info(line);
                else Warn(line + " — count changed while the detail was open");
            }
            catch { }
            openId = null;
            countBefore = -1;
        }

        private void Fail(string where, Exception e)
        {
            broken = true;
            Warn("oath detail disabled: " + where + " threw " + e.GetType().Name + ": " + e.Message);
            Finish("error", true);
            try { if (overlay != null) overlay.SetActive(false); } catch { }
        }

        // ================= build =================

        private void EnsureBuilt()
        {
            if (overlay != null) return;
            if (host == null || textSource == null) throw new InvalidOperationException("host or text template missing");

            overlay = new GameObject("OathDetail", typeof(RectTransform));
            overlay.SetActive(false);
            overlayRt = (RectTransform)overlay.transform;
            overlayRt.SetParent(host, false);
            overlayRt.anchorMin = overlayRt.anchorMax = overlayRt.pivot = new Vector2(0.5f, 0.5f);
            overlayRt.SetAsLastSibling();

            // Own sorting canvas (as the game's own EndGamePanel has): drawn and hit-tested above the whole library,
            // including the fate panel and the close button.
            canvas = overlay.AddComponent<Canvas>();
            overlay.AddComponent<GraphicRaycaster>();
            group = overlay.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = true;
            group.blocksRaycasts = true;

            var dimGo = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            var dimRt = (RectTransform)dimGo.transform;
            dimRt.SetParent(overlayRt, false);
            DetailOverlay.Stretch(dimRt, Vector2.zero, Vector2.one);
            dimRt.sizeDelta = new Vector2(64f, 64f); // a little past the edges
            var dim = dimGo.GetComponent<Image>();
            dim.color = DimColor;
            backdrop = dim;
            dim.raycastTarget = true;
            dimGo.AddComponent<OathOverlayClick>().OnClick = OnOverlayClick;

            var picGo = new GameObject("Picture", typeof(RectTransform), typeof(Image));
            var picRt = (RectTransform)picGo.transform;
            picRt.SetParent(overlayRt, false);
            DetailOverlay.Stretch(picRt, new Vector2(0.03f, 0.03f), new Vector2(0.60f, 0.97f));
            picture = picGo.GetComponent<Image>();
            picture.preserveAspect = true;
            picture.raycastTarget = false;

            var descGo = new GameObject("Desc", typeof(RectTransform), typeof(VerticalLayoutGroup));
            desc = (RectTransform)descGo.transform;
            desc.SetParent(overlayRt, false);
            DetailOverlay.Stretch(desc, new Vector2(0.63f, 0.08f), new Vector2(0.96f, 0.92f));
            var vlg = descGo.GetComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 6f;

            // Line template: a clone of a card text (keeps the game font and its LeanLocalizedTextFont, loses LeanLocalizedText).
            lineTemplate = Object.Instantiate(textSource, overlayRt, false);
            lineTemplate.name = "LineTemplate";
            lineTemplate.SetActive(false);
            foreach (var c in lineTemplate.GetComponents<Component>())
            {
                if (c == null) continue;
                if (access.IsLeanLocalizedText(c) || c is ContentSizeFitter || c is LayoutElement || c is AspectRatioFitter)
                    Object.DestroyImmediate(c);
            }
            DetailOverlay.StripTextEffects(lineTemplate);
            var text = lineTemplate.GetComponent<Text>();
            if (text == null) throw new InvalidOperationException("text template has no Text");
            text.text = "";
            text.resizeTextForBestFit = false;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.fontSize = StartFontSize;
            text.color = new Color(TextColor.r, TextColor.g, TextColor.b, 0f);
            var trt = (RectTransform)lineTemplate.transform;
            trt.anchorMin = trt.anchorMax = new Vector2(0f, 1f);
            trt.pivot = new Vector2(0.5f, 0.5f);
            trt.localScale = Vector3.one;
            trt.localRotation = Quaternion.identity;
        }

        private void FillLines(IReadOnlyList<string> split)
        {
            ClearLines();
            foreach (var s in split)
            {
                var go = Object.Instantiate(lineTemplate, desc, false);
                go.name = "Line";
                go.SetActive(true);
                var t = go.GetComponent<Text>();
                t.text = s;
                t.fontSize = StartFontSize;
                lines.Add(t);
                lineAlphas.Add(-1f);
            }
            FitFontSize();
        }

        /// <summary>Shrinks the font until the whole description fits its column (long translations).</summary>
        private void FitFontSize()
        {
            if (lines.Count == 0) return;
            for (var size = StartFontSize; ; size -= 2)
            {
                foreach (var t in lines) t.fontSize = size;
                LayoutRebuilder.ForceRebuildLayoutImmediate(desc);
                var needed = LayoutUtility.GetPreferredHeight(desc);
                if (needed <= desc.rect.height || size - 2 < MinFontSize) return;
            }
        }

        private void ClearLines()
        {
            foreach (var t in lines)
            {
                if (t == null) continue;
                t.gameObject.SetActive(false);
                Object.Destroy(t.gameObject);
            }
            lines.Clear();
            lineAlphas.Clear();
        }

        private void Info(string m) { try { log.Info(m); } catch { } }

        private void Warn(string m) { try { log.Warn(m); } catch { } }
    }
}
