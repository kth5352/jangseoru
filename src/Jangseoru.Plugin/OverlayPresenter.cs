using System;
using Jangseoru.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Jangseoru.Plugin
{
    /// <summary>
    /// Replays an <see cref="OverlaySet"/> in the title library: the two combat event effects (transparent full-screen
    /// layers fading in one after another, held, then fading out together, with the timings read from the game's data) and
    /// the story's cut in (tall strips appearing one by one and staying). The pictures are catalog pictures loaded with
    /// Addressables through the shared <see cref="SpriteLoader"/>; every handle is released exactly once when the view
    /// closes. A timed set plays again after a short pause until the view is closed; closes on a click anywhere or
    /// Esc/Enter/Space; while open the library's close key binding is switched off. Per-frame work runs from
    /// <see cref="Tick"/>. Every entry point is wrapped; a failure closes the view, releases everything and switches it off
    /// for the session. Game data is never changed.
    /// </summary>
    internal sealed class OverlayPresenter
    {
        private static readonly Color Backdrop = new Color(0.1f, 0.1f, 0.12f, 1f);

        private readonly GameAccess access;
        private readonly ILog log;
        private readonly SpriteLoader loader;
        private readonly Component library;
        private readonly Transform host;

        private GameObject overlay;
        private RectTransform overlayRt, fieldRt;
        private Canvas canvas;
        private CanvasGroup group;
        private Image[] layers = new Image[0];

        private OverlaySet current;
        private object[] handles = new object[0];   // Addressables handles of the open set; each released exactly once
        private Sprite[] ready = new Sprite[0];
        private bool[] settled = new bool[0];        // the picture's load finished (or failed)
        private float playStart = -1f;               // unscaled time the timeline started; negative while pictures load
        private float openedAt;
        private bool locked, broken, keysOff;
        private int openFrame;
        private GameObject savedSelection;

        public OverlayPresenter(GameAccess access, ILog log, SpriteLoader loader, Component library, Transform host)
        {
            this.access = access;
            this.log = log;
            this.loader = loader;
            this.library = library;
            this.host = host;
        }

        public bool IsOpen { get; private set; }

        // ================= entry points =================

        public void Open(OverlaySet set)
        {
            try
            {
                if (set == null) return;
                if (IsOpen) { Info("overlay view " + set.Id + ": ignored, " + current?.Id + " is already open"); return; }
                if (broken) return;
                if (host == null || library == null || loader == null || !host.gameObject.activeInHierarchy)
                {
                    Info("overlay view " + set.Id + ": not opened (library tab not showing)");
                    return;
                }

                EnsureBuilt();

                IsOpen = true;
                current = set;
                openFrame = Time.frameCount;
                openedAt = Time.unscaledTime;
                playStart = -1f;

                locked = access.SetLibraryInputClose(library, false);
                if (!locked) Warn("overlay view " + set.Id + ": could not switch off the library close key");

                var es = EventSystem.current;
                savedSelection = es != null ? es.currentSelectedGameObject : null;
                if (es != null) es.SetSelectedGameObject(null);

                group.alpha = 1f;
                group.blocksRaycasts = true; // Finish switches it off
                overlay.SetActive(true);
                DetailOverlay.FitToScreen(overlayRt, host);
                DetailOverlay.SortAboveLibrary(canvas, library, host);

                var n = set.Keys.Count;
                var height = overlayRt.sizeDelta.y;
                handles = new object[n];
                ready = new Sprite[n];
                settled = new bool[n];
                for (var i = 0; i < layers.Length; i++)
                {
                    var img = layers[i];
                    img.enabled = false;
                    img.sprite = null;
                    img.gameObject.SetActive(i < n);
                    if (i >= n) continue;
                    var rt = img.rectTransform;
                    if (set.Layout == OverlayLayout.Strips)
                    {
                        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                        rt.sizeDelta = new Vector2(OverlaySets.StripAspect * height, height);
                        rt.anchoredPosition = new Vector2(OverlaySets.StripCenter(i, n) * height, 0f);
                    }
                    else
                    {
                        DetailOverlay.Stretch(rt, Vector2.zero, Vector2.one);
                    }

                    handles[i] = loader.Load(set.Keys[i], "overlay");
                    if (handles[i] == null)
                    {
                        settled[i] = true;
                        Warn("overlay view " + set.Id + ": picture " + set.Keys[i] + " could not start loading");
                    }
                }

                Info("overlay view " + set.Id + ": opened (" + n + " pictures, live handles " + loader.Live + ")");
            }
            catch (Exception e) { Fail("open", e); }
        }

        /// <summary>Closes at once (tab hidden, library closed, teardown). Idempotent; never throws.</summary>
        public void Close() => Finish("closed early");

        public void Tick()
        {
            if (!IsOpen) return;
            try
            {
                var now = Time.unscaledTime;
                var all = true;
                for (var i = 0; i < current.Keys.Count; i++)
                {
                    if (!settled[i] && handles[i] != null && loader.Poll(handles[i], out var sprite))
                    {
                        settled[i] = true;
                        if (sprite != null)
                        {
                            ready[i] = sprite; // the handle's result; cleared before the handle is released
                            layers[i].sprite = sprite;
                            layers[i].preserveAspect = true;
                        }
                        else Warn("overlay view " + current.Id + ": picture " + current.Keys[i] + " could not be loaded");
                    }
                    if (!settled[i]) all = false;
                }

                // The timeline starts once every picture is in (a short lead-in lets the eye settle).
                if (playStart < 0f && all) playStart = now + 0.3f;

                if (playStart >= 0f) Apply(now - playStart);

                if (Time.frameCount > openFrame && CloseKeyPressed()) Finish("closed");
            }
            catch (Exception e) { Fail("frame", e); }
        }

        private void Apply(float elapsed)
        {
            var t = elapsed < 0f ? 0f : elapsed;
            if (current.Hold > 0f)
            {
                var cycle = current.PlayLength + OverlaySets.ReplayPause;
                t %= cycle;
            }
            for (var i = 0; i < current.Keys.Count; i++)
            {
                var img = layers[i];
                if (ready[i] == null || elapsed < 0f) { img.enabled = false; continue; }
                var a = current.Alpha(i, t);
                img.enabled = a > 0.001f;
                img.color = new Color(1f, 1f, 1f, a);
            }
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
                Warn("overlay view: keyboard close unavailable (" + e.GetType().Name + "); click to close");
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
                foreach (var img in layers)
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
            settled = new bool[0];
            current = null;
            playStart = -1f;
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
                    if (!access.SetLibraryInputClose(library, true)) Warn("overlay view " + id + ": could not switch the library close key back on");
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

            Info("overlay view " + id + ": " + reason + " (live handles " + (loader?.Live ?? 0) + ")");
        }

        private void Fail(string where, Exception e)
        {
            broken = true;
            Warn("overlay view disabled: " + where + " threw " + e.GetType().Name + ": " + e.Message);
            Finish("error");
            try { if (overlay != null) overlay.SetActive(false); } catch { }
        }

        // ================= build =================

        private void EnsureBuilt()
        {
            if (overlay != null) return;
            if (host == null) throw new InvalidOperationException("host missing");

            overlay = new GameObject("OverlayView", typeof(RectTransform));
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
            dim.color = Backdrop;
            dim.raycastTarget = true;
            dimGo.AddComponent<OathOverlayClick>().OnClick = OnOverlayClick;

            var fieldGo = new GameObject("Field", typeof(RectTransform));
            fieldRt = (RectTransform)fieldGo.transform;
            fieldRt.SetParent(overlayRt, false);
            DetailOverlay.Stretch(fieldRt, Vector2.zero, Vector2.one);

            var max = 0;
            foreach (var set in OverlaySets.All) max = Math.Max(max, set.Keys.Count);
            layers = new Image[max];
            for (var i = 0; i < max; i++)
            {
                var go = new GameObject("Layer" + i, typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(fieldRt, false);
                rt.pivot = new Vector2(0.5f, 0.5f);
                var img = go.GetComponent<Image>();
                img.preserveAspect = true;
                img.raycastTarget = false;
                img.enabled = false;
                layers[i] = img;
            }
        }

        private void Info(string m) { try { log.Info(m); } catch { } }

        private void Warn(string m) { try { log.Warn(m); } catch { } }
    }
}
