using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Jangseoru.Core;

namespace Jangseoru.Plugin
{
    /// <summary>
    /// Read-only access to the game's own types, resolved once by name from <see cref="GameContract"/> and
    /// cached. Hook bodies receive game objects as <c>object</c> and go through here, so a changed game member
    /// can never fail JIT compilation of our hooks. There are deliberately no setters of game data; the only calls with
    /// an effect are UI visibility switches the game itself uses (CommonPanel.Show, InputButton.SetActive).
    /// </summary>
    internal sealed class GameAccess
    {
        private const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        // Patch targets (null when not found).
        public MethodBase SetPlayer { get; private set; }
        public MethodBase ResetData { get; private set; }
        public MethodBase ExecuteSaveData { get; private set; }
        public MethodBase GetSaveData { get; private set; }
        public MethodBase GetAutoSaveData { get; private set; }
        public MethodBase ExecuteLoadGameData { get; private set; }
        public MethodBase ShowPicture { get; private set; }
        public MethodBase SetupEffect { get; private set; }
        public MethodBase CombatOverlayShow { get; private set; }
        public MethodBase LoadPortraitSync { get; private set; }

        /// <summary>The hook targets and readers of the item tabs (ItemData counts, ItemDatabase lists) were all found.</summary>
        public MethodBase ShowBigPicture { get; private set; }
        private PropertyInfo currentLanguage;
        public MethodBase VideoOpen { get; private set; }
        public MethodBase ItemAddCount { get; private set; }
        public MethodBase ItemSetCount { get; private set; }
        public bool ItemReadersResolved { get; private set; }
        private PropertyInfo itemId, itemImage, itemCurrentCount, itemTypeProp, dbInstance, dbBooks, dbMiscs, dbSpecial;
        private MethodInfo itemNameKey, itemDescKey, itemTitleKey;

        /// <summary>The readers of a character's portrait list (cut-in collection) were all found.</summary>
        public bool PortraitReadersResolved { get; private set; }
        private FieldInfo controllerData;
        private PropertyInfo portraitList, portraitAddressKey;

        // Library (도감) UI patch targets.
        public MethodBase LibraryOnPanelOpen { get; private set; }
        public MethodBase LibraryOpenEndPanel { get; private set; }
        public MethodBase LibraryOpenDeadPanel { get; private set; }
        public MethodBase LibraryOpenAchivementPanel { get; private set; }
        public MethodBase LibraryOpenLegendPanel { get; private set; }
        public MethodBase LibraryCloseButtonClick { get; private set; }

        /// <summary>Everything needed to read a save object's token/title and the live title was resolved.</summary>
        public bool ReadersResolved { get; private set; }

        /// <summary>Every field/method/type the Jangseoru tab reads or calls was resolved (patch targets are separate).</summary>
        public bool LibraryUiResolved { get; private set; }

        /// <summary>The 맹세 detail readers (description key and localized text) were resolved; needs <see cref="LibraryUiResolved"/> too.</summary>
        public bool OathDetailResolved { get; private set; }

        // Addressables generic definitions; SpriteLoader closes them over Sprite.
        public MethodInfo LoadAssetAsyncDef { get; private set; }
        public MethodInfo ReleaseDef { get; private set; }
        public Type HandleDef { get; private set; }
        public Type HandleStatusType { get; private set; }

        private FieldInfo libInputClose, libEndPanel, libDeadPanel, libAchievementPanel, libLegendPanel, libFatePanel, inputActionPath;
        private MethodInfo commonPanelShow, inputSetActive;
        private PropertyInfo librarySystemInstance, librarySystemEndCg, collectionList, itemPicture, itemCount;
        private Type inputButtonType, leanLocalizedTextType;
        private MethodInfo itemGetDescKey, resolverGetString;
        private PropertyInfo localizationInstance, localizationResolver;

        private Type gameSaveType, autoGameSaveType;
        private MethodInfo getSaveDataFile, statsGet;
        private FieldInfo timeTick, saveStats, statKey, statValue, autoInner;
        private PropertyInfo playerInstance, playerStats, statValueProp;
        private object titleStatType;

