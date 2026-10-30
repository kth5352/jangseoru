using System.Collections.Generic;
using System.Linq;
using Jangseoru.Core;

namespace Jangseoru.Plugin
{
    internal enum FeatureState
    {
        Missing,   // target or accessor not found in the game
        Resolved,  // found, not (yet) patched
        Patched,   // postfix installed
        Disabled   // found but patching failed or the feature was switched off
    }

    /// <summary>
    /// One-line account of what resolved, what was patched and what was disabled, so a single
    /// BepInEx log is enough to tell which feature is unavailable after a game update.
    /// </summary>
    internal sealed class SelfCheck
    {
        public const string FSetPlayer = "SetPlayer";
        public const string FResetData = "ResetData";
        public const string FSaveExec = "SaveExec";
        public const string FGetSave = "GetSave";
        public const string FGetAutoSave = "GetAutoSave";
        public const string FExecuteLoad = "ExecuteLoad";
        public const string FReaders = "Readers";
        public const string FShowPicture = "ShowPicture";
        public const string FItemHooks = "ItemHooks";         // ItemData.AddCount / SetCount: obtained items are collected
        public const string FItemReaders = "ItemReaders";     // ItemDatabase lists and item members the item tabs read
        public const string FCombatOverlay = "CombatOverlay"; // combat event effects are collected from the overlay image controller
        public const string FPortraitLoad = "PortraitLoad";   // cut in is collected when a story character loads a portrait
        public const string FSetupEffect = "SetupEffect";     // bubble presentations are collected from story effects
        public const string FLibraryOpen = "LibraryOpen";
        public const string FLibraryTabs = "LibraryTabs";     // the four Open*Panel targets, as one feature
        public const string FLibraryClose = "LibraryClose";   // optional: thumbnails are also released by our own OnDisable
        public const string FLibraryReaders = "LibraryReaders";
        public const string FOathDetail = "OathDetail";       // 맹세 detail readers (description text); only that view depends on it

        private static readonly string[] PerSaveFeatures = { FResetData, FSaveExec, FGetSave, FGetAutoSave, FExecuteLoad, FReaders };
        private static readonly string[] ResolveOnlyFeatures = { FReaders, FLibraryReaders, FOathDetail, FItemReaders };

        private readonly Dictionary<string, FeatureState> states = new Dictionary<string, FeatureState>();
        private bool pictureDetailCreated;

        public string AppVersion { get; private set; }

        public static SelfCheck FromAccess(GameAccess access, string appVersion)
        {
            var c = new SelfCheck { AppVersion = appVersion };
            c.Set(FSetPlayer, access.SetPlayer != null);
            c.Set(FResetData, access.ResetData != null);
            c.Set(FSaveExec, access.ExecuteSaveData != null);
            c.Set(FGetSave, access.GetSaveData != null);
            c.Set(FGetAutoSave, access.GetAutoSaveData != null);
            c.Set(FExecuteLoad, access.ExecuteLoadGameData != null);
            c.Set(FReaders, access.ReadersResolved);
            c.Set(FShowPicture, access.ShowPicture != null);
            c.Set(FSetupEffect, access.SetupEffect != null);
            c.Set(FCombatOverlay, access.CombatOverlayShow != null);
            c.Set(FItemHooks, access.ItemAddCount != null && access.ItemSetCount != null);
            c.Set(FItemReaders, access.ItemReadersResolved);
            c.Set(FPortraitLoad, access.PortraitReadersResolved);
            c.Set(FLibraryOpen, access.LibraryOnPanelOpen != null);
            c.Set(FLibraryTabs, access.LibraryOpenEndPanel != null && access.LibraryOpenDeadPanel != null
                                && access.LibraryOpenAchivementPanel != null && access.LibraryOpenLegendPanel != null);
            c.Set(FLibraryClose, access.LibraryCloseButtonClick != null);
            c.Set(FLibraryReaders, access.LibraryUiResolved);
            c.Set(FOathDetail, access.OathDetailResolved);
            return c;
        }

        public FeatureState Get(string feature) => states.TryGetValue(feature, out var s) ? s : FeatureState.Missing;

