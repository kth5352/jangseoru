using System;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using Jangseoru.Core;

namespace Jangseoru.Plugin
{
    /// <summary>
    /// Postfix-only, read-only observers of the game's own events. Every body is wrapped so an exception can
    /// never reach the game: a failure just switches off the affected part for this session.
    /// </summary>
    internal static class Hooks
    {
        private static GameAccess access;
        private static CollectionSession session;
        private static ILog log;
        private static string persistentPath;
        private static readonly OriginTable Origins = new OriginTable();

        private static volatile bool titlesOn;
        private static volatile bool perSaveOn;
        private static volatile bool galleryOn;
        private static volatile bool bubbleOn;
        private static volatile bool combatOn;
        private static volatile bool cutInOn;
        private static volatile bool itemsOn;
        private static ItemRecorder itemRecorder;
        private static GameAccess accessRef;
        private static GalleryRecorder recorder;
        private static volatile bool libraryUiOn;
        private static GalleryUi galleryUi;
        private static int disabledOnce;

        public static void Install(Harmony harmony, StoreFile file, GameAccess gameAccess, SelfCheck check, ILog logger, string persistentDataPath)
        {
            access = gameAccess;
            log = logger;
            persistentPath = persistentDataPath;
            session = new CollectionSession(file, logger);

            check.MarkPatched(SelfCheck.FSetPlayer, Patch(harmony, access.SetPlayer, nameof(SetPlayerPostfix), "title collection"));
            check.MarkPatched(SelfCheck.FResetData, Patch(harmony, access.ResetData, nameof(ResetDataPostfix), "per-save: player reset"));
            check.MarkPatched(SelfCheck.FSaveExec, Patch(harmony, access.ExecuteSaveData, nameof(ExecuteSaveDataPostfix), "per-save: save observation"));
            check.MarkPatched(SelfCheck.FGetSave, Patch(harmony, access.GetSaveData, nameof(GetSaveDataPostfix), "per-save: manual save origin"));
            check.MarkPatched(SelfCheck.FGetAutoSave, Patch(harmony, access.GetAutoSaveData, nameof(GetAutoSaveDataPostfix), "per-save: auto save origin"));
            check.MarkPatched(SelfCheck.FExecuteLoad, Patch(harmony, access.ExecuteLoadGameData, nameof(ExecuteLoadGameDataPostfix), "per-save: load applied"));

            // Flags are only raised after every install attempt, so a half-installed set stays inert.
            perSaveOn = check.PerSaveAvailable;
            titlesOn = check.TitleCollectionAvailable;
            session.Configure(perSaveOn);
        }

        /// <summary>
        /// Event-illustration collection, installed independently of <see cref="Install"/>: neither can disable the other.
        /// </summary>
        public static void InstallGallery(Harmony harmony, GalleryRecorder galleryRecorder, GameAccess gameAccess, SelfCheck check, ILog logger)
        {
            if (log == null) log = logger;
            recorder = galleryRecorder;
            check.MarkPatched(SelfCheck.FShowPicture, Patch(harmony, gameAccess.ShowPicture, nameof(ShowPicturePostfix), "picture collection"));
            galleryOn = recorder != null && check.GalleryCollectionAvailable;

            // Bubble presentations are story effects, not pictures: their own hook, switched off on its own failure.
            check.MarkPatched(SelfCheck.FSetupEffect, Patch(harmony, gameAccess.SetupEffect, nameof(SetupEffectPostfix), "bubble collection"));
            bubbleOn = recorder != null && check.BubbleCollectionAvailable;

            // Combat event layers and the story's cut in: two more collection hooks, each switched off on its own failure.
            accessRef = gameAccess;
            check.MarkPatched(SelfCheck.FCombatOverlay, Patch(harmony, gameAccess.CombatOverlayShow, nameof(CombatOverlayPostfix), "combat event collection"));
            combatOn = recorder != null && check.CombatCollectionAvailable;
            check.MarkPatched(SelfCheck.FPortraitLoad, Patch(harmony, gameAccess.LoadPortraitSync, nameof(PortraitPostfix), "cut in collection"));
            cutInOn = recorder != null && check.CutInCollectionAvailable;
        }

