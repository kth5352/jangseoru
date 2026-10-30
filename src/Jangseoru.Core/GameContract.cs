using System;
using System.Collections.Generic;
using System.Linq;

namespace Jangseoru.Core
{
    public enum MemberKind
    {
        Method,
        Field,
        Property,
        EnumValue,
        /// <summary>A type that must exist; only <see cref="MemberSpec.DeclaringType"/> is meaningful.</summary>
        Type
    }

    /// <summary>A game member the plugin relies on, described by names only (no game assembly reference).</summary>
    public sealed class MemberSpec
    {
        public MemberKind Kind { get; }
        public string DeclaringType { get; }
        public string Name { get; }
        /// <summary>Full parameter type names for methods; empty otherwise.</summary>
        public string[] ParameterTypes { get; }
        /// <summary>Return type (method), field type or property type; null means "do not check".</summary>
        public string ValueType { get; }
        /// <summary>Generic method parameter count (0 = not generic). Generic parameters are written by name, e.g. <c>TObject</c>.</summary>
        public int GenericArity { get; }

        private MemberSpec(MemberKind kind, string declaringType, string name, string valueType, string[] parameterTypes, int genericArity = 0)
        {
            GenericArity = genericArity;
            Kind = kind;
            DeclaringType = declaringType;
            Name = name;
            ValueType = valueType;
            ParameterTypes = parameterTypes ?? new string[0];
        }

        public static MemberSpec Method(string declaringType, string name, string returnType, params string[] parameterTypes)
            => new MemberSpec(MemberKind.Method, declaringType, name, returnType, parameterTypes);

        /// <summary>A generic method definition such as <c>Addressables.LoadAssetAsync&lt;TObject&gt;(object)</c>.</summary>
        public static MemberSpec GenericMethod(string declaringType, string name, int arity, string returnType, params string[] parameterTypes)
            => new MemberSpec(MemberKind.Method, declaringType, name, returnType, parameterTypes, arity);

        public static MemberSpec TypeExists(string fullName)
            => new MemberSpec(MemberKind.Type, fullName, "", null, null);

        public static MemberSpec Field(string declaringType, string name, string fieldType)
            => new MemberSpec(MemberKind.Field, declaringType, name, fieldType, null);

        public static MemberSpec Property(string declaringType, string name, string propertyType)
            => new MemberSpec(MemberKind.Property, declaringType, name, propertyType, null);

        public static MemberSpec EnumValue(string declaringType, string name)
            => new MemberSpec(MemberKind.EnumValue, declaringType, name, null, null);

        public override string ToString()
        {
            switch (Kind)
            {
                case MemberKind.Method:
                    return DeclaringType + "." + Name + (GenericArity > 0 ? "`" + GenericArity : "")
                           + "(" + string.Join(",", ParameterTypes) + ")" + (ValueType != null ? ":" + ValueType : "");
                case MemberKind.EnumValue:
                    return DeclaringType + "." + Name + " (enum value)";
                case MemberKind.Type:
                    return DeclaringType + " (type)";
                default:
                    return DeclaringType + "." + Name + (ValueType != null ? ":" + ValueType : "");
            }
        }
    }

    /// <summary>
    /// Single source of truth for the game members the plugin patches or reads. The plugin resolves its
    /// reflection handles from these specs, and the test project checks them against the real game DLLs.
    /// </summary>
    public static class GameContract
    {
        public const string SaveSystem = "Mortal.Core.SaveSystem";
        public const string GameSave = "Mortal.Core.GameSave";
        public const string AutoGameSave = "Mortal.Core.AutoGameSave";
        public const string StatSave = "Mortal.Core.StatSave";
        public const string PlayerStatManagerData = "Mortal.Core.PlayerStatManagerData";
        public const string PlayerStatData = "Mortal.Core.PlayerStatData";
        public const string GameStat = "Mortal.Core.GameStat";
        public const string GameStatType = "Mortal.Core.GameStatType";
        public const string StatModifyManager = "Mortal.Story.StatModifyManager";
        public const string StoryMainUI = "Mortal.Story.StoryMainUI";
        public const string StoryEffectsController = "Mortal.Story.StoryEffectsController";
        public const string CombatOverlayImageController = "Mortal.Combat.CombatOverlayImageController";
        public const string StoryCharacterController = "Mortal.Story.StoryCharacterController";
        public const string StoryCharacterData = "Mortal.Core.StoryCharacterData";
        public const string StoryCharaterImageItem = "Mortal.Core.StoryCharaterImageItem";

