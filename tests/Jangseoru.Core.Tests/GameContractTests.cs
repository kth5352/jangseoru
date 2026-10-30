using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Jangseoru.Core.Tests
{
    /// <summary>
    /// Verifies, without executing any game code, that every game member the plugin depends on exists with the
    /// expected shape in the real game assemblies. Uses MetadataLoadContext over Mortal_Data/Managed
    /// (override with JANGSEORU_GAMEDIR to check another install).
    /// </summary>
    public class GameContractTests
    {
        public static IEnumerable<object[]> Specs => GameContract.Required.Select(s => new object[] { s });

        [Fact]
        public void Inyeon_observes_display_and_final_load_contracts()
        {
            Assert.Contains(InyeonContract.PortraitShow, GameContract.Required);
            Assert.Contains(InyeonContract.SocialUpdate, GameContract.Required);
            Assert.Contains(InyeonContract.RelationshipActive, GameContract.Required);
        }

        [Theory]
        [MemberData(nameof(Specs))]
        public void Game_member_exists_with_the_expected_shape(MemberSpec spec)
        {
            using (var ctx = OpenGame())
            {
                Assert.True(Check(ctx, spec), "missing or different in game assemblies: " + spec);
            }
        }

        [Fact]
        public void Checker_rejects_members_that_do_not_exist()
        {
            using (var ctx = OpenGame())
            {
                Assert.False(Check(ctx, MemberSpec.Method("Mortal.Core.SaveSystem", "NoSuchMethod", "System.Void")));
                Assert.False(Check(ctx, MemberSpec.Method("Mortal.Core.SaveSystem", "GetSaveData", "Mortal.Core.GameSave", "System.Int32")));
                Assert.False(Check(ctx, MemberSpec.Method("Mortal.Core.NoSuchType", "X", "System.Void")));
                Assert.False(Check(ctx, MemberSpec.Field("Mortal.Core.GameSave", "NoSuchField", "System.Int64")));
                Assert.False(Check(ctx, MemberSpec.Field("Mortal.Core.GameSave", "TimeTick", "System.Int32")));
                Assert.False(Check(ctx, MemberSpec.EnumValue("Mortal.Core.GameStatType", "NoSuchStat")));
                Assert.False(Check(ctx, MemberSpec.TypeExists("Lean.Localization.NoSuchType")));
                // Generic arity must match: the generic LoadAssetAsync is not found as a plain method, and vice versa.
                Assert.False(Check(ctx, MemberSpec.Method(GameContract.Addressables, "LoadAssetAsync",
                    GameContract.AsyncOperationHandle + "[TObject]", "System.Object")));
                Assert.False(Check(ctx, MemberSpec.GenericMethod("Mortal.Core.CommonPanel", "Show", 1, "System.Void", "System.Boolean")));
            }
        }

        [Fact]
        public void Contract_lists_every_library_ui_member_the_plan_names()
        {
            var text = string.Join("\n", GameContract.Required.Select(s => s.ToString()));
            foreach (var needle in new[]
            {
                "Mortal.Core.LibraryPanel.OnPanelOpen()",
                "Mortal.Core.LibraryPanel.OpenEndPanel()",
                "Mortal.Core.LibraryPanel.OpenDeadPanel()",
                "Mortal.Core.LibraryPanel.OpenAchivementPanel()",
                "Mortal.Core.LibraryPanel.OpenLegendPanel()",
                "Mortal.Core.LibraryPanel._inputCloseButton",
                "Mortal.Core.LibraryPanel._endPanel",
                "Mortal.Core.LibraryPanel._deadPanel",
                "Mortal.Core.LibraryPanel._achievementPanel",
                "Mortal.Core.LibraryPanel._legendPanel",
                "Mortal.Core.LibraryPanel._fatePanel",
                "Mortal.Core.CommonPanel.Show(System.Boolean)",
                "Mortal.Core.LibrarySystem.EndCg",
                "UnityEngine.AddressableAssets.Addressables.LoadAssetAsync`1(System.Object)",
                "UnityEngine.AddressableAssets.Addressables.Release`1(",
            })
            {
                Assert.Contains(needle, text);
            }
        }

        [Fact]
        public void Contract_lists_every_hook_target_the_plan_names()
        {
            var text = string.Join("\n", GameContract.Required.Select(s => s.ToString()));
            foreach (var needle in new[]
            {
                "Mortal.Story.StatModifyManager.SetPlayer",
                "Mortal.Core.PlayerStatManagerData.ResetData",
                "Mortal.Core.SaveSystem.ExecuteSaveData",
                "Mortal.Core.SaveSystem.GetSaveData(System.String)",
                "Mortal.Core.SaveSystem.GetAutoSaveData",
                "Mortal.Core.SaveSystem.ExecuteLoadGameData",
                "Mortal.Core.SaveSystem.GetSaveDataFile",
                "Mortal.Core.GameSave.TimeTick",
                "Mortal.Core.StatSave.Key",
            })
            {
                Assert.Contains(needle, text);
            }
        }

        [Fact]
        public void Contract_lists_the_oath_detail_readers()
        {
            var text = string.Join("\n", GameContract.Required.Select(s => s.ToString()));
            foreach (var needle in new[]
            {
                "Mortal.Core.LibraryItemData.GetDescKey():System.String",
                "Mortal.Core.LocalizationManager.Instance:Mortal.Core.LocalizationManager",
                "Mortal.Core.LocalizationManager.LocaleResolver:Mortal.Core.ILocaleResolver",
                "Mortal.Core.ILocaleResolver.GetString(System.String):System.String",
            })
            {
                Assert.Contains(needle, text);
            }
            Assert.All(GameContract.OathDetail, s => Assert.Contains(s, GameContract.Required));
        }

        [Fact]
        public void Contract_lists_item_members()
        {
            var text = string.Join("\n", GameContract.Required.Select(s => s.ToString()));
            Assert.Contains("Mortal.Core.ItemData.AddCount(System.Int32):System.Void", text);
            Assert.Contains("Mortal.Core.ItemData.SetCount(System.Int32):System.Void", text);
            Assert.Contains("Mortal.Core.ItemDatabase.Instance:Mortal.Core.ItemDatabase", text);
            Assert.Contains("Mortal.Core.ItemDatabase.Books:Mortal.Core.ItemDataCollection", text);
            Assert.Contains("Mortal.Core.ItemData.GetNameKey():System.String", text);
            Assert.Contains("Mortal.Core.ItemData.Image:UnityEngine.Sprite", text);
            Assert.Contains("Mortal.Core.ItemData.ItemType:Mortal.Core.GameItemType", text);
            Assert.All(GameContract.ItemReaders, s => Assert.Contains(s, GameContract.Required));
        }

        [Fact]
        public void Contract_lists_SetupEffect()
        {
            var text = string.Join("\n", GameContract.Required.Select(s => s.ToString()));
            Assert.Contains("Mortal.Story.StoryEffectsController.SetupEffect(System.String,System.Single,System.Single,System.Single,System.Single,System.Int32,System.Int32)", text);
        }

        [Fact]
        public void Contract_lists_ShowPicture()
        {
            var text = string.Join("\n", GameContract.Required.Select(s => s.ToString()));
            Assert.Contains("Mortal.Story.StoryMainUI.ShowPicture(System.String)", text);
        }

        // ---- helpers ----

        private static MetadataLoadContext OpenGame()
        {
            var managed = Path.Combine(FindGameDir(), "Mortal_Data", "Managed");
            var resolver = new PathAssemblyResolver(Directory.GetFiles(managed, "*.dll"));
            var ctx = new MetadataLoadContext(resolver, "mscorlib");
            return ctx;
        }

        private static string FindGameDir()
        {
            var env = Environment.GetEnvironmentVariable("JANGSEORU_GAMEDIR");
            if (!string.IsNullOrEmpty(env)) return env;

            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, "LegendOfMortal");
                if (Directory.Exists(Path.Combine(candidate, "Mortal_Data", "Managed"))) return candidate;
            }
            throw new DirectoryNotFoundException("Game folder not found. Set JANGSEORU_GAMEDIR to the LegendOfMortal folder.");
        }

        private static Type FindType(MetadataLoadContext ctx, string fullName)
        {
            foreach (var asm in ctx.GetAssemblies().Concat(LoadGameAssemblies(ctx)))
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }

        private static IEnumerable<Assembly> LoadGameAssemblies(MetadataLoadContext ctx)
        {
            foreach (var name in new[]
                     {
                         "Mortal.Core", "Mortal.Story", "Mortal.Combat", "Assembly-CSharp", "UnityEngine.CoreModule",
                         "Unity.Addressables", "Unity.ResourceManager", "LeanLocalization", "Fungus", "OBB.Framework",
                     })
            {
                Assembly asm = null;
                try { asm = ctx.LoadFromAssemblyName(name); } catch { }
                if (asm != null) yield return asm;
            }
        }

        private const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        /// <summary>Assembly-neutral type name: generics are written as Def`1[Arg] without assembly qualifiers.</summary>
        private static string TypeName(Type t)
            => t.IsGenericParameter ? t.Name
            : t.IsGenericType
                ? t.GetGenericTypeDefinition().FullName + "[" + string.Join(",", t.GetGenericArguments().Select(TypeName)) + "]"
                : t.FullName;

        private static bool Check(MetadataLoadContext ctx, MemberSpec spec)
        {
            var type = FindType(ctx, spec.DeclaringType);
            if (type == null) return false;

            switch (spec.Kind)
            {
                case MemberKind.Method:
                    return type.GetMethods(All).Any(m =>
                        m.Name == spec.Name
                        && (m.IsGenericMethodDefinition ? m.GetGenericArguments().Length : 0) == spec.GenericArity
                        && (spec.ValueType == null || TypeName(m.ReturnType) == spec.ValueType)
                        && m.GetParameters().Select(p => TypeName(p.ParameterType)).SequenceEqual(spec.ParameterTypes));

                case MemberKind.Type:
                    return true;

                case MemberKind.Field:
                    var field = type.GetField(spec.Name, All);
                    return field != null && (spec.ValueType == null || TypeName(field.FieldType) == spec.ValueType);

                case MemberKind.Property:
                    var prop = type.GetProperty(spec.Name, All);
                    return prop != null && (spec.ValueType == null || TypeName(prop.PropertyType) == spec.ValueType);

                case MemberKind.EnumValue:
                    return type.IsEnum && type.GetField(spec.Name, All) != null;

                default:
                    return false;
            }
        }
    }
}