        public static GameAccess Resolve(ILog log)
        {
            var a = new GameAccess();
            try { a.ResolveAll(log); }
            catch (Exception e) { log.Warn("game access resolution failed (" + e.GetType().Name + ")"); }
            return a;
        }

        private void ResolveAll(ILog log)
        {
            SetPlayer = FindMethod(GameContract.SetPlayer);
            ResetData = FindMethod(GameContract.ResetData);
            ExecuteSaveData = FindMethod(GameContract.ExecuteSaveData);
            GetSaveData = FindMethod(GameContract.GetSaveData);
            GetAutoSaveData = FindMethod(GameContract.GetAutoSaveData);
            ExecuteLoadGameData = FindMethod(GameContract.ExecuteLoadGameData);
            getSaveDataFile = FindMethod(GameContract.GetSaveDataFile);
            ShowPicture = FindMethod(GameContract.ShowPicture);
            SetupEffect = FindMethod(GameContract.SetupEffect);
            CombatOverlayShow = FindMethod(GameContract.CombatOverlayShow);
            ShowBigPicture = FindMethod(MediaContract.ShowBigPicture);
            currentLanguage = FindProperty(UiContract.CurrentLanguage);
            VideoOpen = FindMethod(MediaContract.VideoOpen);
            LoadPortraitSync = FindMethod(GameContract.LoadPortraitSync);
            controllerData = FindField(GameContract.CharacterControllerData);
            portraitList = FindProperty(GameContract.CharacterPortraitList);
            portraitAddressKey = FindProperty(GameContract.PortraitAddressKey);
            ResolveItems();
            PortraitReadersResolved = LoadPortraitSync != null && controllerData != null && portraitList != null && portraitAddressKey != null;

            gameSaveType = ResolveType(GameContract.GameSave);
            autoGameSaveType = ResolveType(GameContract.AutoGameSave);
            timeTick = FindField(GameContract.GameSaveTimeTick);
            saveStats = FindField(GameContract.GameSaveStats);
            statKey = FindField(GameContract.StatSaveKey);
            statValue = FindField(GameContract.StatSaveValue);
            autoInner = FindField(GameContract.AutoGameSaveGameSave);
            playerInstance = FindProperty(GameContract.PlayerInstance);
            playerStats = FindProperty(GameContract.PlayerStats);
            statsGet = FindMethod(GameContract.StatsGet);
            statValueProp = FindProperty(GameContract.StatValue);

            var statType = ResolveType(GameContract.GameStatType);
            if (statType != null && statType.IsEnum)
            {
                try { titleStatType = Enum.Parse(statType, GameContract.TitleStatEnumName); } catch { titleStatType = null; }
            }

            ReadersResolved = new object[]
            {
                getSaveDataFile, gameSaveType, autoGameSaveType, timeTick, saveStats, statKey, statValue, autoInner,
                playerInstance, playerStats, statsGet, statValueProp, titleStatType,
            }.All(x => x != null);

            if (!ReadersResolved) log.Warn("some read accessors were not found; per-save tracking will be disabled");

            try { ResolveLibraryUi(log); }
            catch (Exception e) { LibraryUiResolved = false; log.Warn("library UI resolution failed (" + e.GetType().Name + ")"); }
        }

