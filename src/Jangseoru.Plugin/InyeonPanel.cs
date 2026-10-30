using System;
using System.Collections.Generic;
using System.Linq;
using Jangseoru.Core;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Jangseoru.Plugin
{
    /// <summary>Ink-and-brass character folio: biography, separate portrait/name plate, roster and collected picture strip.</summary>
    internal sealed class InyeonPanel
    {
        private sealed class Tile
        {
            public string Id, Key;
            public RectTransform Rect;
            public Image Picture, Frame;
            public Text Placeholder;
            public object Handle;
            public bool Borrowed, Done, Failed;
        }
        private readonly InyeonFile file;
        private readonly GameAccess access;
        private readonly InyeonGameAccess readers;
        private readonly SpriteLoader loader;
        private readonly ILog log;
        private readonly InyeonUiParts ui;
        private readonly InyeonSelection selection;
        private readonly InyeonBiographySelection biography = new InyeonBiographySelection();
        private readonly InyeonZoom zoom;
        /// <summary>Pixels the panel extends below the card area; the picture row lives there and the progress bar moves down by as much.</summary>
        public const float ExtraBottom = 150f;
        /// <summary>How far the progress bar moves down: less than the extension, so it stays clear of the screen edge.</summary>
        public const float BarShift = 100f;
        /// <summary>Height of the picture row at the panel's bottom (taller than the extension: it reaches into the main area).</summary>
        private const float StripHeight = 140f;
        private const float CounterGap = 44f;
        private const float ExtraWidth = 140f;
        private float baseHeight = 640f;
        private readonly List<Tile> tiles = new List<Tile>();
        private RectTransform root, listContent, bodyContent;
        private ScrollRect listScroll, bodyScroll, stripScroll;
        private RectTransform stripContent;
        private readonly List<Tile> strip = new List<Tile>();
        private Image portrait;
        private Text name, title, pictureCounter, status, bioHeading, body;
        private RectTransform bioSource;
        private Button previousPicture, nextPicture;
        private readonly List<Button> chapters = new List<Button>();
        private readonly List<Button> sources = new List<Button>();
        private float lastWidth = -1, tileHeight, lastStripHeight = -1;
        private object portraitHandle;
        private bool portraitDone, broken;

        public InyeonPanel(InyeonFile file, GameAccess access, InyeonGameAccess readers, SpriteLoader loader, ILog log,
            Transform parent, RectTransform bounds, GameObject textTemplate, Component library, GameObject headingTemplate = null)
        {
            this.file = file;
            this.access = access;
            this.readers = readers;
            this.loader = loader;
            this.log = log;
            selection = new InyeonSelection(file?.Store);
            ui = new InyeonUiParts(access, textTemplate, headingTemplate);
            zoom = new InyeonZoom(access, log, library, parent, ui, delta => Guard(() => MovePicture(delta)));
            try
            {
                root = InyeonUiParts.Rect("Inyeon", parent, 0, 0, 1, 1);
                root.gameObject.SetActive(false);
                root.anchorMin = bounds.anchorMin;
                root.anchorMax = bounds.anchorMax;
                root.pivot = bounds.pivot;
                baseHeight = bounds.sizeDelta.y > 1f ? bounds.sizeDelta.y : baseHeight;
                // Keep the top/right edges aligned with the card area; use the free space toward the left menu.
                root.sizeDelta = new Vector2(bounds.sizeDelta.x + ExtraWidth, bounds.sizeDelta.y + ExtraBottom);
                root.anchoredPosition = new Vector2(bounds.anchoredPosition.x - (1f - bounds.pivot.x) * ExtraWidth,
                    bounds.anchoredPosition.y - (1f - bounds.pivot.y) * ExtraBottom);
                Build();
            }
            catch (Exception e) { Fail("build", e); }
        }

        /// <summary>A height fraction of the card area (0 bottom, 1 top) as a fraction of this taller panel.</summary>
        private float Y(float cardFraction)
        {
            var total = baseHeight + ExtraBottom;
            var floor = StripHeight + CounterGap; // the picture row and the counter line under the main area
            return (floor + cardFraction * (total - floor)) / total;
        }

        private static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        private void Build()
        {
            // No outline, corner marks or fill on the outermost backdrop: the library shows through completely.
            var bioPane = InyeonUiParts.ArtFrame("Biography", root, .014f, Y(.02f), .322f, .98f, WithAlpha(InyeonUiParts.Panel, .85f));
            var artPane = InyeonUiParts.ArtFrame("PortraitPage", root, .334f, Y(.02f), .694f, .98f, new Color(.04f, .04f, .04f, .85f));
            var peoplePane = InyeonUiParts.ArtFrame("Roster", root, .706f, Y(.02f), .986f, .98f, WithAlpha(InyeonUiParts.Panel, .85f));

            // The art stops above the name plate; neither title nor name is drawn over the sprite.
            var artViewport = InyeonUiParts.Rect("ArtViewport", artPane, 0, .19f, 1, .94f); // same size as before, lowered a little now that the hint is gone
            artViewport.gameObject.AddComponent<RectMask2D>();
            // Fills the viewport; preserveAspect fits the whole picture inside it (a tall sprite must not overflow the mask).
            portrait = InyeonUiParts.Image("Standing", artViewport, 0, 0, 1, 1, Color.white);
            portrait.preserveAspect = true;
            portrait.raycastTarget = true;
            portrait.enabled = false;
            var enlarge = portrait.gameObject.AddComponent<Button>();
            enlarge.targetGraphic = portrait;
            enlarge.transition = Selectable.Transition.None;
            enlarge.onClick.AddListener(() => Guard(OpenZoom));
            status = ui.Text("PictureStatus", artPane, .10f, .45f, .9f, .73f, "", 22, TextAnchor.MiddleCenter);
            var plate = InyeonUiParts.Image("NamePlate", artPane, .025f, .02f, .975f, .185f, new Color(.02f, .02f, .02f, .90f)).rectTransform;
            InyeonUiParts.Rule(plate, .07f, .93f, .98f, InyeonUiParts.Gold);
            title = ui.Text("Title", plate, .05f, .57f, .95f, .96f, "", 20, TextAnchor.MiddleCenter);
            title.color = InyeonUiParts.Gold;
            name = ui.Text("Name", plate, .05f, .06f, .95f, .58f, "", 31, TextAnchor.MiddleCenter);
            FitLabel(name, 22, 31);
            FitLabel(title, 15, 20);

            var railHeader = InyeonUiParts.Rect("PicturesHeader", root, .025f, StripHeight / (baseHeight + ExtraBottom), .975f, Y(0));
            // 85% of the former button size, bold arrows, and the counter close to both.
            previousPicture = ui.Button("PreviousPicture", railHeader, .855f, .16f, .885f, .84f, "◀", () => Guard(() => MovePicture(-1)), 20);
            pictureCounter = ui.Text("PictureCounter", railHeader, .89f, 0, .955f, 1, "0/0", 22, TextAnchor.MiddleCenter);
            nextPicture = ui.Button("NextPicture", railHeader, .96f, .16f, .99f, .84f, "▶", () => Guard(() => MovePicture(1)), 20);
            foreach (var arrow in new[] { previousPicture, nextPicture }) arrow.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;
            stripScroll = InyeonUiParts.HorizontalScroll("PictureStrip", root, .025f, .018f, .975f, StripHeight / (baseHeight + ExtraBottom), out stripContent);

            bioHeading = ui.Text("BiographyHeading", bioPane, .07f, .86f, .93f, .98f, Loc.T(UiText.Biography), 31);
            BrushFont.Apply(bioHeading);
            bioHeading.color = InyeonUiParts.Gold;
            InyeonUiParts.Rule(bioPane, .07f, .93f, .86f, InyeonUiParts.Edge);
            for (int stage = 1; stage <= 4; stage++)
            {
                var chapter = stage;
                var x = .07f + (stage - 1) * .225f;
                var button = ui.TextButton("Chapter" + stage, bioPane, x, .74f, x + .19f, .835f,
                    InyeonBiographySelection.Label(stage), () => Guard(() => { biography.SelectStage(chapter); RefreshBiography(); }), 27, true); // 一 二 三 四 in the heading font, like the status window
                BrushFont.Apply(button.GetComponentInChildren<Text>(), true);
                chapters.Add(button);
            }
            bioSource = InyeonUiParts.Rect("BiographySource", bioPane, .07f, .66f, .93f, .73f);
            bodyScroll = InyeonUiParts.Scroll("BiographyScroll", bioPane, .07f, .045f, .94f, .70f, out bodyContent);
            body = ui.Text("Body", bodyContent, 0, 0, 1, 1, "", 18, TextAnchor.UpperLeft);
            body.lineSpacing = 1.28f;
            body.verticalOverflow = VerticalWrapMode.Overflow;
            var peopleHeading = ui.Text("PeopleHeading", peoplePane, .07f, .87f, .93f, .98f, Loc.T(UiText.People), 28);
            peopleHeading.color = InyeonUiParts.Gold;
            BrushFont.Apply(peopleHeading);
            InyeonUiParts.Rule(peoplePane, .07f, .93f, .87f, InyeonUiParts.Edge);
            listScroll = InyeonUiParts.Scroll("PeopleScroll", peoplePane, .05f, .035f, .95f, .84f, out listContent);
        }

        private static void FitLabel(Text label, int min, int max)
        {
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = min;
            label.resizeTextMaxSize = max;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
        }

        public void Show()
        {
            if (broken || root == null) return;
            Guard(() =>
            {
                Hide();
                foreach (var tile in tiles)
                    if (tile.Rect != null) { tile.Rect.gameObject.SetActive(false); Object.Destroy(tile.Rect.gameObject); }
                tiles.Clear();
                root.gameObject.SetActive(true);
                var heads = readers?.HeadIcons() ?? new Dictionary<string, Sprite>();
                foreach (var person in InyeonCatalog.Characters) AddTile(person, heads);
                listContent.anchoredPosition = Vector2.zero;
                listScroll.StopMovement();
                lastWidth = -1;
                Canvas.ForceUpdateCanvases();
                LayoutTiles();
                var id = selection.CharacterId;
                if (id == null || !(file?.Store.IsKnown(id) ?? false))
                    id = InyeonCatalog.Characters.FirstOrDefault(c => file?.Store.IsKnown(c.Id) ?? false)?.Id;
                Select(id);
                Tick();
            });
        }

        private void AddTile(CharacterDefinition person, Dictionary<string, Sprite> heads)
        {
            bool known = file?.Store.IsKnown(person.Id) ?? false;
            var frame = InyeonUiParts.Image("Person_" + person.Id, listContent, 0, 1, 0, 1, PlainFrame);
            frame.raycastTarget = true;
            var back = InyeonUiParts.Image("Back", frame.transform, .025f, .012f, .975f, .988f, new Color(.03f, .03f, .03f, 1f));
            var picture = InyeonUiParts.Image("Portrait", back.transform, .025f, .23f, .975f, .98f, Color.white);
            picture.preserveAspect = true;
            picture.enabled = false;
            var placeholder = ui.Text("Unknown", back.transform, 0, .23f, 1, 1, known ? "…" : "?", 25, TextAnchor.MiddleCenter);
            placeholder.color = InyeonUiParts.Muted;
            // Best fit in a one-line-high rect: a long name shrinks to one line instead of wrapping to two.
            InyeonUiParts.Rule(back.transform, .07f, .93f, .23f, InyeonUiParts.Edge);
            var label = ui.Text("Name", back.transform, .04f, .01f, .96f, .23f, known ? PersonName(person) : "???", 17, TextAnchor.MiddleCenter);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 9;
            label.resizeTextMaxSize = 17;
            var tile = new Tile { Id = person.Id, Rect = frame.rectTransform, Picture = picture, Frame = frame, Placeholder = placeholder };
            // Every person shows their first collected (normal) picture; the game's small head icon is only a stand-in for a person
            // who has no collected picture at all, so the roster looks the same for everyone.
            if (known) tile.Key = file.Store.Pictures(person.Id).FirstOrDefault();
            if (known && tile.Key == null && heads.TryGetValue(person.Id, out var sprite))
            {
                tile.Borrowed = true;
                picture.sprite = sprite;
                picture.enabled = true;
                placeholder.text = "";
            }
            var button = frame.gameObject.AddComponent<Button>();
            button.targetGraphic = frame;
            button.transition = Selectable.Transition.None;
            button.interactable = known;
            button.onClick.AddListener(() => Guard(() => Select(person.Id)));
            tiles.Add(tile);
        }

        private void LayoutTiles()
        {
            float width = listContent.rect.width;
            if (width <= 0 || Math.Abs(width - lastWidth) < .1f) return;
            lastWidth = width;
            float cellWidth = (width - 12f) / 3f;
            tileHeight = cellWidth * 1.9f;
            for (int i = 0; i < tiles.Count; i++)
            {
                var rt = tiles[i].Rect;
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0, 1);
                rt.sizeDelta = new Vector2(cellWidth, tileHeight - 6f);
                rt.anchoredPosition = new Vector2((i % 3) * (cellWidth + 6f), -(i / 3) * tileHeight);
            }
            listContent.sizeDelta = new Vector2(0, ((tiles.Count + 2) / 3) * tileHeight);
            SizeBody();
        }

        private void Select(string id)
        {
            selection.Select(id);
            var person = InyeonCatalog.Find(id);
            name.text = person == null ? Loc.T(UiText.ChoosePerson) : PersonName(person);
            title.text = person == null ? "" : InyeonTitleText.FillPlayerTitle(Localized("CharacterTitle/" + person.NameId), PlayerTitle());
            foreach (var tile in tiles) tile.Frame.color = tile.Id == id ? InyeonUiParts.Gold : PlainFrame;
            BuildStrip();
            biography.Reset(InyeonBiography.Pages(id, file?.Store, Localized));
            BuildBiographySources();
            RefreshBiography();
            RefreshPicture();
        }
        private string PersonName(CharacterDefinition person)
        {
            var text = Localized("Character/" + person.NameId);
            return string.IsNullOrWhiteSpace(text) ? Loc.T(UiText.UnknownName) : text;
        }
        /// <summary>The player's current 별호 text for the "{title}" epithet line; empty when it cannot be read.</summary>
        private string PlayerTitle()
            => access.TryReadCurrentTitle(out var n) && n >= 0 ? Localized("PlayerInfo/Title/" + n) : "";
        private string Localized(string key)
            => access.TryGetLocalizedString(key, out var text) && !string.IsNullOrWhiteSpace(text) && text != key ? text : "";

        private static readonly Color PlainFrame = InyeonUiParts.Edge;

        private void BuildStrip()
        {
            foreach (var t in strip)
            {
                ReleaseTile(t);
                if (t.Rect != null) { t.Rect.gameObject.SetActive(false); Object.Destroy(t.Rect.gameObject); }
            }
            strip.Clear();
            var keys = selection.Keys;
            for (var i = 0; i < keys.Count; i++)
            {
                var index = i;
                var frame = InyeonUiParts.Image("Pic_" + i, stripContent, 0, 0, 0, 0, PlainFrame);
                frame.raycastTarget = true;
                var back = InyeonUiParts.Image("Back", frame.transform, .018f, .012f, .982f, .988f, InyeonUiParts.Panel);
                var picture = InyeonUiParts.Image("Picture", back.transform, .04f, .04f, .96f, .96f, Color.white);
                picture.preserveAspect = true;
                picture.enabled = false;
                var placeholder = ui.Text("Loading", frame.transform, 0, 0, 1, 1, "…", 24, TextAnchor.MiddleCenter);
                var button = frame.gameObject.AddComponent<Button>();
                button.targetGraphic = frame;
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => Guard(() => { selection.SelectIndex(index); RefreshPicture(); }));
                strip.Add(new Tile { Id = selection.CharacterId, Key = keys[i], Rect = frame.rectTransform, Picture = picture, Frame = frame, Placeholder = placeholder });
            }
            stripContent.anchoredPosition = Vector2.zero;
            stripScroll.StopMovement();
            lastStripHeight = -1;
            LayoutStrip();
        }

        private void LayoutStrip()
        {
            var height = stripScroll.viewport.rect.height;
            if (height <= 0 || Math.Abs(height - lastStripHeight) < .1f) return;
            lastStripHeight = height;
            var cellHeight = height - 8f;
            var x = 4f;
            for (var i = 0; i < strip.Count; i++)
            {
                var width = cellHeight * .66f; // every cell the same size; the picture fits inside it
                var rt = strip[i].Rect;
                rt.anchorMin = rt.anchorMax = new Vector2(0, .5f);
                rt.pivot = new Vector2(0, .5f);
                rt.sizeDelta = new Vector2(width, cellHeight);
                rt.anchoredPosition = new Vector2(x, 0);
                x += width + 6f;
            }
            stripContent.sizeDelta = new Vector2(x + 2f, 0);
        }

        private void HighlightStrip()
        {
            for (var i = 0; i < strip.Count; i++)
                if (strip[i].Frame != null) strip[i].Frame.color = i == selection.Index ? InyeonUiParts.Gold : PlainFrame;
        }

        private void LoadTile(Tile tile, bool near)
        {
            if (tile.Borrowed || tile.Key == null) return;
            if (!near) { ReleaseTile(tile); return; }
            if (tile.Handle == null && !tile.Failed)
            {
                tile.Handle = loader.Load(tile.Key, "inyeon thumbnail");
                tile.Failed = tile.Handle == null;
            }
            if (tile.Handle == null || tile.Done || !loader.Poll(tile.Handle, out var picture)) return;
            tile.Done = true;
            tile.Picture.sprite = picture;
            tile.Picture.enabled = picture != null;
            tile.Placeholder.text = picture == null ? "…" : "";
        }

        private void RefreshPicture()
        {
            zoom.ClearPicture(); // drop the borrowed sprite before its handle is released
            ReleasePortrait();
            pictureCounter.text = selection.Counter;
            InyeonUiParts.ButtonState(previousPicture, selection.CanMove);
            InyeonUiParts.ButtonState(nextPicture, selection.CanMove);
            HighlightStrip();
            status.text = selection.CurrentKey == null ? Loc.T(UiText.NoPictures) : Loc.T(UiText.Loading);
            if (selection.CurrentKey != null) portraitHandle = loader?.Load(selection.CurrentKey, "inyeon portrait");
            if (selection.CurrentKey != null && portraitHandle == null) status.text = Loc.T(UiText.PictureFailed);
            zoom.Sync(null, name.text, title.text, selection.Counter, selection.CanMove, status.text);
        }
        private void MovePicture(int delta)
        {
            if (!selection.CanMove) return;
            selection.Move(delta);
            RefreshPicture();
            // Keep the selected thumbnail visible when using arrows, including wrap-around.
            if (selection.Index < strip.Count)
            {
                var rt = strip[selection.Index].Rect;
                var max = Math.Max(0, stripContent.rect.width - stripScroll.viewport.rect.width);
                stripScroll.StopMovement();
                stripContent.anchoredPosition = new Vector2(-Math.Min(max, Math.Max(0, rt.anchoredPosition.x - stripScroll.viewport.rect.width / 2f + rt.rect.width / 2f)), 0);
            }
        }
        private void OpenZoom()
        {
            if (portrait == null || portrait.sprite == null) return;
            zoom.Open(portrait.sprite, name.text, title.text, selection.Counter, selection.CanMove);
        }
        public bool ZoomOpen => zoom.IsOpen;
        private void BuildBiographySources()
        {
            foreach (var button in sources)
            {
                button.gameObject.SetActive(false);
                Object.Destroy(button.gameObject);
            }
            sources.Clear();
            if (biography.SourceCount <= 1) return;
            for (var i = 0; i < biography.SourceCount; i++)
            {
                var index = i;
                var width = 1f / biography.SourceCount;
                sources.Add(ui.TextButton("Source" + i, bioSource, i * width, 0, (i + 1) * width, 1,
                    Loc.Format(UiText.Record, i + 1), () => Guard(() =>
                    {
                        biography.MoveSource(index - biography.SourceIndex);
                        RefreshBiography();
                    }), 17));
            }
        }
        private void RefreshBiography()
        {
            var page = biography.Current;
            bioHeading.text = Loc.T(UiText.Biography);
            var visibleChapters = 0;
            for (var i = 0; i < chapters.Count; i++)
            {
                var available = biography.HasStage(i + 1);
                chapters[i].gameObject.SetActive(available);
                if (!available) continue;
                var x = .07f + visibleChapters++ * .225f;
                InyeonUiParts.Place((RectTransform)chapters[i].transform, x, .74f, x + .19f, .835f);
                InyeonUiParts.ButtonState(chapters[i], true, page?.Stage == i + 1);
            }
            bioSource.gameObject.SetActive(biography.SourceCount > 1);
            for (var i = 0; i < sources.Count; i++)
                InyeonUiParts.ButtonState(sources[i], true, biography.SourceIndex == i);
            var bodyRect = (RectTransform)bodyScroll.transform;
            bodyRect.anchorMax = new Vector2(bodyRect.anchorMax.x, visibleChapters == 0 ? .82f : biography.SourceCount > 1 ? .64f : .70f);
            body.text = page?.Text ?? (selection.CharacterId == null ? Loc.T(UiText.ChoosePersonHint)
                : (InyeonCatalog.Find(selection.CharacterId)?.BiographyIds.Count ?? 0) == 0
                    ? Loc.T(UiText.NoBiography) : Loc.T(UiText.NoBiographyYet));
            bodyScroll.StopMovement();
            bodyContent.anchoredPosition = Vector2.zero;
            SizeBody();
        }
        private void SizeBody()
        {
            if (body == null || bodyContent == null) return;
            bodyContent.sizeDelta = new Vector2(0, Math.Max(bodyScroll.viewport.rect.height, body.preferredHeight + 16f));
        }

        public void Tick()
        {
            if (broken || root == null || !root.gameObject.activeInHierarchy) return;
            Guard(() =>
            {
                zoom.Tick();
                LayoutTiles();
                if (portraitHandle != null && !portraitDone && loader.Poll(portraitHandle, out var sprite))
                {
                    portraitDone = true;
                    portrait.sprite = sprite;
                    portrait.enabled = sprite != null;
                    status.text = sprite != null ? "" : Loc.T(UiText.PictureFailed);
                    zoom.Sync(sprite, name.text, title.text, selection.Counter, selection.CanMove, status.text);
                }
                if (loader == null) return;
                LayoutStrip();
                // Cells near the view (half a view either side) hold a loaded picture; the others let it go.
                var viewWidth = stripScroll.viewport.rect.width;
                var from = -stripContent.anchoredPosition.x - viewWidth / 2f;
                var to = -stripContent.anchoredPosition.x + viewWidth * 1.5f;
                for (int i = 0; i < strip.Count; i++)
                {
                    var left = strip[i].Rect.anchoredPosition.x;
                    LoadTile(strip[i], left + strip[i].Rect.sizeDelta.x >= from && left <= to);
                }
                if (tileHeight <= 0) return;
                GalleryLayout.ThumbnailWindow(tiles.Count, 3, tileHeight, listScroll.viewport.rect.height,
                    Math.Max(0, listContent.anchoredPosition.y), 1, out var first, out var last);
                for (int i = 0; i < tiles.Count; i++) LoadTile(tiles[i], i >= first && i <= last);
            });
        }
        private void ReleasePortrait()
        {
            var handle = portraitHandle;
            portraitHandle = null;
            portraitDone = false;
            if (portrait != null) { portrait.sprite = null; portrait.enabled = false; }
            loader?.Release(handle);
        }
        private void ReleaseTile(Tile tile)
        {
            if (tile.Borrowed) return;
            var handle = tile.Handle;
            tile.Handle = null;
            tile.Done = false;
            if (tile.Picture != null) { tile.Picture.sprite = null; tile.Picture.enabled = false; }
            if (tile.Placeholder != null && tile.Key != null) tile.Placeholder.text = "…";
            loader?.Release(handle);
        }
        public void Hide()
        {
            zoom.Close();
            ReleasePortrait();
            foreach (var tile in tiles) ReleaseTile(tile);
            foreach (var tile in strip) ReleaseTile(tile);
            if (root != null) root.gameObject.SetActive(false);
        }
        public void Destroy()
        {
            Hide();
            zoom.Destroy();
            if (root != null) Object.Destroy(root.gameObject);
            root = null;
            tiles.Clear();
            strip.Clear();
        }
        private void Guard(Action action)
        {
            if (broken) return;
            try { action(); } catch (Exception e) { Fail("view", e); }
        }
        private void Fail(string where, Exception error)
        {
            broken = true;
            try { Hide(); } catch { }
            try { log?.Warn("inyeon " + where + " disabled: " + error.GetType().Name + ": " + error.Message); } catch { }
        }
    }
}
