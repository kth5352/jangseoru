using System;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.Mono;
using HarmonyLib;
using Jangseoru.Core;
using UnityEngine;

namespace Jangseoru.Plugin
{
    internal sealed class BepLog : ILog
    {
        private readonly ManualLogSource source;
        public BepLog(ManualLogSource source) { this.source = source; }
        public void Info(string message) => source.LogInfo("[Jangseoru] " + message);
        public void Warn(string message) => source.LogWarning("[Jangseoru] " + message);
        public void Error(string message) => source.LogError("[Jangseoru] " + message);
    }

    [BepInPlugin(Guid, "Jangseoru Gallery", "1.1.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "jangseoru.legend.mod";

        private StoreFile file;
        private GalleryFile galleryFile;
        private InyeonFile inyeonFile;
        private ItemFile itemFile;
        private MediaFile mediaFile;
        private OathFile oathFile;
        private GalleryUi galleryUi;
        private AdminPopup adminPopup;
        private Harmony harmony;

        private void Awake()
        {
            // Nothing in here may stop the game from starting: any failure just leaves the mod inactive.
            try
            {
                var log = new BepLog(Logger);
                BookSpriteKeeper.SetLog(log);
                BubbleArt.SetLog(log);
                PaperArt.SetLog(log);
                ItemArt.SetLog(log);
                CatalogDiscovery.Install(log);
                var appVersion = Application.version;
                var access = GameAccess.Resolve(log);
                var check = SelfCheck.FromAccess(access, appVersion);
                harmony = new Harmony(Guid);

                // The byeolho and gallery features are independent: a failure in one must not disable the other.
                try
                {
                    file = new StoreFile(StorePaths.Resolve(Application.persistentDataPath), log, TimeSpan.FromSeconds(2));
                    file.Load(DateTime.UtcNow);

                    var stored = file.Store.GameVersion;
                    if (stored != null && stored != appVersion)
                        log.Warn("game version changed (" + stored + " -> " + appVersion + "); re-checking hook targets");
                    if (stored != appVersion)
                    {
                        file.Store.GameVersion = appVersion;
                        file.MarkDirty(DateTime.UtcNow);
                    }

                    Hooks.Install(harmony, file, access, check, log, Application.persistentDataPath);
                }
                catch (Exception e)
                {
                    try { log.Warn("byeolho features inactive: " + e.GetType().Name); } catch { }
                }

                try
                {
                    galleryFile = new GalleryFile(StorePaths.ResolveGallery(Application.persistentDataPath), log, TimeSpan.FromSeconds(2));
                    galleryFile.Load(DateTime.UtcNow);
                    Hooks.InstallGallery(harmony, new GalleryRecorder(galleryFile, log), access, check, log);
                }
                catch (Exception e)
                {
                    try { log.Warn("picture collection inactive: " + e.GetType().Name); } catch { }
                }

                InyeonGameAccess inyeonAccess = null;
                try
                {
                    inyeonFile = new InyeonFile(StorePaths.ResolveInyeon(Application.persistentDataPath), log, TimeSpan.FromSeconds(2));
                    inyeonFile.Load(DateTime.UtcNow);
                    inyeonAccess = InyeonGameAccess.Create(log);
                    InyeonHooks.Install(harmony, inyeonFile, inyeonAccess, log);
                }
                catch (Exception e)
                {
                    try { log.Warn("inyeon collection inactive: " + e.GetType().Name); } catch { }
                }

                // Obtained items: own file, own hooks; a failure here touches nothing else.
                try
                {
                    itemFile = new ItemFile(StorePaths.ResolveItems(Application.persistentDataPath), log, TimeSpan.FromSeconds(2));
                    itemFile.Load(DateTime.UtcNow);
                    Hooks.InstallItems(harmony, new ItemRecorder(itemFile, log), access, check, log);
                }
                catch (Exception e)
                {
                    try { log.Warn("item collection inactive: " + e.GetType().Name); } catch { }
                }

                // Seen backgrounds, big pictures and videos: own file, own hooks and watcher; a failure here touches nothing else.
                try
                {
                    mediaFile = new MediaFile(StorePaths.ResolveMedia(Application.persistentDataPath), log, TimeSpan.FromSeconds(2));
                    mediaFile.Load(DateTime.UtcNow);
                    var mediaRecorder = new MediaRecorder(mediaFile, log);
                    MediaHooks.Install(harmony, mediaRecorder, access, log);
                    BackgroundWatcher.Install(mediaRecorder, log);
                }
                catch (Exception e)
                {
                    try { log.Warn("media collection inactive: " + e.GetType().Name); } catch { }
                }

                // The mod's layer over the game's ending record (맹세 opened/reset by the F11 popup); the game's record is never written.
                try
                {
                    oathFile = new OathFile(StorePaths.ResolveOath(Application.persistentDataPath), log, TimeSpan.FromSeconds(2));
                    oathFile.Load(DateTime.UtcNow);
                }
                catch (Exception e)
                {
                    try { log.Warn("oath layer inactive: " + e.GetType().Name); } catch { }
                    oathFile = null;
                }

                // The library tab only reads stores (null = locked); its failure touches nothing else.
                try
                {
                    var ui = new GalleryUi(galleryFile, itemFile, access, log, inyeonFile, inyeonAccess, mediaFile, oathFile);
                    galleryUi = ui;
                    adminPopup = new AdminPopup(access, log, () => galleryUi, galleryFile, inyeonFile, itemFile, mediaFile, oathFile, Application.persistentDataPath);
                    Hooks.InstallLibraryUi(harmony, ui, access, check, log);
                    check.MarkPictureDetail(ui.PictureDetailCreatable);
                    // Scene-load book capture only once the 사건 detail that uses it is really there.
                    if (check.PictureDetailAvailable) BookSpriteKeeper.InstallSceneCapture();
                }
                catch (Exception e)
                {
                    try { log.Warn("jangseoru tab inactive: " + e.GetType().Name); } catch { }
                }

                check.Report(log);
            }
            catch (Exception e)
            {
                try { Logger.LogWarning("[Jangseoru] initialization failed, mod inactive: " + e.GetType().Name); } catch { }
            }
        }

        private void Update()
        {
            try { file?.FlushIfDue(DateTime.UtcNow); } catch { }
            try { galleryFile?.FlushIfDue(DateTime.UtcNow); } catch { }
            try { inyeonFile?.FlushIfDue(DateTime.UtcNow); } catch { }
            try { itemFile?.FlushIfDue(DateTime.UtcNow); } catch { }
            try { mediaFile?.FlushIfDue(DateTime.UtcNow); } catch { }
            try { oathFile?.FlushIfDue(DateTime.UtcNow); } catch { }
            BackgroundWatcher.Tick();
            CatalogDiscovery.Tick(); // wrapped inside; stops once it has run
            adminPopup?.Tick(); // wrapped inside
        }

        private void OnApplicationQuit()
        {
            try { file?.FlushNow(); } catch { }
            try { galleryFile?.FlushNow(); } catch { }
            try { inyeonFile?.FlushNow(); } catch { }
            try { itemFile?.FlushNow(); } catch { }
            try { mediaFile?.FlushNow(); } catch { }
            try { oathFile?.FlushNow(); } catch { }
        }

        private void OnDestroy()
        {
            try { adminPopup?.Close(); } catch { }
            BookSpriteKeeper.Uninstall();
            BubbleArt.Uninstall();
            PaperArt.Uninstall();
            ItemArt.Uninstall();
            InyeonHooks.Disable();
            MediaHooks.Disable();
            BackgroundWatcher.Disable();
            CatalogDiscovery.Disable();
            try { file?.FlushNow(); } catch { }
            try { galleryFile?.FlushNow(); } catch { }
            try { inyeonFile?.FlushNow(); } catch { }
            try { itemFile?.FlushNow(); } catch { }
            try { mediaFile?.FlushNow(); } catch { }
            try { oathFile?.FlushNow(); } catch { }
        }
    }
}
