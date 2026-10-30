using System;
using Jangseoru.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Jangseoru.Plugin
{
    /// <summary>Full-screen portrait viewer. Borrows the panel's current sprite; owns no asset handles.</summary>
    internal sealed class InyeonZoom
    {
        private readonly GameAccess access;
        private readonly ILog log;
        private readonly Component library;
        private readonly Transform host;
        private readonly InyeonUiParts ui;
        private readonly Action<int> move;
        private GameObject root, savedSelection;
        private RectTransform rect;
        private Canvas canvas;
        private CanvasGroup group;
        private Image picture;
        private Text name, title, counter, notice;
        private Button previous, next;
        private bool locked, broken, inputUnavailable;
        private int openFrame;
        public bool IsOpen { get; private set; }

        public InyeonZoom(GameAccess access, ILog log, Component library, Transform host, InyeonUiParts ui, Action<int> move)
        {
            this.access = access;
            this.log = log;
            this.library = library;
            this.host = host;
            this.ui = ui;
            this.move = move;
        }

        public void Open(Sprite sprite, string personName, string epithet, string count, bool canMove)
        {
            if (broken || IsOpen || sprite == null || host == null || library == null) return;
            try
            {
                Build();
                locked = access.SetLibraryInputClose(library, false);
                if (!locked) { Warn("library close key could not be suspended"); return; }
                savedSelection = EventSystem.current?.currentSelectedGameObject;
                EventSystem.current?.SetSelectedGameObject(null);
                root.SetActive(true);
                rect.SetAsLastSibling();
                DetailOverlay.FitToScreen(rect, host);
                DetailOverlay.SortAboveLibrary(canvas, library, host);
                IsOpen = true;
                openFrame = Time.frameCount;
                group.alpha = 1;
                group.blocksRaycasts = true;
                Sync(sprite, personName, epithet, count, canMove, "");
            }
            catch (Exception e) { Fail("open", e); }
        }

        private void Build()
        {
            if (root != null) return;
            root = new GameObject("InyeonPortraitZoom", typeof(RectTransform));
            root.SetActive(false);
            rect = (RectTransform)root.transform;
            rect.SetParent(host, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            canvas = root.AddComponent<Canvas>();
            root.AddComponent<GraphicRaycaster>();
            group = root.AddComponent<CanvasGroup>();
            group.alpha = 0;
            group.blocksRaycasts = false;
            var backdrop = InyeonUiParts.Image("Backdrop", rect, 0, 0, 1, 1, new Color(.025f, .022f, .018f, .98f));
            backdrop.raycastTarget = true;
            var dismiss = backdrop.gameObject.AddComponent<Button>();
            DisableNavigation(dismiss);
            dismiss.transition = Selectable.Transition.None;
            dismiss.onClick.AddListener(() => { if (Time.frameCount > openFrame) Close(); });
            var frame = InyeonUiParts.Frame("Frame", rect, .025f, .035f, .975f, .965f, new Color(.09f, .08f, .06f, 1f));
            name = ui.Text("Name", frame, .2f, .91f, .8f, .995f, "", 37, TextAnchor.MiddleCenter);
            title = ui.Text("Title", frame, .2f, .86f, .8f, .92f, "", 24, TextAnchor.MiddleCenter);
            title.color = InyeonUiParts.Gold;
            InyeonUiParts.Rule(frame, .16f, .84f, .86f, InyeonUiParts.Edge);
            picture = InyeonUiParts.Image("Portrait", frame, .12f, .13f, .88f, .845f, Color.white);
            picture.preserveAspect = true;
            picture.raycastTarget = true; // clicks on the art do not dismiss it
            notice = ui.Text("Notice", frame, .23f, .4f, .77f, .6f, "", 28, TextAnchor.MiddleCenter);
            previous = ui.Button("Previous", frame, .035f, .42f, .09f, .58f, "<", () => Navigate(-1), 46);
            next = ui.Button("Next", frame, .91f, .42f, .965f, .58f, ">", () => Navigate(1), 46);
            DisableNavigation(previous);
            DisableNavigation(next);
            counter = ui.Text("Counter", frame, .35f, .04f, .65f, .115f, "", 28, TextAnchor.MiddleCenter);
            ui.Text("Help", frame, .35f, .005f, .65f, .05f, Loc.T(UiText.ZoomHelp), 17, TextAnchor.MiddleCenter).color = InyeonUiParts.Muted;
            DisableNavigation(ui.Button("Close", frame, .88f, .92f, .98f, .99f, Loc.T(UiText.Close), Close, 24));
        }

        private static void DisableNavigation(Button button)
        {
            // Arrow keys switch art; automatic focus navigation must not reach the UI behind the overlay.
            button.navigation = new Navigation { mode = Navigation.Mode.None };
        }

        public void Sync(Sprite sprite, string personName, string epithet, string count, bool canMove, string message)
        {
            if (!IsOpen) return;
            try
            {
                picture.sprite = sprite;
                picture.enabled = sprite != null;
                name.text = personName;
                title.text = epithet;
                counter.text = count;
                notice.text = message;
                InyeonUiParts.ButtonState(previous, canMove);
                InyeonUiParts.ButtonState(next, canMove);
            }
            catch (Exception e) { Fail("image", e); }
        }
        public void ClearPicture()
        {
            if (picture != null) { picture.sprite = null; picture.enabled = false; }
        }
        private void Navigate(int delta)
        {
            if (!IsOpen) return;
            try { move(delta); } catch (Exception e) { Fail("navigation", e); }
        }
        public void Tick()
        {
            if (!IsOpen || Time.frameCount <= openFrame) return;
            try
            {
                DetailOverlay.FitToScreen(rect, host);
                if (inputUnavailable) return;
                try
                {
                    if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
                    if (Input.GetKeyDown(KeyCode.LeftArrow)) Navigate(-1);
                    else if (Input.GetKeyDown(KeyCode.RightArrow)) Navigate(1);
                }
                catch { inputUnavailable = true; } // visible navigation/close buttons stay usable
            }
            catch (Exception e) { Fail("frame", e); }
        }
        public void Close()
        {
            IsOpen = false;
            try
            {
                if (group != null) { group.alpha = 0; group.blocksRaycasts = false; }
                ClearPicture();
                if (root != null) root.SetActive(false);
            }
            catch { }
            if (locked)
            {
                locked = false;
                try { access.SetLibraryInputClose(library, true); } catch { }
            }
            var selected = savedSelection;
            savedSelection = null;
            try
            {
                if (selected != null && selected.activeInHierarchy) EventSystem.current?.SetSelectedGameObject(selected);
            }
            catch { }
        }
        public void Destroy()
        {
            Close();
            if (root != null) Object.Destroy(root);
            root = null;
        }
        private void Fail(string where, Exception e) { broken = true; Close(); Warn(where + ": " + e.GetType().Name); }
        private void Warn(string message) { try { log?.Warn("inyeon zoom: " + message); } catch { } }
    }
}
