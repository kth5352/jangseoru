using System;
using Jangseoru.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;
using Object = UnityEngine.Object;

namespace Jangseoru.Plugin
{
    /// <summary>
    /// The full-screen view of a background, a big picture or a video, over our library panel. A picture is loaded with Addressables
    /// through the shared <see cref="SpriteLoader"/>; a video is loaded as a clip through <see cref="ClipLoader"/> and played, muted
    /// and looping, by a VideoPlayer of ours into a RenderTexture of ours. A left click closes through the backdrop (which consumes it);
    /// a right click and Esc are polled. Everything made here (player, render texture) and
    /// every Addressables handle is released exactly once when the view closes for any reason. A left click, a right click or Esc
    /// closes it (the click that opened it never does); while it is open the library's close key binding is switched off. Per-frame
    /// work runs from <see cref="Tick"/> (our panel driver's Update). Every entry point is wrapped; a failure closes the view,
    /// releases everything and switches the view off for the session. Game data is never changed.
    /// </summary>
    internal sealed class MediaPresenter
    {
        private static readonly Color Backdrop = new Color(0f, 0f, 0f, 0.97f);

        private readonly GameAccess access;
        private readonly ILog log;
        private readonly SpriteLoader sprites;
        private readonly ClipLoader clips;
        private readonly Component library;
        private readonly Transform host;
        private readonly GameObject textSource;

        private GameObject overlay, playerGo;
        private RectTransform overlayRt;
        private Canvas canvas;
        private CanvasGroup group;
        private Image picture;
        private RawImage video;
        private AspectRatioFitter videoFit;
        private Text notice;

        private object spriteHandle, clipHandle; // released exactly once
        private VideoPlayer player;
        private RenderTexture target;
        private bool applied, locked, broken, keysOff;
        private string openKey;
        private int openFrame;
        private GameObject savedSelection;

        public MediaPresenter(GameAccess access, ILog log, SpriteLoader sprites, ClipLoader clips, Component library, Transform host, GameObject textSource)
        {
            this.access = access;
            this.log = log;
            this.sprites = sprites;
            this.clips = clips;
            this.library = library;
            this.host = host;
            this.textSource = textSource;
        }

        public bool IsOpen { get; private set; }

        /// <summary>Opens the view for an unlocked row. Ignored while already open.</summary>
        public void Open(MediaRow row)
        {
            try
            {
                if (IsOpen || broken || row == null) return;
                if (host == null || library == null || !host.gameObject.activeInHierarchy)
                {
                    Info("media view " + row.Key + ": not opened (library tab not showing)");
                    return;
                }
                var isVideo = row.Kind == MediaKind.Video;
                if (isVideo ? clips == null : sprites == null)
                {
                    Info("media view " + row.Key + ": not opened (loader unavailable)");
                    return;
                }

                EnsureBuilt();
                IsOpen = true;
                openKey = row.Key;
                openFrame = Time.frameCount;
                applied = false;

                locked = access.SetLibraryInputClose(library, false);
                if (!locked) Warn("media view " + row.Key + ": could not switch off the library close key");
                var es = EventSystem.current;
                savedSelection = es != null ? es.currentSelectedGameObject : null;
                if (es != null) es.SetSelectedGameObject(null);

                group.alpha = 1f;
                group.blocksRaycasts = true;
                overlay.SetActive(true);
                DetailOverlay.FitToScreen(overlayRt, host);
                DetailOverlay.SortAboveLibrary(canvas, library, host);

                picture.sprite = null;
                picture.enabled = false;
                video.texture = null;
                video.enabled = false;
                notice.gameObject.SetActive(false);

                if (isVideo)
                {
                    clipHandle = clips.Load(row.Address, "media clip");
                    if (clipHandle == null) ShowFailed("the load could not start");
                }
                else
                {
                    spriteHandle = sprites.Load(row.Address, "media image");
                    if (spriteHandle == null) ShowFailed("the load could not start");
                }
                Info("media view " + row.Key + ": opened");
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
                if (spriteHandle != null && !applied && sprites.Poll(spriteHandle, out var sprite))
                {
                    applied = true;
                    if (sprite != null)
                    {
                        picture.sprite = sprite; // the handle's result; cleared before the handle is released
                        picture.enabled = true;
                    }
                    else ShowFailed("the load did not succeed");
                }
                if (clipHandle != null && !applied && clips.Poll(clipHandle, out var clip))
                {
                    applied = true;
                    if (clip != null) StartVideo(clip);
                    else ShowFailed("the load did not succeed");
                }
                if (Time.frameCount > openFrame && CloseRequested()) Finish("closed");
            }
            catch (Exception e) { Fail("frame", e); }
        }

        private void OnOverlayClick()
        {
            try { if (IsOpen && Time.frameCount > openFrame) Finish("closed"); }
            catch (Exception e) { Fail("click", e); }
        }