        private void ResolveLibraryUi(ILog log)
        {
            LibraryOnPanelOpen = FindMethod(GameContract.LibraryOnPanelOpen);
            LibraryOpenEndPanel = FindMethod(GameContract.LibraryOpenEndPanel);
            LibraryOpenDeadPanel = FindMethod(GameContract.LibraryOpenDeadPanel);
            LibraryOpenAchivementPanel = FindMethod(GameContract.LibraryOpenAchivementPanel);
            LibraryOpenLegendPanel = FindMethod(GameContract.LibraryOpenLegendPanel);
            LibraryCloseButtonClick = FindMethod(GameContract.LibraryCloseButtonClick);

            libInputClose = FindField(GameContract.LibraryInputCloseButton);
            libEndPanel = FindField(GameContract.LibraryEndPanel);
            libDeadPanel = FindField(GameContract.LibraryDeadPanel);
            libAchievementPanel = FindField(GameContract.LibraryAchievementPanel);
            libLegendPanel = FindField(GameContract.LibraryLegendPanel);
            libFatePanel = FindField(GameContract.LibraryFatePanel);
            commonPanelShow = FindMethod(GameContract.CommonPanelShow);
            inputSetActive = FindMethod(GameContract.InputButtonSetActive);
            inputActionPath = FindField(GameContract.InputButtonActionPath);
            inputButtonType = ResolveType(GameContract.InputButton);

            librarySystemInstance = FindProperty(GameContract.LibrarySystemInstance);
            librarySystemEndCg = FindProperty(GameContract.LibrarySystemEndCg);
            collectionList = FindProperty(GameContract.CollectionDataList); // generic definition: only checked here, read by name per instance
            itemPicture = FindProperty(GameContract.LibraryItemPicture);
            itemCount = FindProperty(GameContract.LibraryItemCount);

            LoadAssetAsyncDef = FindMethod(GameContract.AddressablesLoadAssetAsync);
            ReleaseDef = FindMethod(GameContract.AddressablesRelease);
            HandleDef = ResolveType(GameContract.AsyncOperationHandle);
            HandleStatusType = ResolveType(GameContract.AsyncOperationStatus);
            var handleMembersOk = HandleDef != null
                                  && FindProperty(GameContract.HandleIsDone) != null
                                  && FindProperty(GameContract.HandleStatus) != null
                                  && FindProperty(GameContract.HandleResult) != null
                                  && FindMethod(GameContract.HandleIsValid) != null
                                  && HandleStatusType != null && HandleStatusType.IsEnum
                                  && Enum.GetNames(HandleStatusType).Contains(GameContract.StatusSucceeded.Name);
            leanLocalizedTextType = ResolveType(GameContract.LeanLocalizedText);

            LibraryUiResolved = handleMembersOk && new object[]
            {
                libInputClose, libEndPanel, libDeadPanel, libAchievementPanel, libLegendPanel, libFatePanel,
                commonPanelShow, inputSetActive, inputActionPath, inputButtonType,
                librarySystemInstance, librarySystemEndCg, collectionList, itemPicture, itemCount,
                LoadAssetAsyncDef, ReleaseDef, leanLocalizedTextType,
            }.All(x => x != null);

            if (!LibraryUiResolved) log.Warn("some library members were not found; the Jangseoru tab will be disabled");

            itemGetDescKey = FindMethod(GameContract.LibraryItemGetDescKey);
            localizationInstance = FindProperty(GameContract.LocalizationManagerInstance);
            localizationResolver = FindProperty(GameContract.LocalizationManagerLocaleResolver);
            resolverGetString = FindMethod(GameContract.LocaleResolverGetString);
            OathDetailResolved = LibraryUiResolved
                                 && new object[] { itemGetDescKey, localizationInstance, localizationResolver, resolverGetString }.All(x => x != null);
            if (LibraryUiResolved && !OathDetailResolved) log.Warn("oath detail members were not found; 맹세 cards will not open");
        }

        // ---- library UI (never throw) ----

        /// <summary>The four game panels and the fate panel are all set on this LibraryPanel instance.</summary>
        public bool LibraryPanelsPresent(object libraryPanel)
        {
            try
            {
                if (!LibraryUiResolved || libraryPanel == null) return false;
                return new[] { libEndPanel, libDeadPanel, libAchievementPanel, libLegendPanel, libFatePanel, libInputClose }
                    .All(f => f.GetValue(libraryPanel) != null);
            }
            catch (Exception) { return false; }
        }

        /// <summary>CommonPanel.Show(false) on the four game panels, the way the game switches its own tabs.</summary>
        public bool HideLibraryPanels(object libraryPanel)
        {
            try
            {
                if (!LibraryUiResolved || libraryPanel == null) return false;
                foreach (var f in new[] { libEndPanel, libDeadPanel, libAchievementPanel, libLegendPanel })
                {
                    var panel = f.GetValue(libraryPanel);
                    if (panel != null) commonPanelShow.Invoke(panel, new object[] { false });
                }
                return true;
            }
            catch (Exception) { return false; }
        }

