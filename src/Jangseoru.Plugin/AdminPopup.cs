using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Jangseoru.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Jangseoru.Plugin
{
    /// <summary>
    /// F11 while the library (도감) is open: a popup asking whether to change 장서루, with 초기화 (forget every record), 전부 해금
    /// (open every entry) and 취소. Both actions ask once more before doing anything. The popup is our own overlay canvas: a dark
    /// grey veil (80%) over the screen and a black, borderless panel with white brush-font buttons. While it is open the library's
    /// close key is switched off and Esc cancels. Before an action the five record files are copied to
    /// <c>JangseoruMod\backup\&lt;time&gt;</c>; the action then changes the mod's own stores only (맹세 through the mod's layer in
    /// oath.txt; never the game's ending record, game data or saves) and
    /// writes them at once. Every entry point is wrapped; a failure closes the popup and switches F11 off for the session.
    /// </summary>
    internal sealed class AdminPopup
    {
        private enum Step { Ask, ConfirmReset, ConfirmUnlock }

        private static readonly Color Veil = new Color(0.12f, 0.12f, 0.12f, 0.8f);
        private static readonly Color PanelColor = new Color(0f, 0f, 0f, 1f);
        private static readonly Color ButtonColor = Color.white; // tinted black by the button colours below

        private readonly GameAccess access;
        private readonly ILog log;
        private readonly Func<GalleryUi> ui;
        private readonly GalleryFile gallery;
        private readonly InyeonFile inyeon;
        private readonly ItemFile items;
        private readonly MediaFile media;
        private readonly OathFile oath;
        private readonly string persistentDataPath;

        private GameObject root;
        private Text message;
        private readonly List<Button> buttons = new List<Button>();
        private Step step;
        private Component lockedLibrary;
        private int openFrame;
        private bool broken, keysOff;

        public AdminPopup(GameAccess access, ILog log, Func<GalleryUi> ui, GalleryFile gallery, InyeonFile inyeon, ItemFile items,
            MediaFile media, OathFile oath, string persistentDataPath)
        {
            this.access = access;
            this.log = log;
            this.ui = ui;
            this.gallery = gallery;
            this.inyeon = inyeon;
            this.items = items;
            this.media = media;
            this.oath = oath;
            this.persistentDataPath = persistentDataPath;
        }

        public bool IsOpen => root != null;

        /// <summary>Every frame from the plugin: F11 opens (only while the library is open), Esc cancels.</summary>
        public void Tick()
        {
            if (broken || keysOff) return;
            try
            {
                bool f11, esc;
                try
                {
                    f11 = Input.GetKeyDown(KeyCode.F11);
                    esc = Input.GetKeyDown(KeyCode.Escape);
                }
                catch (Exception e)
                {
                    keysOff = true; // legacy input switched off in this build: no hotkey
                    Warn("F11 popup unavailable: keyboard polling failed (" + e.GetType().Name + ")");
                    return;
                }

                if (IsOpen)
                {
                    if (lockedLibrary == null || !lockedLibrary.gameObject.activeInHierarchy) { Close(); return; }
                    if (esc && Time.frameCount > openFrame) Close();
                    return;
                }
                if (!f11) return;
                var gallery = ui();
                var library = gallery?.Library;
                if (library == null || !library.gameObject.activeInHierarchy) return; // only while the 도감 is open
                Open(library);
            }
            catch (Exception e) { Fail("frame", e); }
        }

        private void Open(Component library)
        {
            Build();
            openFrame = Time.frameCount;
            if (!access.SetLibraryInputClose(library, false)) Warn("F11 popup: could not switch off the library close key");
            lockedLibrary = library; // the popup closes with the library
            var es = EventSystem.current;
            if (es != null) es.SetSelectedGameObject(null); // Submit must not reach the card underneath
            Show(Step.Ask);
            Info("F11 popup opened");
        }

        /// <summary>Removes the popup and gives the library its close key back. Idempotent; never throws.</summary>
        public void Close()
        {
            try
            {
                if (lockedLibrary != null) access.SetLibraryInputClose(lockedLibrary, true);
            }
            catch { }
            lockedLibrary = null;
            try { if (root != null) Object.Destroy(root); }
            catch { }
            root = null;
            message = null;
            buttons.Clear();
        }

        private void Fail(string where, Exception e)
        {
            broken = true;
            Warn("F11 popup disabled: " + where + " threw " + e.GetType().Name + ": " + e.Message);
            Close();
        }

        // ================= steps =================

        private void Show(Step next)
        {
            step = next;
            switch (next)
            {
                case Step.Ask:
                    message.text = Loc.T(UiText.AdminQuestion);
                    SetButtons((UiText.AdminReset, () => Show(Step.ConfirmReset)),
                        (UiText.AdminUnlock, () => Show(Step.ConfirmUnlock)),
                        (UiText.AdminCancel, Close));
                    break;
                case Step.ConfirmReset:
                    message.text = Loc.T(UiText.AdminConfirmReset);
                    SetButtons((UiText.AdminReset, () => Run("reset", ResetAll)), (UiText.AdminCancel, Close));
                    break;
                default:
                    message.text = Loc.T(UiText.AdminConfirmUnlock);
                    SetButtons((UiText.AdminUnlock, () => Run("unlock all", UnlockAll)), (UiText.AdminCancel, Close));
                    break;
            }
            foreach (var b in buttons) BrushFont.Apply(b.GetComponentInChildren<Text>());
            BrushFont.Apply(message);
        }

        private void Run(string what, Action action)
        {
            try
            {
                var backup = Backup();
                action();
                Info("F11 " + what + " done" + (backup != null ? " (previous records copied to " + backup + ")" : " (no backup copy)"));
                ui()?.RefreshAfterDataChange();
            }
            catch (Exception e) { Warn("F11 " + what + " failed: " + e.GetType().Name + ": " + e.Message); }
            Close();
        }

        private void ResetAll()
        {
            access.TryReadEndCg(out var endings); // the game's ending counts become the baselines (unread: none)
            var counts = endings?.Select(e => new KeyValuePair<string, int>(e.Id, e.Count)).ToList();
            CollectionAdmin.Reset(gallery?.Store, inyeon?.Store, items?.Store, media?.Store, oath?.Store, counts);
            Save();
        }

        private void UnlockAll()
        {
            var gameItems = new List<(ItemKind, string)>();
            foreach (var kind in new[] { ItemKind.Book, ItemKind.Misc, ItemKind.Special })
            {
                if (!access.TryReadItems(kind, out var list)) continue;
                foreach (var entry in list)
                    if (entry != null && entry.Image != null && !string.IsNullOrEmpty(entry.Id)) gameItems.Add((kind, entry.Id));
            }
            access.TryReadEndCg(out var endings);
            var oathIds = endings?.Select(e => e.Id).ToList();
            CollectionAdmin.UnlockAll(gallery?.Store, inyeon?.Store, items?.Store, media?.Store, gameItems, oath?.Store, oathIds);
            Save();
        }

        /// <summary>Writes the four stores now (a blocked file is skipped by its own rules).</summary>
        private void Save()
        {
            var now = DateTime.UtcNow;
            try { gallery?.MarkDirty(now); gallery?.FlushNow(); } catch { }
            try { inyeon?.MarkDirty(now); inyeon?.FlushNow(); } catch { }
            try { items?.MarkDirty(now); items?.FlushNow(); } catch { }
            try { media?.MarkDirty(now); media?.FlushNow(); } catch { }
            try { oath?.MarkDirty(now); oath?.FlushNow(); } catch { }
        }

        /// <summary>Copies the record files as they are on disk to JangseoruMod\backup\&lt;time&gt;; null when nothing was copied.</summary>
        private string Backup()
        {
            try
            {
                var any = StorePaths.ResolveGallery(persistentDataPath);
                if (any == null) return null;
                var folder = Path.Combine(Path.GetDirectoryName(any), "backup", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                var copied = 0;
                foreach (var path in new[]
                         {
                             StorePaths.Resolve(persistentDataPath), any, StorePaths.ResolveInyeon(persistentDataPath),
                             StorePaths.ResolveItems(persistentDataPath), StorePaths.ResolveMedia(persistentDataPath),
                             StorePaths.ResolveOath(persistentDataPath),
                         })
                {
                    if (path == null || !File.Exists(path)) continue;
                    Directory.CreateDirectory(folder);
                    File.Copy(path, Path.Combine(folder, Path.GetFileName(path)), false);
                    copied++;
                }
                return copied > 0 ? folder : null;
            }
            catch (Exception e)
            {
                Warn("F11 backup copy failed (" + e.GetType().Name + "); continuing");
                return null;
            }
        }

        // ================= build =================

        private void Build()
        {
            Close();
            root = new GameObject("JangseoruAdminPopup", typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000; // above the library and everything of ours
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            // The veil catches every click outside the panel, so nothing underneath reacts.
            var veil = NewImage("Veil", root.transform, Veil);
            Stretch(veil.rectTransform, Vector2.zero, Vector2.one);
            veil.raycastTarget = true;

            var panel = NewImage("Panel", root.transform, PanelColor);
            var prt = panel.rectTransform;
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(820f, 320f);
            panel.raycastTarget = true;

            message = NewText("Message", prt, 40, TextAnchor.MiddleCenter);
            Stretch(message.rectTransform, new Vector2(0.05f, 0.42f), new Vector2(0.95f, 0.95f));
        }

        private void SetButtons(params (string Key, Action Action)[] specs)
        {
            foreach (var b in buttons) if (b != null) Object.Destroy(b.gameObject);
            buttons.Clear();
            var panel = message.rectTransform.parent;
            const float width = 0.26f, gap = 0.04f;
            var total = specs.Length * width + (specs.Length - 1) * gap;
            var x = 0.5f - total / 2f;
            foreach (var spec in specs)
            {
                var image = NewImage("Button", panel, ButtonColor);
                Stretch(image.rectTransform, new Vector2(x, 0.08f), new Vector2(x + width, 0.34f));
                image.raycastTarget = true;
                var button = image.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                var colors = button.colors;
                colors.normalColor = Color.black; // black buttons, no outline
                colors.highlightedColor = new Color(0.22f, 0.22f, 0.22f, 1f); // hover: dark grey
                colors.selectedColor = Color.black;
                colors.pressedColor = new Color(0.32f, 0.32f, 0.32f, 1f);
                button.colors = colors;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                var action = spec.Action;
                button.onClick.AddListener(() =>
                {
                    try { action(); }
                    catch (Exception e) { Fail("button", e); }
                });
                var label = NewText("Label", image.rectTransform, 38, TextAnchor.MiddleCenter);
                Stretch(label.rectTransform, Vector2.zero, Vector2.one);
                label.text = Loc.T(spec.Key);
                buttons.Add(button);
                x += width + gap;
            }
        }

        private static Image NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text NewText(string name, Transform parent, int size, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = BrushFont.Fallback(); // a game font that is loaded; BrushFont.Apply swaps in the brush font where it fits
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private void Info(string m) { try { log.Info(m); } catch { } }

        private void Warn(string m) { try { log.Warn(m); } catch { } }
    }
}