        public void MarkPatched(string feature, bool patched)
        {
            if (Get(feature) == FeatureState.Missing) return;
            states[feature] = patched ? FeatureState.Patched : FeatureState.Disabled;
        }

        /// <summary>Accessors resolve without a patch ("Readers"): they count as available once resolved.</summary>
        private bool Available(string feature)
        {
            var s = Get(feature);
            return ResolveOnlyFeatures.Contains(feature) ? s == FeatureState.Resolved : s == FeatureState.Patched;
        }

        /// <summary>The Jangseoru tab needs the open hook, all four tab hooks (to hide itself) and every UI reader.</summary>
        public bool LibraryUiAvailable => Available(FLibraryOpen) && Available(FLibraryTabs) && Available(FLibraryReaders);

        /// <summary>Before patching: every library hook target and every UI reader was found.</summary>
        public bool LibraryUiResolvable => Get(FLibraryOpen) == FeatureState.Resolved && Get(FLibraryTabs) == FeatureState.Resolved
                                           && Available(FLibraryReaders);

        /// <summary>The 맹세 detail opens only with the tab itself and its own readers.</summary>
        public bool OathDetailAvailable => LibraryUiAvailable && Available(FOathDetail);

        /// <summary>Records whether the 사건 detail can actually be created (the shared Addressables loader was bound).</summary>
        public void MarkPictureDetail(bool created) => pictureDetailCreated = created;

        /// <summary>The 사건 detail opens only with the installed tab and a bound Addressables loader.</summary>
        public bool PictureDetailAvailable => LibraryUiAvailable && pictureDetailCreated;

        /// <summary>The book frame is captured from the patched ShowPicture hook; without it the detail uses the paper pages.</summary>
        public bool BookCaptureAvailable => PictureDetailAvailable && Available(FShowPicture);

        public bool TitleCollectionAvailable => Available(FSetPlayer);

        /// <summary>Event-illustration collection is independent of the byeolho features.</summary>
        public bool GalleryCollectionAvailable => Available(FShowPicture);

        /// <summary>Bubble presentations are collected from the story effect hook, independently of ShowPicture.</summary>
        public bool BubbleCollectionAvailable => Available(FSetupEffect);

        public bool CombatCollectionAvailable => Available(FCombatOverlay);

        /// <summary>Obtained items are collected when both count hooks are in and the item members can be read.</summary>
        public bool ItemCollectionAvailable => Available(FItemHooks) && Available(FItemReaders);

        /// <summary>The 잡물/서적/보물 tabs need the library tab itself and the item readers.</summary>
        public bool ItemTabsAvailable => LibraryUiAvailable && Available(FItemReaders);

        public bool CutInCollectionAvailable => Available(FPortraitLoad);

        /// <summary>Per-save tracking needs every per-save hook and all readers; one gap disables all of it.</summary>
        public bool PerSaveAvailable => PerSaveFeatures.All(Available);

        public void Report(ILog log)
        {
            var line = "self-check (game " + (AppVersion ?? "?") + "): "
                       + string.Join(" ", states.OrderBy(k => k.Key).Select(k => k.Key + "=" + k.Value.ToString().ToUpperInvariant()))
                       + " | titles=" + (TitleCollectionAvailable ? "ON" : "OFF")
                       + " perSave=" + (PerSaveAvailable ? "ON" : "OFF")
                       + " pictures=" + (GalleryCollectionAvailable ? "ON" : "OFF")
                       + " library=" + (LibraryUiAvailable ? "ON" : "OFF")
                       + " oathDetail=" + (OathDetailAvailable ? "ON" : "OFF")
                       + " items=" + (ItemCollectionAvailable ? "ON" : "OFF") + "/" + (ItemTabsAvailable ? "tabs" : "no-tabs")
                       + " pictureDetail=" + (PictureDetailAvailable ? (BookCaptureAvailable ? "ON(book capture)" : "ON(paper only)") : "OFF");
            if (TitleCollectionAvailable && PerSaveAvailable && GalleryCollectionAvailable && LibraryUiAvailable && OathDetailAvailable
                && PictureDetailAvailable && BookCaptureAvailable) log.Info(line);
            else log.Warn(line);
        }

        private void Set(string feature, bool resolved) => states[feature] = resolved ? FeatureState.Resolved : FeatureState.Missing;
    }
}