        /// <summary>Shows the first game panel again (used only if our tab has to be torn down while it is showing).</summary>
        public bool ShowEndPanel(object libraryPanel)
        {
            try
            {
                var panel = LibraryUiResolved && libraryPanel != null ? libEndPanel.GetValue(libraryPanel) : null;
                if (panel == null) return false;
                commonPanelShow.Invoke(panel, new object[] { true });
                return true;
            }
            catch (Exception) { return false; }
        }

        public object GetFatePanel(object libraryPanel)
        {
            try { return LibraryUiResolved && libraryPanel != null ? libFatePanel.GetValue(libraryPanel) : null; }
            catch (Exception) { return null; }
        }

        /// <summary>LibraryPanel._inputCloseButton.SetActive(active): off while a detail view owns the cancel key.</summary>
        public bool SetLibraryInputClose(object libraryPanel, bool active)
        {
            try
            {
                var ib = LibraryUiResolved && libraryPanel != null ? libInputClose.GetValue(libraryPanel) : null;
                if (ib == null) return false;
                inputSetActive.Invoke(ib, new object[] { active });
                return true;
            }
            catch (Exception) { return false; }
        }

        public bool IsInputButton(object component) => inputButtonType != null && component != null && inputButtonType.IsInstanceOfType(component);

        /// <summary>
        /// The picture keys a story character's portrait list points at. Read only; empty when anything is missing. Never throws.
        /// </summary>
        public System.Collections.Generic.IEnumerable<string> ReadPortraitKeys(object characterController)
        {
            var keys = new System.Collections.Generic.List<string>();
            try
            {
                if (!PortraitReadersResolved || characterController == null) return keys;
                var data = controllerData.GetValue(characterController);
                var list = data != null ? portraitList.GetValue(data) as System.Collections.IEnumerable : null;
                if (list == null) return keys;
                foreach (var item in list)
                {
                    if (item == null) continue;
                    if (portraitAddressKey.GetValue(item) is string key && key.Length > 0) keys.Add(key);
                }
            }
            catch (Exception) { keys.Clear(); }
            return keys;
        }

        private void ResolveItems()
        {
            ItemAddCount = FindMethod(GameContract.ItemAddCount);
            ItemSetCount = FindMethod(GameContract.ItemSetCount);
            itemId = FindProperty(GameContract.ItemId);
            itemImage = FindProperty(GameContract.ItemImage);
            itemCurrentCount = FindProperty(GameContract.ItemCurrentCount);
            itemTypeProp = FindProperty(GameContract.ItemTypeProp);
            itemNameKey = FindMethod(GameContract.ItemNameKey);
            itemDescKey = FindMethod(GameContract.ItemDescKey);
            itemTitleKey = FindMethod(GameContract.ItemTitleKey);
            dbInstance = FindProperty(GameContract.ItemDatabaseInstance);
            dbBooks = FindProperty(GameContract.ItemDatabaseBooks);
            dbMiscs = FindProperty(GameContract.ItemDatabaseMiscs);
            dbSpecial = FindProperty(GameContract.ItemDatabaseSpecial);
            ItemReadersResolved = new object[]
            {
                ItemAddCount, ItemSetCount, itemId, itemImage, itemCurrentCount, itemTypeProp, itemNameKey, itemDescKey, itemTitleKey,
                dbInstance, dbBooks, dbMiscs, dbSpecial,
            }.All(x => x != null);
        }

