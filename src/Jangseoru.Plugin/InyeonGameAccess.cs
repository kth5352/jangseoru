using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Jangseoru.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Jangseoru.Plugin
{
    /// <summary>Cached, reflection-only readers for Inyeon. None of these readers load a save or alter game data.</summary>
    internal sealed class InyeonGameAccess
    {
        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private readonly Dictionary<MemberSpec, MemberInfo> members = new Dictionary<MemberSpec, MemberInfo>();
        private readonly Type controllerType;
        private readonly ILog log;
        private readonly HashSet<string> unknownStory = new HashSet<string>(StringComparer.Ordinal);

        private InyeonGameAccess(ILog log)
        {
            this.log = log;
            foreach (var name in new[] { "Mortal.Core", "Mortal.Story", "Fungus", "OBB.Framework", "Unity.Addressables" })
                try { Assembly.Load(name); } catch { }
            controllerType = FindType(GameContract.StoryCharacterController);
            foreach (var spec in InyeonContract.Required.Concat(new[] { GameContract.CharacterControllerData, GameContract.CharacterPortraitList,
                GameContract.PortraitAddressKey, GameContract.PlayerInstance, GameContract.CollectionDataList, GameContract.ResetData,
                GameContract.ExecuteLoadGameData }))
            {
                try { members[spec] = Resolve(spec); }
                catch { members[spec] = null; }
            }
        }

        public static InyeonGameAccess Create(ILog log) => new InyeonGameAccess(log);
        public MethodBase Method(MemberSpec spec) => members.TryGetValue(spec, out var value) ? value as MethodBase : null;
        private bool Has(params MemberSpec[] specs) => specs.All(s => members.TryGetValue(s, out var m) && m != null);
        public bool PortraitReaders => controllerType != null && Has(InyeonContract.OptionsCharacter, InyeonContract.CharacterState,
            InyeonContract.StateShown, InyeonContract.StatePortrait, InyeonContract.CharacterId, InyeonContract.PortraitNames,
            InyeonContract.OriginalPortrait, InyeonContract.PortraitMapping, InyeonContract.MappingValue,
            GameContract.CharacterControllerData, GameContract.CharacterPortraitList, GameContract.PortraitAddressKey);
        public bool BiographyReaders => Has(GameContract.PlayerInstance, InyeonContract.PlayerRelations, GameContract.CollectionDataList,
            InyeonContract.RelationType, InyeonContract.RelationValue, InyeonContract.RelationActive, InyeonContract.MissionInstance,
            InyeonContract.GetFlag, InyeonContract.FlagState);
        public bool SocialReaders => BiographyReaders && Has(InyeonContract.SocialCurrent, InyeonContract.SocialAvatar,
            InyeonContract.RelationAvatar, InyeonContract.CacheReference, InyeonContract.ReferenceKey);

        private object Read(MemberSpec spec, object instance = null)
        {
            if (!members.TryGetValue(spec, out var info) || info == null) return null;
            if (info is PropertyInfo property) return property.GetValue(instance, null);
            return info is FieldInfo field ? field.GetValue(instance) : null;
        }

        public bool TryShownPortrait(object options, out string id, out string key)
        {
            id = key = null;
            var character = Read(InyeonContract.OptionsCharacter, options);
            if (character == null || !controllerType.IsInstanceOfType(character)) return false;
            var state = Read(InyeonContract.CharacterState, character);
            if (state == null || !Equals(Read(InyeonContract.StateShown, state), true)) return false;
            var sprite = Read(InyeonContract.StatePortrait, state) as Sprite;
            if (sprite == null) return false;
            var data = Read(GameContract.CharacterControllerData, character);
            if (data == null) return false;
            id = Read(InyeonContract.CharacterId, data) as string;
            var canonical = InyeonCatalog.ResolveStory(id);
            if (canonical == null)
            {
                // A character the catalog has never heard of (new content of a game update): never guessed, never recorded.
                if (id != null && unknownStory.Add(id)) { try { log?.Info("inyeon: story character '" + id + "' is not in the catalog; its portraits are not recorded"); } catch { } }
                return false;
            }
            var loaded = Read(InyeonContract.PortraitNames, character) as IDictionary;
            var original = Read(InyeonContract.OriginalPortrait, character) as string;
            var list = Read(GameContract.CharacterPortraitList, data) as IEnumerable;
            if (loaded == null || list == null) return false;
            var candidates = new List<string>();
            foreach (var item in list)
            {
                var mapping = Read(InyeonContract.PortraitMapping, item);
                var alias = mapping == null ? null : Read(InyeonContract.MappingValue, mapping) as string;
                if (alias == null || !loaded.Contains(alias) || !Equals(loaded[alias], sprite.name)) continue;
                var address = Read(GameContract.PortraitAddressKey, item) as string;
                var path = InyeonCatalog.ResolveAddress(address) ?? address;
                if (!InyeonCatalog.Accepts(canonical, path)) continue;
                if (alias == original) { key = path; return true; }
                if (!candidates.Contains(path)) candidates.Add(path);
            }
            // With no explicit request, a unique loaded sprite-to-address match is required.
            if (candidates.Count != 1) return false;
            key = candidates[0];
            return true;
        }

        public IEnumerable<object> Relationships()
        {
            var player = Read(GameContract.PlayerInstance);
            var data = player == null ? null : Read(InyeonContract.PlayerRelations, player);
            var list = data == null ? null : Read(GameContract.CollectionDataList, data) as IEnumerable;
            return list == null ? new object[0] : list.Cast<object>().Where(x => x != null).ToArray();
        }
        public bool IsPlayer(object instance) => ReferenceEquals(instance, Read(GameContract.PlayerInstance));
        public bool IsLiveRelationship(object instance) => Relationships().Any(r => ReferenceEquals(instance, r));
        public bool TryPrologue(out bool prologue)
        {
            prologue = true; // never guess the more permissive state when readers fail
            var mission = Read(InyeonContract.MissionInstance);
            if (mission == null || !(Method(InyeonContract.GetFlag) is MethodInfo get)) return false;
            var flag = get.Invoke(mission, new object[] { "Prologue" });
            if (flag == null || !(Read(InyeonContract.FlagState, flag) is int state)) return false;
            prologue = state == 1;
            return true;
        }
        public bool TryRelation(object instance, out int type, out int value, out bool active)
        {
            type = value = -1;
            active = false;
            var rawType = Read(InyeonContract.RelationType, instance);
            if (rawType == null || !(Read(InyeonContract.RelationValue, instance) is int v)
                || !(Read(InyeonContract.RelationActive, instance) is bool a)) return false;
            type = Convert.ToInt32(rawType);
            value = v;
            active = a;
            return true;
        }
        public object SocialRelationship(object panel) => Read(InyeonContract.SocialCurrent, panel);
        public bool TrySocialPortrait(object panel, out string id, out string address)
        {
            id = address = null;
            var relation = SocialRelationship(panel);
            var image = Read(InyeonContract.SocialAvatar, panel) as Image;
            if (relation == null || image == null || image.sprite == null || !image.gameObject.activeInHierarchy
                || !TryRelation(relation, out var type, out _, out var active) || !active) return false;
            var identity = InyeonCatalog.Relationship(type);
            if (identity == null) return false;
            var cache = Read(InyeonContract.RelationAvatar, relation);
            var reference = cache == null ? null : Read(InyeonContract.CacheReference, cache);
            address = reference == null ? null : InyeonCatalog.ResolveAddress(Read(InyeonContract.ReferenceKey, reference)?.ToString());
            id = identity.CharacterId;
            return InyeonCatalog.Accepts(id, address);
        }
        public Dictionary<string, Sprite> HeadIcons()
        {
            var result = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            try
            {
                foreach (var relation in Relationships())
                {
                    if (!TryRelation(relation, out var type, out _, out _)) continue;
                    var id = InyeonCatalog.Relationship(type)?.CharacterId;
                    var sprite = Read(InyeonContract.RelationHead, relation) as Sprite;
                    if (id != null && sprite != null && !result.ContainsKey(id)) result.Add(id, sprite);
                }
            }
            catch (Exception e) { try { log?.Warn("inyeon head icons unavailable: " + e.GetType().Name); } catch { } }
            return result;
        }

        private MemberInfo Resolve(MemberSpec spec)
        {
            var type = FindType(spec.DeclaringType);
            if (type == null) return null;
            if (type.IsGenericTypeDefinition)
            {
                if (spec.DeclaringType == InyeonContract.Cache) type = type.MakeGenericType(typeof(Sprite));
                else if (spec == GameContract.CollectionDataList) type = type.MakeGenericType(FindType(InyeonContract.Relation));
            }
            if (spec.Kind == MemberKind.Field) return type.GetField(spec.Name, All);
            if (spec.Kind == MemberKind.Property) return type.GetProperty(spec.Name, All);
            if (spec.Kind == MemberKind.Method)
                return type.GetMethods(All).FirstOrDefault(m => m.Name == spec.Name
                    && m.GetParameters().Select(p => p.ParameterType.FullName).SequenceEqual(spec.ParameterTypes));
            return null;
        }
        private static Type FindType(string name)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = asm.GetType(name, false);
                if (type != null) return type;
            }
            return null;
        }
    }
}