        /// <summary>Enum member name of the title (byeolho) stat; its string value in scripts is "title".</summary>
        public const string TitleStatEnumName = "稱號";

        public static readonly MemberSpec SetPlayer =
            MemberSpec.Method(StatModifyManager, "SetPlayer", "System.Void", "System.String", "System.Int32");

        public static readonly MemberSpec ResetData =
            MemberSpec.Method(PlayerStatManagerData, "ResetData", "System.Void");

        public static readonly MemberSpec ExecuteSaveData =
            MemberSpec.Method(SaveSystem, "ExecuteSaveData", "System.Void", "System.String", "System.Object");

        public static readonly MemberSpec GetSaveData =
            MemberSpec.Method(SaveSystem, "GetSaveData", GameSave, "System.String");

        public static readonly MemberSpec GetAutoSaveData =
            MemberSpec.Method(SaveSystem, "GetAutoSaveData", AutoGameSave, "System.String");

        public static readonly MemberSpec ExecuteLoadGameData =
            MemberSpec.Method(SaveSystem, "ExecuteLoadGameData", "System.Void", GameSave);

        public static readonly MemberSpec GetSaveDataFile =
            MemberSpec.Method(SaveSystem, "GetSaveDataFile", "System.String", "System.String");

        public static readonly MemberSpec GameSaveTimeTick = MemberSpec.Field(GameSave, "TimeTick", "System.Int64");
        public static readonly MemberSpec GameSaveStats = MemberSpec.Field(GameSave, "Stats", "System.Collections.Generic.List`1[" + StatSave + "]");
        public static readonly MemberSpec StatSaveKey = MemberSpec.Field(StatSave, "Key", "System.String");
        public static readonly MemberSpec StatSaveValue = MemberSpec.Field(StatSave, "Value", "System.Int32");
        public static readonly MemberSpec AutoGameSaveGameSave = MemberSpec.Field(AutoGameSave, "GameSave", GameSave);

        public static readonly MemberSpec PlayerInstance = MemberSpec.Property(PlayerStatManagerData, "Instance", PlayerStatManagerData);
        public static readonly MemberSpec PlayerStats = MemberSpec.Property(PlayerStatManagerData, "Stats", PlayerStatData);
        public static readonly MemberSpec StatsGet = MemberSpec.Method(PlayerStatData, "Get", GameStat, GameStatType);
        public static readonly MemberSpec StatValue = MemberSpec.Property(GameStat, "Value", "System.Int32");
        public static readonly MemberSpec TitleStatType = MemberSpec.EnumValue(GameStatType, TitleStatEnumName);

        /// <summary>Iterator that shows an event illustration; its parameter is the script key (without the pic_ prefix).</summary>
        public static readonly MemberSpec ShowPicture =
            MemberSpec.Method(StoryMainUI, "ShowPicture", "System.Collections.IEnumerator", "System.String");

        /// <summary>Starts or stops a story effect: (name, x, y, scaleX, scaleY, unique, play). Only the effect name and play flag are read.</summary>
        public static readonly MemberSpec SetupEffect =
            MemberSpec.Method(StoryEffectsController, "SetupEffect", "System.Void",
                "System.String", "System.Single", "System.Single", "System.Single", "System.Single", "System.Int32", "System.Int32");

        /// <summary>
        /// Combat overlay images (the combat event effects): only the address list, which holds catalog picture keys, is read.
        /// Parameters: addresses, fadeIn, interval, duration, fadeOut, preserveAspect, waitHide, sound.
        /// </summary>
        public static readonly MemberSpec CombatOverlayShow =
            MemberSpec.Method(CombatOverlayImageController, "Show", "System.Collections.IEnumerator",
                "System.Collections.Generic.IList`1[System.String]", "System.Single", "System.Single", "System.Single", "System.Single",
                "System.Boolean", "System.Boolean", "Mortal.Core.SoundData");

