using System.Collections.Generic;

namespace Jangseoru.Core
{
    /// <summary>Inyeon's complete read/observe boundary; included in GameContract.Required.</summary>
    public static class InyeonContract
    {
        public const string Relation = "Mortal.Core.RelationshipStat";
        public const string Social = "Mortal.Core.SocialStatPanel";
        public const string Missions = "Mortal.Core.MissionManagerData";
        public const string Mapping = "OBB.Framework.Data.StoryMappingItem";
        public const string Cache = "Mortal.Core.AssetReferenceCache`1";
        public static readonly MemberSpec PortraitShow = MemberSpec.Method("Fungus.PortraitController", "Show", "System.Void", "Fungus.PortraitOptions");
        public static readonly MemberSpec OptionsCharacter = MemberSpec.Field("Fungus.PortraitOptions", "character", "Fungus.Character");
        public static readonly MemberSpec CharacterState = MemberSpec.Property("Fungus.Character", "State", "Fungus.PortraitState");
        public static readonly MemberSpec StateShown = MemberSpec.Field("Fungus.PortraitState", "onScreen", "System.Boolean");
        public static readonly MemberSpec StatePortrait = MemberSpec.Property("Fungus.PortraitState", "portrait", "UnityEngine.Sprite");
        public static readonly MemberSpec CharacterId = MemberSpec.Property(GameContract.StoryCharacterData, "Id", "System.String");
        public static readonly MemberSpec PortraitNames = MemberSpec.Field(GameContract.StoryCharacterController, "_dicPortrait", "System.Collections.Generic.Dictionary`2[System.String,System.String]");
        public static readonly MemberSpec OriginalPortrait = MemberSpec.Field(GameContract.StoryCharacterController, "_originPortrait", "System.String");
        public static readonly MemberSpec PortraitMapping = MemberSpec.Property(GameContract.StoryCharaterImageItem, "Mapping", Mapping);
        public static readonly MemberSpec MappingValue = MemberSpec.Field(Mapping, "Value", "System.String");
        public static readonly MemberSpec SocialUpdate = MemberSpec.Method(Social, "UpdateInfo", "System.Void");
        public static readonly MemberSpec SocialCurrent = MemberSpec.Field(Social, "_currentData", Relation);
        public static readonly MemberSpec SocialAvatar = MemberSpec.Field(Social, "_avatarImage", "UnityEngine.UI.Image");
        public static readonly MemberSpec RelationshipActive = MemberSpec.Method(Relation, "SetActive", "System.Void", "System.Boolean");
        public static readonly MemberSpec RelationshipSet = MemberSpec.Method(Relation, "SetValue", "System.Void", "System.Int32");
        public static readonly MemberSpec RelationshipAdd = MemberSpec.Method(Relation, "AddValue", "System.Void", "System.Int32");
        public static readonly MemberSpec RelationType = MemberSpec.Property(Relation, "Type", "Mortal.Core.RelationshipStatType");
        public static readonly MemberSpec RelationValue = MemberSpec.Property(Relation, "Value", "System.Int32");
        public static readonly MemberSpec RelationActive = MemberSpec.Property(Relation, "Active", "System.Boolean");
        public static readonly MemberSpec RelationHead = MemberSpec.Property(Relation, "HeadIcon", "UnityEngine.Sprite");
        public static readonly MemberSpec RelationAvatar = MemberSpec.Property(Relation, "AvatarReference", Cache + "[UnityEngine.Sprite]");
        public static readonly MemberSpec CacheReference = MemberSpec.Property(Cache, "Reference", "UnityEngine.AddressableAssets.AssetReference");
        public static readonly MemberSpec ReferenceKey = MemberSpec.Property("UnityEngine.AddressableAssets.AssetReference", "RuntimeKey", "System.Object");
        public static readonly MemberSpec PlayerRelations = MemberSpec.Property(GameContract.PlayerStatManagerData, "Relationships", "Mortal.Core.RelationshipData");
        public static readonly MemberSpec MissionInstance = MemberSpec.Property(Missions, "Instance", Missions);
        public static readonly MemberSpec GetFlag = MemberSpec.Method(Missions, "GetFlag", "Mortal.Core.FlagData", "System.String");
        public static readonly MemberSpec FlagState = MemberSpec.Field("Mortal.Core.FlagData", "State", "System.Int32");
        public static readonly MemberSpec SetFlag = MemberSpec.Method(Missions, "SetFlag", "System.Void", "System.String", "System.Int32");
        public static readonly MemberSpec NewGame = MemberSpec.Method(GameContract.SaveSystem, "NewGameData", "System.Void");
        public static readonly MemberSpec NewAct = MemberSpec.Method(GameContract.SaveSystem, "NewActGameData", "System.Void");
        public static readonly IReadOnlyList<MemberSpec> Required = new[]
        {
            PortraitShow, OptionsCharacter, CharacterState, StateShown, StatePortrait, CharacterId, PortraitNames, OriginalPortrait,
            PortraitMapping, MappingValue, SocialUpdate, SocialCurrent, SocialAvatar, RelationshipActive, RelationshipSet, RelationshipAdd,
            RelationType, RelationValue, RelationActive, RelationHead, RelationAvatar, CacheReference, ReferenceKey, PlayerRelations,
            MissionInstance, GetFlag, FlagState, SetFlag, NewGame, NewAct,
        };
    }
}