        /// <summary>
        /// Obtained-item collection (the 잡물/서적/보물 tabs), independent of every other feature: Postfix on the two methods
        /// through which an item's owned count changes. Both must be in, or none of it runs.
        /// </summary>
        public static void InstallItems(Harmony harmony, ItemRecorder recorderForItems, GameAccess gameAccess, SelfCheck check, ILog logger)
        {
            if (log == null) log = logger;
            accessRef = gameAccess;
            itemRecorder = recorderForItems;
            var add = Patch(harmony, gameAccess.ItemAddCount, nameof(ItemCountPostfix), "item collection (AddCount)");
            var set = Patch(harmony, gameAccess.ItemSetCount, nameof(ItemCountPostfix), "item collection (SetCount)");
            check.MarkPatched(SelfCheck.FItemHooks, add && set);
            itemsOn = itemRecorder != null && check.ItemCollectionAvailable;
        }

        /// <summary>
        /// The Jangseoru library tab, independent of the other features. The four tab hooks (which hide our panel) go in
        /// first; the open hook that builds the UI is only installed when all of them are in, so the tab can always hide.
        /// </summary>
        public static void InstallLibraryUi(Harmony harmony, GalleryUi ui, GameAccess gameAccess, SelfCheck check, ILog logger)
        {
            if (log == null) log = logger;
            galleryUi = ui;
            if (ui == null || !check.LibraryUiResolvable)
            {
                log.Warn("feature 'jangseoru tab' disabled: library members not found");
                return;
            }

            var tabs = Patch(harmony, gameAccess.LibraryOpenEndPanel, nameof(LibraryTabPostfix), "jangseoru tab: hide on other tab")
                       && Patch(harmony, gameAccess.LibraryOpenDeadPanel, nameof(LibraryTabPostfix), "jangseoru tab: hide on other tab")
                       && Patch(harmony, gameAccess.LibraryOpenAchivementPanel, nameof(LibraryTabPostfix), "jangseoru tab: hide on other tab")
                       && Patch(harmony, gameAccess.LibraryOpenLegendPanel, nameof(LibraryTabPostfix), "jangseoru tab: hide on other tab");
            check.MarkPatched(SelfCheck.FLibraryTabs, tabs);
            if (!tabs) return;

            check.MarkPatched(SelfCheck.FLibraryClose,
                Patch(harmony, gameAccess.LibraryCloseButtonClick, nameof(LibraryClosePostfix), "jangseoru tab: release on close"));

            libraryUiOn = true; // raised before the open hook exists, so it can never run with the flag down
            var open = Patch(harmony, gameAccess.LibraryOnPanelOpen, nameof(LibraryOpenPostfix), "jangseoru tab");
            check.MarkPatched(SelfCheck.FLibraryOpen, open);
            libraryUiOn = open && check.LibraryUiAvailable;
        }

