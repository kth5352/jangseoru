using System;
using Jangseoru.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Jangseoru.Plugin
{
    /// <summary>
    /// Replays an event-bubble presentation of the story in the title library: a white full-screen overlay on which the
    /// bubbles appear one after another at the story's positions, each swaying and breathing exactly like the story's
    /// particle effect (see <see cref="BubbleSets"/>). The pictures of a catalog set are loaded with Addressables through the
    /// shared <see cref="SpriteLoader"/> (every handle released exactly once when the view closes); the other set uses the
    /// copies decoded by <see cref="BubbleArt"/>. Closes on a click anywhere or Esc/Enter/Space; while open the library's close
    /// key binding is switched off. Per-frame work runs from <see cref="Tick"/>. Every entry point is wrapped; a failure closes
    /// the view, releases everything and switches it off for the session. Game data is never changed.
    /// </summary>
    internal sealed class BubblePresenter
    {
        /// <summary>Seconds before the first bubble appears.</summary>
        private const float FirstDelay = 0.6f;

        private readonly GameAccess access;
        private readonly ILog log;
        private readonly SpriteLoader loader;
        private readonly Component library;
        private readonly Transform host;

        private GameObject overlay;
        private RectTransform overlayRt, fieldRt;
        private Canvas canvas;
        private CanvasGroup group;
        private Image[] bubbles = new Image[0];

        private BubbleSet current;
        private object[] handles = new object[0];   // Addressables handles of the open set; each released exactly once
        private Sprite[] ready = new Sprite[0];
        private float[] appearAt = new float[0];     // unscaled time each bubble appeared; negative while hidden
        private float openedAt;
        private bool locked, broken, keysOff;
        private int openFrame;
        private GameObject savedSelection;

        public BubblePresenter(GameAccess access, ILog log, SpriteLoader loader, Component library, Transform host)
        {
            this.access = access;
            this.log = log;
            this.loader = loader;
            this.library = library;
            this.host = host;
        }

        public bool IsOpen { get; private set; }

        // ================= entry points =================

        /// <summary>Opens the replay of a bubble set. Ignored while a view is already open.</summary>
        public void Open(BubbleSet set)
        {
            try
            {
                if (set == null) return;
                if (IsOpen) { Info("bubble view " + set.Id + ": ignored, " + current?.Id + " is already open"); return; }
                if (broken) return;
                if (host == null || library == null || !host.gameObject.activeInHierarchy)
                {
                    Info("bubble view " + set.Id + ": not opened (library tab not showing)");
                    return;
                }
                if (set.UsesAddressables && loader == null)
                {
                    Info("bubble view " + set.Id + ": not opened (no picture loader)");
                    return;
                }

                EnsureBuilt(set.Frames.Count);

                IsOpen = true;
                current = set;
                openFrame = Time.frameCount;
                openedAt = Time.unscaledTime;

                locked = access.SetLibraryInputClose(library, false);
                if (!locked) Warn("bubble view " + set.Id + ": could not switch off the library close key");

                var es = EventSystem.current;
                savedSelection = es != null ? es.currentSelectedGameObject : null;
                if (es != null) es.SetSelectedGameObject(null);

                group.alpha = 1f;
                group.blocksRaycasts = true; // Finish switches it off
                overlay.SetActive(true);
                DetailOverlay.FitToScreen(overlayRt, host);
                DetailOverlay.SortAboveLibrary(canvas, library, host);

                var ppu = BubbleSets.PixelsPerUnit(overlayRt.sizeDelta.y);
                var n = set.Frames.Count;
                handles = new object[n];
                ready = new Sprite[n];
                appearAt = new float[n];
                for (var i = 0; i < bubbles.Length; i++)
                {
                    var img = bubbles[i];
                    img.enabled = false;
                    img.sprite = null;
                    img.gameObject.SetActive(i < n);
                    if (i >= n) continue;
                    var f = set.Frames[i];
                    var rt = img.rectTransform;
                    rt.sizeDelta = new Vector2(BubbleSets.BubbleSize * ppu, BubbleSets.BubbleSize * ppu);
                    rt.anchoredPosition = new Vector2(f.X * ppu, f.Y * ppu);
                    rt.localRotation = Quaternion.identity;
                    rt.localScale = Vector3.one;
                    appearAt[i] = -1f;

                    if (set.UsesAddressables)
                    {
                        handles[i] = loader.Load(f.CatalogKey, "bubble");
                        if (handles[i] == null) Warn("bubble view " + set.Id + ": picture " + f.CatalogKey + " could not start loading");
                    }
                    else
                    {
                        ready[i] = BubbleArt.For(f.CatalogKey);
                        if (ready[i] == null) Warn("bubble view " + set.Id + ": picture " + f.CatalogKey + " is unavailable");
                    }
                }

                Info("bubble view " + set.Id + ": opened (" + n + " bubbles, live handles " + (loader?.Live ?? 0) + ")");
            }
            catch (Exception e) { Fail("open", e); }
        }

        /// <summary>Closes at once (tab hidden, library closed, teardown). Idempotent; never throws.</summary>
        public void Close() => Finish("closed early");

        /// <summary>Every frame while our panel is active: finishes loads, shows due bubbles, sways them, watches the close keys.</summary>
        public void Tick()
        {
            if (!IsOpen) return;
            try
            {
                var now = Time.unscaledTime;
                var elapsed = now - openedAt;
                for (var i = 0; i < current.Frames.Count; i++)
                {
                    if (ready[i] == null && handles[i] != null && loader.Poll(handles[i], out var sprite) && sprite != null)
                        ready[i] = sprite; // the handle's result; cleared before the handle is released

                    var img = bubbles[i];
                    if (appearAt[i] < 0f)
                    {
                        if (ready[i] == null || elapsed < FirstDelay + i * current.Interval) continue;
                        appearAt[i] = now;
                        img.sprite = ready[i];
                        img.enabled = true;
                    }

                    BubbleSets.Sway(now - appearAt[i], out var degrees, out var scale);
                    var rt = img.rectTransform;
                    rt.localRotation = Quaternion.Euler(0f, 0f, degrees);
                    rt.localScale = new Vector3(scale, scale, 1f);
                }

                if (Time.frameCount > openFrame && CloseKeyPressed()) Finish("closed");
            }
            catch (Exception e) { Fail("frame", e); }
        }

        private void OnOverlayClick()
        {
            try
            {
                // The click that opened the view never closes it (same frame).
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
                keysOff = true; // legacy input switched off in this build: the click still closes
                Warn("bubble view: keyboard close unavailable (" + e.GetType().Name + "); click to close");
                return false;
            }
        }

        // ================= close =================

        private void Finish(string reason)
        {
            if (!IsOpen) return;
            IsOpen = false;
            var id = current?.Id;

            try
            {
                // Invisible and click-through first, so a refused SetActive(false) never leaves an overlay that blocks the library.
                if (group != null)
                {
                    group.alpha = 0f;
                    group.blocksRaycasts = false;
                }
                foreach (var img in bubbles)
                {
                    if (img == null) continue;
                    img.sprite = null; // no reference to a loaded result survives its release
                    img.enabled = false;
                }
                if (overlay != null) overlay.SetActive(false);
            }
            catch { }

            // Exactly once: the array is cleared before any call, so no path can release a handle again.
            var h = handles;
            handles = new object[0];
            ready = new Sprite[0];
            appearAt = new float[0];
            current = null;
            foreach (var handle in h)
            {
                if (handle == null) continue;
                try { loader?.Release(handle); } catch { }
            }

            if (locked)
            {
                locked = false;
                try
                {
                    if (!access.SetLibraryInputClose(library, true)) Warn("bubble view " + id + ": could not switch the library close key back on");
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

            Info("bubble view " + id + ": " + reason + " (live handles " + (loader?.Live ?? 0) + ")");
        }

        private void Fail(string where, Exception e)
        {
            broken = true;
            Warn("bubble view disabled: " + where + " threw " + e.GetType().Name + ": " + e.Message);
            Finish("error");
            try { if (overlay != null) overlay.SetActive(false); } catch { }
        }

        // ================= build =================

        private void EnsureBuilt(int bubbleCount)
        {
            if (overlay != null) return;
            if (host == null) throw new InvalidOperationException("host missing");

            overlay = new GameObject("BubbleView", typeof(RectTransform));
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

            // The only raycast target: a click anywhere lands here and closes.
            var dimGo = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            var dimRt = (RectTransform)dimGo.transform;
            dimRt.SetParent(overlayRt, false);
            DetailOverlay.Stretch(dimRt, Vector2.zero, Vector2.one);
            dimRt.sizeDelta = new Vector2(64f, 64f);
            var dim = dimGo.GetComponent<Image>();
            dim.color = DetailOverlay.BackdropColor; // the story shows its bubbles on a white view
            dim.raycastTarget = true;
            dimGo.AddComponent<OathOverlayClick>().OnClick = OnOverlayClick;

            // The bubbles' coordinate origin: the screen centre.
            var fieldGo = new GameObject("Field", typeof(RectTransform));
            fieldRt = (RectTransform)fieldGo.transform;
            fieldRt.SetParent(overlayRt, false);
            fieldRt.anchorMin = fieldRt.anchorMax = fieldRt.pivot = new Vector2(0.5f, 0.5f);
            fieldRt.anchoredPosition = Vector2.zero;

            var count = Math.Max(bubbleCount, 6);
            bubbles = new Image[count];
            for (var i = 0; i < count; i++)
            {
                var go = new GameObject("Bubble" + i, typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(fieldRt, false);
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                var img = go.GetComponent<Image>();
                img.preserveAspect = true;
                img.raycastTarget = false;
                img.enabled = false;
                bubbles[i] = img;
            }
        }

        private void Info(string m) { try { log.Info(m); } catch { } }

        private void Warn(string m) { try { log.Warn(m); } catch { } }
    }
}