        /// <summary>
        /// The items of one kind from the game's ItemDatabase, in the game's order. Read only: nothing is called that changes a
        /// count, and the sprites stay the game's. False when anything is missing. Never throws.
        /// </summary>
        public bool TryReadItems(ItemKind kind, out List<ItemEntry> entries)
        {
            entries = new List<ItemEntry>();
            try
            {
                if (!ItemReadersResolved) return false;
                var db = dbInstance.GetValue(null, null);
                if (db == null || (db is UnityEngine.Object udb && udb == null)) return false;
                var collection = (kind == ItemKind.Book ? dbBooks : kind == ItemKind.Misc ? dbMiscs : dbSpecial).GetValue(db, null);
                if (collection == null) return false;
                var listProp = collection.GetType().GetProperty(GameContract.CollectionDataList.Name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var list = listProp?.GetValue(collection, null) as IEnumerable;
                if (list == null) return false;
                foreach (var item in list)
                {
                    if (item == null || (item is UnityEngine.Object u && u == null)) continue;
                    var id = itemId.GetValue(item, null) as string;
                    if (string.IsNullOrEmpty(id)) continue;
                    var sprite = itemImage.GetValue(item, null) as UnityEngine.Sprite;
                    entries.Add(new ItemEntry
                    {
                        Kind = kind,
                        Id = id,
                        Image = sprite != null ? sprite : null, // a destroyed sprite reads as null
                        NameKey = itemNameKey.Invoke(item, null) as string,
                        DescKey = itemDescKey.Invoke(item, null) as string,
                        TitleKey = itemTitleKey.Invoke(item, null) as string,
                    });
                }
                return true;
            }
            catch (Exception) { entries.Clear(); return false; }
        }

        /// <summary>
        /// Kind, id and current count of an ItemData instance (for the count hooks). False for consumables, unknown kinds or
        /// anything unreadable. Read only. Never throws.
        /// </summary>
        public bool TryReadItemCount(object item, out ItemKind kind, out string id, out int count)
        {
            kind = ItemKind.Book;
            id = null;
            count = 0;
            try
            {
                if (!ItemReadersResolved || item == null) return false;
                var type = itemTypeProp.GetValue(item, null);
                if (type == null) return false;
                switch (Convert.ToInt32(type)) // GameItemType: 1 book, 2 misc, 3 special (4 consumable is not collected)
                {
                    case 1: kind = ItemKind.Book; break;
                    case 2: kind = ItemKind.Misc; break;
                    case 3: kind = ItemKind.Special; break;
                    default: return false;
                }
                id = itemId.GetValue(item, null) as string;
                if (string.IsNullOrEmpty(id)) return false;
                count = (int)itemCurrentCount.GetValue(item, null);
                return true;
            }
            catch (Exception) { id = null; return false; }
        }

        public bool IsLeanLocalizedText(object component) => leanLocalizedTextType != null && component != null && leanLocalizedTextType.IsInstanceOfType(component);

        /// <summary>Switches off an InputButton's key binding when it has one, so a cloned button never answers the original's key.</summary>
        public bool DisableKeyBindingIfAny(object inputButton, out string actionPath)
        {
            actionPath = null;
            try
            {
                if (!IsInputButton(inputButton)) return false;
                actionPath = inputActionPath.GetValue(inputButton) as string;
                if (string.IsNullOrEmpty(actionPath)) return false;
                inputSetActive.Invoke(inputButton, new object[] { false });
                return true;
            }
            catch (Exception) { return false; }
        }

        internal sealed class EndCgEntry
        {
            public string Id;
            public int Count;
            public UnityEngine.Sprite Picture;
        }

        /// <summary>
        /// Reads LibrarySystem.Instance.EndCg.List (id = asset name, Count, Picture). Read-only: the sprites are references
        /// owned by the game and must never be modified or destroyed.
        /// </summary>
        public bool TryReadEndCg(out List<EndCgEntry> entries)
        {
            entries = new List<EndCgEntry>();
            try
            {
                if (!TryGetEndCgList(out var list)) return false;
                foreach (var item in list)
                {
                    if (!(item is UnityEngine.Object unityItem) || unityItem == null) continue;
                    entries.Add(new EndCgEntry
                    {
                        Id = unityItem.name,
                        Count = (int)itemCount.GetValue(item, null),
                        Picture = itemPicture.GetValue(item, null) as UnityEngine.Sprite,
                    });
                }
                return true;
            }
            catch (Exception) { entries.Clear(); return false; }
        }

        /// <summary>
        /// One EndCg entry by id, with its description key from LibraryItemData.GetDescKey() (a pure string getter).
        /// Read-only: never calls Add/Set or anything that changes Count. False if the id is not in EndCg.
        /// </summary>
        public bool TryReadOath(string id, out EndCgEntry entry, out string descKey)
        {
            entry = null;
            descKey = null;
            try
            {
                if (!OathDetailResolved || !TryFindEndCgItem(id, out var item)) return false;
                entry = new EndCgEntry
                {
                    Id = id,
                    Count = (int)itemCount.GetValue(item, null),
                    Picture = itemPicture.GetValue(item, null) as UnityEngine.Sprite,
                };
                descKey = itemGetDescKey.Invoke(item, null) as string;
                return true;
            }
            catch (Exception) { entry = null; descKey = null; return false; }
        }

        /// <summary>The current Count of an EndCg id (read-only), for the before/after check around the 맹세 detail.</summary>
        public bool TryReadOathCount(string id, out int count)
        {
            count = 0;
            try
            {
                if (!TryFindEndCgItem(id, out var item)) return false;
                count = (int)itemCount.GetValue(item, null);
                return true;
            }
            catch (Exception) { count = 0; return false; }
        }

        /// <summary>LocalizationManager.Instance.LocaleResolver.GetString(key), the way CgPanel reads the description.</summary>
        public bool TryGetLocalizedString(string key, out string text)
        {
            text = null;
            try
            {
                if (!OathDetailResolved || string.IsNullOrEmpty(key)) return false;
                var manager = localizationInstance.GetValue(null, null);
                if (manager == null || (manager is UnityEngine.Object um && um == null)) return false;
                var resolver = localizationResolver.GetValue(manager, null);
                if (resolver == null) return false;
                text = resolverGetString.Invoke(resolver, new object[] { key }) as string;
                return text != null;
            }
            catch (Exception) { text = null; return false; }
        }

        private bool TryGetEndCgList(out IEnumerable list)
        {
            list = null;
            if (!LibraryUiResolved) return false;
            var system = librarySystemInstance.GetValue(null, null);
            if (system == null) return false;
            var endCg = librarySystemEndCg.GetValue(system, null);
            if (endCg == null) return false;
            var listProp = endCg.GetType().GetProperty(collectionList.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            list = listProp?.GetValue(endCg, null) as IEnumerable;
            return list != null;
        }

        private bool TryFindEndCgItem(string id, out object item)
        {
            item = null;
            if (string.IsNullOrEmpty(id) || !TryGetEndCgList(out var list)) return false;
            foreach (var candidate in list)
            {
                if (candidate is UnityEngine.Object u && u != null && u.name == id) { item = candidate; return true; }
            }
            return false;
        }

        // ---- reads (never throw) ----

        public bool TryReadCurrentTitle(out int title)
        {
            title = 0;
            try
            {
                var player = playerInstance.GetValue(null, null);
                if (player == null) return false;
                var stats = playerStats.GetValue(player, null);
                if (stats == null) return false;
                var stat = statsGet.Invoke(stats, new[] { titleStatType });
                if (stat == null) return false;
                title = (int)statValueProp.GetValue(stat, null);
                return true;
            }
            catch (Exception) { return false; }
        }

        public bool IsPlayerSingleton(object candidate)
        {
            try { return candidate != null && ReferenceEquals(candidate, playerInstance.GetValue(null, null)); }
            catch (Exception) { return false; }
        }

        /// <summary>Accepts a GameSave or an AutoGameSave and returns the inner GameSave.</summary>
        public bool TryUnwrapGameSave(object saveObject, out object gameSave)
        {
            gameSave = null;
            try
            {
                if (saveObject == null) return false;
                if (gameSaveType.IsInstanceOfType(saveObject)) { gameSave = saveObject; return true; }
                if (autoGameSaveType.IsInstanceOfType(saveObject))
                {
                    gameSave = autoInner.GetValue(saveObject);
                    return gameSave != null;
                }
                return false;
            }
            catch (Exception) { return false; }
        }

        /// <summary>Reads the token and the saved title from an in-memory save object. False if either is unavailable.</summary>
        public bool TryReadSave(object saveObject, out long tick, out int title)
        {
            tick = 0;
            title = 0;
            try
            {
                if (!TryUnwrapGameSave(saveObject, out var save)) return false;
                tick = (long)timeTick.GetValue(save);

                var list = saveStats.GetValue(save) as IEnumerable;
                if (list == null) return false;
                foreach (var item in list)
                {
                    if (item != null && string.Equals(statKey.GetValue(item) as string, "title", StringComparison.Ordinal))
                    {
                        title = (int)statValue.GetValue(item);
                        return true;
                    }
                }
                return false;
            }
            catch (Exception) { return false; }
        }

        /// <summary>The game's own path for a slot (it already includes the user folder). Path assembly only; nothing is opened.</summary>
        public bool TryGetSavePath(object saveSystem, string slot, out string path)
        {
            path = null;
            try
            {
                if (saveSystem == null || getSaveDataFile == null) return false;
                path = getSaveDataFile.Invoke(saveSystem, new object[] { slot }) as string;
                return !string.IsNullOrEmpty(path);
            }
            catch (Exception) { return false; }
        }

        // ---- resolution helpers ----

        private static Type ResolveType(string fullName)
        {
            var t = AccessTools.TypeByName(fullName);
            if (t != null) return t;

            // The game's assemblies are loaded lazily; load by name if the plugin starts before first use.
            try
            {
                return Assembly.Load(AssemblyFor(fullName)).GetType(fullName, false);
            }
            catch (Exception) { return null; }
        }

        private static string AssemblyFor(string fullName)
        {
            if (fullName.StartsWith("Mortal.Story.", StringComparison.Ordinal)) return "Mortal.Story";
            if (fullName.StartsWith("Mortal.Combat.", StringComparison.Ordinal)) return "Mortal.Combat";
            if (fullName.StartsWith("UnityEngine.AddressableAssets.", StringComparison.Ordinal)) return "Unity.Addressables";
            if (fullName.StartsWith("UnityEngine.ResourceManagement.", StringComparison.Ordinal)) return "Unity.ResourceManager";
            if (fullName.StartsWith("Lean.Localization.", StringComparison.Ordinal)) return "LeanLocalization";
            return "Mortal.Core";
        }

        private static string TypeName(Type t)
            => t.IsGenericParameter ? t.Name
            : t.IsGenericType
                ? t.GetGenericTypeDefinition().FullName + "[" + string.Join(",", t.GetGenericArguments().Select(TypeName)) + "]"
                : t.FullName;

        private static MethodInfo FindMethod(MemberSpec spec)
        {
            var type = ResolveType(spec.DeclaringType);
            if (type == null) return null;
            return type.GetMethods(All).FirstOrDefault(m =>
                m.Name == spec.Name
                && (m.IsGenericMethodDefinition ? m.GetGenericArguments().Length : 0) == spec.GenericArity
                && (spec.ValueType == null || TypeName(m.ReturnType) == spec.ValueType)
                && m.GetParameters().Select(p => TypeName(p.ParameterType)).SequenceEqual(spec.ParameterTypes));
        }

        private static FieldInfo FindField(MemberSpec spec)
        {
            var f = ResolveType(spec.DeclaringType)?.GetField(spec.Name, All);
            return f != null && (spec.ValueType == null || TypeName(f.FieldType) == spec.ValueType) ? f : null;
        }

        /// <summary>The game's display language name (LeanLocalization.CurrentLanguage), or null when it cannot be read.</summary>
        public string ReadLanguage()
        {
            try { return currentLanguage?.GetValue(null, null) as string; }
            catch (Exception) { return null; }
        }

        private static PropertyInfo FindProperty(MemberSpec spec)
        {
            var p = ResolveType(spec.DeclaringType)?.GetProperty(spec.Name, All);
            return p != null && (spec.ValueType == null || TypeName(p.PropertyType) == spec.ValueType) ? p : null;
        }
    }
}