        private bool CloseRequested()
        {
            if (keysOff) return false;
            try
            {
                // Left clicks close through the backdrop click handler only (see MediaViewInput).
                return MediaViewInput.ClosesOnPolledInput(Input.GetKeyDown(KeyCode.Escape), false, Input.GetMouseButtonDown(1));
            }
            catch (Exception e)
            {
                keysOff = true; // legacy input switched off in this build: the overlay's own click handler still closes
                Warn("media view: keyboard/mouse polling unavailable (" + e.GetType().Name + "); click to close");
                return false;
            }
        }

        private void StartVideo(VideoClip clip)
        {
            var width = (int)Math.Max(2u, Math.Min(clip.width, 4096u));
            var height = (int)Math.Max(2u, Math.Min(clip.height, 4096u));
            target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) { name = "jangseoru_media_video" };
            playerGo = new GameObject("MediaVideoPlayer");
            playerGo.transform.SetParent(overlayRt, false);
            player = playerGo.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = target;
            player.audioOutputMode = VideoAudioOutputMode.None; // muted: the game's own sound cues are not part of the file
            player.isLooping = true;
            player.clip = clip;
            video.texture = target;
            video.enabled = true;
            videoFit.aspectRatio = width / (float)height;
            player.Play();
        }

        private void ShowFailed(string why)
        {
            applied = true;
            Warn("media view " + openKey + ": could not be shown (" + why + ")");
            picture.sprite = null;
            picture.enabled = false;
            video.enabled = false;
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
                // Invisible and click-through first, so a refused SetActive can never leave an invisible overlay blocking the library.
                if (group != null)
                {
                    group.alpha = 0f;
                    group.blocksRaycasts = false;
                }
            }
            catch { }

            // Video first: stop, drop the clip and the texture, destroy what we made.
            try
            {
                if (player != null)
                {
                    player.Stop();
                    player.clip = null;
                    player.targetTexture = null;
                }
                if (video != null) { video.texture = null; video.enabled = false; }
                if (target != null) { target.Release(); Object.Destroy(target); }
                if (playerGo != null) Object.Destroy(playerGo);
            }
            catch { }
            player = null;
            target = null;
            playerGo = null;

            try
            {
                if (picture != null)
                {
                    picture.sprite = null; // no reference to the result survives its release
                    picture.enabled = false;
                }
                if (overlay != null) overlay.SetActive(false);
            }
            catch { }

            // Exactly once: the fields are cleared before the calls, so no path can release them again.
            var sh = spriteHandle;
            var ch = clipHandle;
            spriteHandle = clipHandle = null;
            applied = false;
            try { sprites?.Release(sh); } catch { }
            try { clips?.Release(ch); } catch { }

            if (locked)
            {
                locked = false;
                try
                {
                    if (!access.SetLibraryInputClose(library, true)) Warn("media view " + key + ": could not switch the library close key back on");
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

            Info("media view " + key + ": " + reason + " (live handles " + ((sprites?.Live ?? 0) + (clips?.Live ?? 0)) + ")");
            openKey = null;
        }

        private void Fail(string where, Exception e)
        {
            broken = true;
            Warn("media view disabled: " + where + " threw " + e.GetType().Name + ": " + e.Message);
            Finish("error");
            try { if (overlay != null) overlay.SetActive(false); } catch { }
        }

        // ================= build =================

        private void EnsureBuilt()
        {
            if (overlay != null) return;
            if (host == null || textSource == null) throw new InvalidOperationException("host or text template missing");

            overlay = new GameObject("MediaView", typeof(RectTransform));
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
            dimRt.sizeDelta = new Vector2(64f, 64f); // a little past the edges
            var dim = dimGo.GetComponent<Image>();
            dim.color = Backdrop;
            dim.raycastTarget = true;
            dimGo.AddComponent<OathOverlayClick>().OnClick = OnOverlayClick;

            var picGo = new GameObject("Picture", typeof(RectTransform), typeof(Image));
            var picRt = (RectTransform)picGo.transform;
            picRt.SetParent(overlayRt, false);
            DetailOverlay.Stretch(picRt, Vector2.zero, Vector2.one);
            picture = picGo.GetComponent<Image>();
            picture.preserveAspect = true;
            picture.raycastTarget = false;
            picture.enabled = false;

            var vidGo = new GameObject("Video", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            var vidRt = (RectTransform)vidGo.transform;
            vidRt.SetParent(overlayRt, false);
            DetailOverlay.Stretch(vidRt, Vector2.zero, Vector2.one);
            video = vidGo.GetComponent<RawImage>();
            video.raycastTarget = false;
            video.enabled = false;
            videoFit = vidGo.GetComponent<AspectRatioFitter>();
            videoFit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

            notice = DetailOverlay.CloneText(access, textSource, overlayRt, "Notice", 36, TextAnchor.MiddleCenter, Color.white, false);
            notice.text = Loc.T(UiText.LoadFailed);
            notice.gameObject.SetActive(false);
        }

        private void Info(string m) { try { log.Info(m); } catch { } }

        private void Warn(string m) { try { log.Warn(m); } catch { } }
    }
}