        /// <summary>A story character loads one of its portraits; the character's portrait list holds the picture keys (cut in).</summary>
        public static readonly MemberSpec LoadPortraitSync =
            MemberSpec.Method(StoryCharacterController, "LoadPortraitSync", "System.Void", "System.String");
        public static readonly MemberSpec CharacterControllerData = MemberSpec.Field(StoryCharacterController, "Data", StoryCharacterData);
        public static readonly MemberSpec CharacterPortraitList =
            MemberSpec.Property(StoryCharacterData, "PortraitResourceList", StoryCharaterImageItem + "[]");
        public static readonly MemberSpec PortraitAddressKey = MemberSpec.Property(StoryCharaterImageItem, "AddressKey", "System.String");

        /// <summary>Everything the cut-in collection reads by name.</summary>
        public static readonly IReadOnlyList<MemberSpec> PortraitReaders = new[]
        {
            LoadPortraitSync, CharacterControllerData, CharacterPortraitList, PortraitAddressKey,
        };

        public const string ItemData = "Mortal.Core.ItemData";
        public const string ItemDatabase = "Mortal.Core.ItemDatabase";
        public const string ItemDataCollection = "Mortal.Core.ItemDataCollection";
        public const string GameItemType = "Mortal.Core.GameItemType";

        /// <summary>The two methods through which an item's owned count ever changes (acquire, consume, load a save).</summary>
        public static readonly MemberSpec ItemAddCount = MemberSpec.Method(ItemData, "AddCount", "System.Void", "System.Int32");
        public static readonly MemberSpec ItemSetCount = MemberSpec.Method(ItemData, "SetCount", "System.Void", "System.Int32");
        public static readonly MemberSpec ItemId = MemberSpec.Property(ItemData, "Id", "System.String");
        public static readonly MemberSpec ItemImage = MemberSpec.Property(ItemData, "Image", "UnityEngine.Sprite");
        public static readonly MemberSpec ItemCurrentCount = MemberSpec.Property(ItemData, "CurrentCount", "System.Int32");
        public static readonly MemberSpec ItemTypeProp = MemberSpec.Property(ItemData, "ItemType", GameItemType);
        public static readonly MemberSpec ItemNameKey = MemberSpec.Method(ItemData, "GetNameKey", "System.String");
        public static readonly MemberSpec ItemDescKey = MemberSpec.Method(ItemData, "GetDescKey", "System.String");
        public static readonly MemberSpec ItemTitleKey = MemberSpec.Method(ItemData, "GetTitleKey", "System.String");
        public static readonly MemberSpec ItemDatabaseInstance = MemberSpec.Property(ItemDatabase, "Instance", ItemDatabase);
        public static readonly MemberSpec ItemDatabaseBooks = MemberSpec.Property(ItemDatabase, "Books", ItemDataCollection);
        public static readonly MemberSpec ItemDatabaseMiscs = MemberSpec.Property(ItemDatabase, "Miscs", ItemDataCollection);
        public static readonly MemberSpec ItemDatabaseSpecial = MemberSpec.Property(ItemDatabase, "Special", ItemDataCollection);

        /// <summary>Everything the item tabs read or observe by name. A gap disables only the item features.</summary>
        public static readonly IReadOnlyList<MemberSpec> ItemReaders = new[]
        {
            ItemAddCount, ItemSetCount, ItemId, ItemImage, ItemCurrentCount, ItemTypeProp, ItemNameKey, ItemDescKey, ItemTitleKey,
            ItemDatabaseInstance, ItemDatabaseBooks, ItemDatabaseMiscs, ItemDatabaseSpecial,
        };

        /// <summary>
        /// Sprite of the Story scene's event presentation frame (MainUI/Layer_5/Picture, an open book, 1254x1080). Not a
        /// member: it is found by name among loaded sprites while a Story scene is alive and held by reference only.
        /// </summary>
        public const string BookSpriteName = "pic_book_1";