        private static bool Patch(Harmony harmony, MethodBase target, string postfixName, string feature)
        {
            var postfix = typeof(Hooks).GetMethod(postfixName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            return SafePatcher.TryPostfix(harmony, target, postfix, feature, log);
        }

        // ---- postfixes (parameter names must match the game's own parameter names) ----

        private static void SetPlayerPostfix(string name, int value)
        {
            try
            {
                if (!titlesOn || name != "title" || value < 0 || value > ByeolhoStore.MaxByeolhoId) return;
                if (!access.TryReadCurrentTitle(out var actual)) return;
                if (!HookLogic.ShouldCollectOnSetPlayer(name, value, actual)) return;
                session.ObserveTitle(actual, DateTime.UtcNow);
            }
            catch (Exception e) { FailTitles("SetPlayer", e); }
        }

        // ShowPicture is an iterator: this runs when the enumerator object is created, which is enough to know the key.
        // It also means a Story scene (whose event presentation uses the open-book sprite) is alive: hold that sprite by
        // reference for the library's 사건 detail (only once that detail is installed). Cheap after the first capture; scans are
        // throttled inside and never throw.
        private static void ShowPicturePostfix(string key)
        {
            try { if (BookSpriteKeeper.CaptureInstalled) BookSpriteKeeper.TryCapture(); }
            catch (Exception) { }

            try
            {
                if (!HookLogic.ShouldRecordPicture(galleryOn, key)) return;
                recorder.OnPicture(key, DateTime.UtcNow);
            }
            catch (Exception e) { FailGallery(e); }
        }

        // StoryEffectsController.SetupEffect(name, x, y, scaleX, scaleY, unique, play): only the name and the play flag are read.
        private static void SetupEffectPostfix(string name, int play)
        {
            try
            {
                if (!bubbleOn) return;
                recorder.OnEffect(name, play, DateTime.UtcNow);
            }
            catch (Exception e)
            {
                bubbleOn = false;
                try { log?.Warn("bubble collection disabled: SetupEffect hook threw " + e.GetType().Name); } catch { }
            }
        }

        // CombatOverlayImageController.Show(addresses, ...): an iterator; the address list is known when it is created. Only
        // the catalog filter in the store decides what is kept.
        private static void CombatOverlayPostfix(System.Collections.Generic.IList<string> addresses)
        {
            try
            {
                if (!combatOn) return;
                recorder.OnCatalogKeys(addresses, DateTime.UtcNow);
            }
            catch (Exception e)
            {
                combatOn = false;
                try { log?.Warn("combat event collection disabled: Show hook threw " + e.GetType().Name); } catch { }
            }
        }

        // StoryCharacterController.LoadPortraitSync(key): reads the character's portrait list for catalog picture keys (cut in).
        private static void PortraitPostfix(object __instance)
        {
            try
            {
                if (!cutInOn) return;
                recorder.OnCatalogKeys(accessRef.ReadPortraitKeys(__instance), DateTime.UtcNow);
            }
            catch (Exception e)
            {
                cutInOn = false;
                try { log?.Warn("cut in collection disabled: LoadPortraitSync hook threw " + e.GetType().Name); } catch { }
            }
        }

        // ItemData.AddCount / SetCount: runs after the game changed the count (a save load calls SetCount for every item, so
        // this stays cheap: read three fields, one set insert). Never changes anything.
        private static void ItemCountPostfix(object __instance)
        {
            try
            {
                if (!itemsOn) return;
                if (!accessRef.TryReadItemCount(__instance, out var kind, out var id, out var count)) return;
                itemRecorder.OnCount(kind, id, count, DateTime.UtcNow);
            }
            catch (Exception e)
            {
                itemsOn = false;
                try { log?.Warn("item collection disabled: count hook threw " + e.GetType().Name); } catch { }
            }
        }

        // LibraryPanel.OnPanelOpen: the game has already run OpenEndPanel (ClickFirstButton) by the time this postfix runs.
        private static void LibraryOpenPostfix(object __instance)
        {
            try
            {
                if (!libraryUiOn) return;
                galleryUi.OnLibraryOpen(__instance as UnityEngine.Component);
            }
            catch (Exception e) { FailLibraryUi("OnPanelOpen", e); }
        }

        // OpenEndPanel / OpenDeadPanel / OpenAchivementPanel / OpenLegendPanel: another tab is showing, hide ours.
        private static void LibraryTabPostfix()
        {
            try
            {
                if (!libraryUiOn) return;
                galleryUi.HideOurs();
            }
            catch (Exception e) { FailLibraryUi("Open*Panel", e); }
        }

        private static void LibraryClosePostfix()
        {
            try
            {
                if (!libraryUiOn) return;
                galleryUi.OnLibraryClosed();
            }
            catch (Exception e) { FailLibraryUi("CloseButtonClick", e); }
        }

        private static void ResetDataPostfix(object __instance)
        {
            try
            {
                if (!perSaveOn || !access.IsPlayerSingleton(__instance)) return;
                session.ResetActive();
            }
            catch (Exception e) { FailPerSave("ResetData", e); }
        }

        private static void ExecuteSaveDataPostfix(string saveFile, object gameSave)
        {
            try
            {
                if (!HookLogic.ShouldObserveSaveEvents(titlesOn, perSaveOn)) return;
                if (!access.TryReadSave(gameSave, out var tick, out var title)) return; // universe/legend saves end here

                SnapshotKey key = null;
                if (perSaveOn && tick > 0 && HookLogic.TryGetSlotIdentity(persistentPath, saveFile, out var profile, out var stem))
                    key = new SnapshotKey(profile, stem, tick);

                session.ObserveSave(key, title, DateTime.UtcNow);
            }
            catch (Exception e) { FailPerSave("ExecuteSaveData", e); }
        }

        private static void GetSaveDataPostfix(object __instance, string slot, object __result)
        {
            try
            {
                if (!perSaveOn || __result == null) return;
                AttachOrigin(__instance, slot, __result);
            }
            catch (Exception e) { FailPerSave("GetSaveData", e); }
        }

        private static void GetAutoSaveDataPostfix(object __instance, string slot, object __result)
        {
            try
            {
                if (!perSaveOn || __result == null) return;
                // ExecuteLoadGameData receives the inner GameSave, so the origin is attached to that object.
                if (!access.TryUnwrapGameSave(__result, out var inner)) return;
                AttachOrigin(__instance, slot, inner);
            }
            catch (Exception e) { FailPerSave("GetAutoSaveData", e); }
        }

        private static void ExecuteLoadGameDataPostfix(object gameSave)
        {
            try
            {
                if (!HookLogic.ShouldObserveSaveEvents(titlesOn, perSaveOn)) return;
                var current = access.TryReadCurrentTitle(out var c) ? c : -1;

                SnapshotKey key = null;
                if (perSaveOn
                    && Origins.TryGet(gameSave, out var origin)
                    && access.TryReadSave(gameSave, out var tick, out _)
                    && tick == origin.TimeTick)
                    key = origin;

                session.ObserveLoaded(key, current, DateTime.UtcNow);
            }
            catch (Exception e) { FailPerSave("ExecuteLoadGameData", e); }
        }

        // ---- helpers ----

        private static void AttachOrigin(object saveSystem, string slot, object gameSave)
        {
            if (!access.TryGetSavePath(saveSystem, slot, out var path)) return;
            if (!HookLogic.TryGetSlotIdentity(persistentPath, path, out var profile, out var stem)) return;
            if (!access.TryReadSave(gameSave, out var tick, out _) || tick <= 0) return;
            Origins.Attach(gameSave, new SnapshotKey(profile, stem, tick));
        }

        private static void FailPerSave(string where, Exception e)
        {
            if (Interlocked.Exchange(ref disabledOnce, 1) != 0) return;
            perSaveOn = false;
            try { session?.DisablePerSave(where + " hook threw " + e.GetType().Name); } catch { }
        }

        private static void FailGallery(Exception e)
        {
            galleryOn = false;
            try { log?.Warn("picture collection disabled: ShowPicture hook threw " + e.GetType().Name); } catch { }
        }

        private static void FailLibraryUi(string where, Exception e)
        {
            libraryUiOn = false;
            try { log?.Warn("jangseoru tab disabled: " + where + " hook threw " + e.GetType().Name); } catch { }
            try { galleryUi?.Disable(where, e); } catch { }
        }

        private static void FailTitles(string where, Exception e)
        {
            titlesOn = false;
            try { log?.Warn("title collection disabled: " + where + " hook threw " + e.GetType().Name); } catch { }
        }
    }
}