        // ---- library (도감) UI: the Jangseoru tab is built inside LibraryPanel from clones of its own objects ----

        public const string LibraryPanel = "Mortal.Core.LibraryPanel";
        public const string CommonPanel = "Mortal.Core.CommonPanel";
        public const string InputButton = "Mortal.Core.InputButton";
        public const string LibrarySystem = "Mortal.Core.LibrarySystem";
        public const string LibraryItemCollection = "Mortal.Core.LibraryItemCollection";
        public const string CollectionData = "Mortal.Core.CollectionData`1";
        public const string LibraryItemData = "Mortal.Core.LibraryItemData";
        public const string Addressables = "UnityEngine.AddressableAssets.Addressables";
        public const string AsyncOperationHandle = "UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle`1";
        public const string AsyncOperationStatus = "UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus";
        public const string LeanLocalizedText = "Lean.Localization.LeanLocalizedText";

        public static readonly MemberSpec LibraryOnPanelOpen = MemberSpec.Method(LibraryPanel, "OnPanelOpen", "System.Void");
        public static readonly MemberSpec LibraryOpenEndPanel = MemberSpec.Method(LibraryPanel, "OpenEndPanel", "System.Void");
        public static readonly MemberSpec LibraryOpenDeadPanel = MemberSpec.Method(LibraryPanel, "OpenDeadPanel", "System.Void");
        public static readonly MemberSpec LibraryOpenAchivementPanel = MemberSpec.Method(LibraryPanel, "OpenAchivementPanel", "System.Void");
        public static readonly MemberSpec LibraryOpenLegendPanel = MemberSpec.Method(LibraryPanel, "OpenLegendPanel", "System.Void");
        public static readonly MemberSpec LibraryCloseButtonClick = MemberSpec.Method(LibraryPanel, "CloseButtonClick", "System.Void");

        public static readonly MemberSpec LibraryInputCloseButton = MemberSpec.Field(LibraryPanel, "_inputCloseButton", InputButton);
        public static readonly MemberSpec LibraryEndPanel = MemberSpec.Field(LibraryPanel, "_endPanel", CommonPanel);
        public static readonly MemberSpec LibraryDeadPanel = MemberSpec.Field(LibraryPanel, "_deadPanel", CommonPanel);
        public static readonly MemberSpec LibraryAchievementPanel = MemberSpec.Field(LibraryPanel, "_achievementPanel", CommonPanel);
        public static readonly MemberSpec LibraryLegendPanel = MemberSpec.Field(LibraryPanel, "_legendPanel", CommonPanel);
        public static readonly MemberSpec LibraryFatePanel = MemberSpec.Field(LibraryPanel, "_fatePanel", "UnityEngine.GameObject");

        public static readonly MemberSpec CommonPanelShow = MemberSpec.Method(CommonPanel, "Show", "System.Void", "System.Boolean");
        public static readonly MemberSpec InputButtonSetActive = MemberSpec.Method(InputButton, "SetActive", "System.Void", "System.Boolean");
        public static readonly MemberSpec InputButtonActionPath = MemberSpec.Field(InputButton, "_actionPath", "System.String");

        public static readonly MemberSpec LibrarySystemInstance = MemberSpec.Property(LibrarySystem, "Instance", LibrarySystem);
        public static readonly MemberSpec LibrarySystemEndCg = MemberSpec.Property(LibrarySystem, "EndCg", LibraryItemCollection);
        public static readonly MemberSpec CollectionDataList = MemberSpec.Property(CollectionData, "List", "System.Collections.Generic.List`1[T]");
        public static readonly MemberSpec LibraryItemPicture = MemberSpec.Property(LibraryItemData, "Picture", "UnityEngine.Sprite");
        public static readonly MemberSpec LibraryItemCount = MemberSpec.Property(LibraryItemData, "Count", "System.Int32");

        public static readonly MemberSpec AddressablesLoadAssetAsync =
            MemberSpec.GenericMethod(Addressables, "LoadAssetAsync", 1, AsyncOperationHandle + "[TObject]", "System.Object");
        public static readonly MemberSpec AddressablesRelease =
            MemberSpec.GenericMethod(Addressables, "Release", 1, "System.Void", AsyncOperationHandle + "[TObject]");
        public static readonly MemberSpec HandleIsDone = MemberSpec.Property(AsyncOperationHandle, "IsDone", "System.Boolean");
        public static readonly MemberSpec HandleStatus = MemberSpec.Property(AsyncOperationHandle, "Status", AsyncOperationStatus);
        public static readonly MemberSpec HandleResult = MemberSpec.Property(AsyncOperationHandle, "Result", "TObject");
        public static readonly MemberSpec HandleIsValid = MemberSpec.Method(AsyncOperationHandle, "IsValid", "System.Boolean");
        public static readonly MemberSpec StatusSucceeded = MemberSpec.EnumValue(AsyncOperationStatus, "Succeeded");
        public static readonly MemberSpec LeanLocalizedTextType = MemberSpec.TypeExists(LeanLocalizedText);

        /// <summary>Everything the Jangseoru library tab touches by name.</summary>
        public static readonly IReadOnlyList<MemberSpec> LibraryUi = new[]
        {
            LibraryOnPanelOpen, LibraryOpenEndPanel, LibraryOpenDeadPanel, LibraryOpenAchivementPanel, LibraryOpenLegendPanel, LibraryCloseButtonClick,
            LibraryInputCloseButton, LibraryEndPanel, LibraryDeadPanel, LibraryAchievementPanel, LibraryLegendPanel, LibraryFatePanel,
            CommonPanelShow, InputButtonSetActive, InputButtonActionPath,
            LibrarySystemInstance, LibrarySystemEndCg, CollectionDataList, LibraryItemPicture, LibraryItemCount,
            AddressablesLoadAssetAsync, AddressablesRelease, HandleIsDone, HandleStatus, HandleResult, HandleIsValid, StatusSucceeded,
            LeanLocalizedTextType,
        };

        // ---- 맹세 detail: the description text, read the way CgPanel reads it (read-only; CgPanel itself is never called) ----

        public const string LocalizationManager = "Mortal.Core.LocalizationManager";
        public const string LocaleResolver = "Mortal.Core.ILocaleResolver";

        /// <summary>"Library/Desc/" + asset name.</summary>
        public static readonly MemberSpec LibraryItemGetDescKey = MemberSpec.Method(LibraryItemData, "GetDescKey", "System.String");
        public static readonly MemberSpec LocalizationManagerInstance = MemberSpec.Property(LocalizationManager, "Instance", LocalizationManager);
        public static readonly MemberSpec LocalizationManagerLocaleResolver = MemberSpec.Property(LocalizationManager, "LocaleResolver", LocaleResolver);
        public static readonly MemberSpec LocaleResolverGetString = MemberSpec.Method(LocaleResolver, "GetString", "System.String", "System.String");

        /// <summary>Everything the 맹세 detail presentation reads by name. A gap disables only the detail, not the tab.</summary>
        public static readonly IReadOnlyList<MemberSpec> OathDetail = new[]
        {
            LibraryItemGetDescKey, LocalizationManagerInstance, LocalizationManagerLocaleResolver, LocaleResolverGetString,
        };

        public static readonly IReadOnlyList<MemberSpec> Required = new[]
        {
            SetPlayer, ResetData, ExecuteSaveData, GetSaveData, GetAutoSaveData, ExecuteLoadGameData, GetSaveDataFile,
            GameSaveTimeTick, GameSaveStats, StatSaveKey, StatSaveValue, AutoGameSaveGameSave,
            PlayerInstance, PlayerStats, StatsGet, StatValue, TitleStatType,
            ShowPicture, SetupEffect, CombatOverlayShow,
        }.Concat(PortraitReaders).Concat(ItemReaders).Concat(LibraryUi).Concat(OathDetail).Concat(InyeonContract.Required).Concat(MediaContract.Required).Concat(UiContract.Required).ToArray();
    }
}
