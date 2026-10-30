using System;
using System.Reflection;
using HarmonyLib;
using Jangseoru.Core;

namespace Jangseoru.Plugin
{
    internal static class InyeonHooks
    {
        private static InyeonGameAccess access;
        private static InyeonRecorder recorder;
        private static ILog log;
        private static bool portraits, biographies, social;

        public static void Disable() { portraits = biographies = social = false; }

        public static void Install(Harmony harmony, InyeonFile file, InyeonGameAccess readers, ILog logger)
        {
            access = readers;
            recorder = new InyeonRecorder(file);
            log = logger;
            portraits = readers.PortraitReaders && Patch(harmony, InyeonContract.PortraitShow, nameof(PortraitPostfix));
            // Without every reset/load boundary, no relationship observer may run.
            biographies = readers.BiographyReaders
                && Patch(harmony, GameContract.ResetData, nameof(ResetPostfix))
                && Patch(harmony, GameContract.ExecuteLoadGameData, nameof(ReadyPostfix))
                && Patch(harmony, InyeonContract.NewGame, nameof(ReadyPostfix))
                && Patch(harmony, InyeonContract.NewAct, nameof(ReadyPostfix))
                && Patch(harmony, InyeonContract.RelationshipActive, nameof(RelationPostfix))
                && Patch(harmony, InyeonContract.RelationshipSet, nameof(RelationPostfix))
                && Patch(harmony, InyeonContract.RelationshipAdd, nameof(RelationPostfix))
                && Patch(harmony, InyeonContract.SetFlag, nameof(FlagPostfix));
            social = readers.SocialReaders && Patch(harmony, InyeonContract.SocialUpdate, nameof(SocialPostfix));
            log.Info("inyeon self-check: portraits=" + (portraits ? "ON" : "OFF") + " biographies=" + (biographies ? "ON" : "OFF")
                + " social=" + (social ? "ON" : "OFF"));
        }
        private static bool Patch(Harmony harmony, MemberSpec target, string method)
            => SafePatcher.TryPostfix(harmony, access.Method(target), typeof(InyeonHooks).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic),
                "inyeon " + target.Name, log);
        private static void PortraitPostfix(object options)
        {
            try { if (portraits && access.TryShownPortrait(options, out var id, out var key)) recorder.Portrait(id, key); }
            catch (Exception e) { portraits = false; Warn("portrait collection", e); }
        }
        private static void ResetPostfix(object __instance)
        {
            try { if (biographies && access.IsPlayer(__instance)) recorder.Reset(); }
            catch (Exception e) { biographies = false; Warn("biography collection", e); }
        }
        private static void ReadyPostfix()
        {
            try { if (biographies) { recorder.BeginPlay(); SampleAll(); } }
            catch (Exception e) { biographies = false; Warn("biography collection", e); }
        }
        private static void RelationPostfix(object __instance)
        {
            try { if (biographies && access.IsLiveRelationship(__instance)) Sample(__instance); }
            catch (Exception e) { biographies = false; Warn("biography collection", e); }
        }
        private static void FlagPostfix(string name)
        {
            try { if (biographies && name == "Prologue") SampleAll(); }
            catch (Exception e) { biographies = false; Warn("biography collection", e); }
        }
        private static void SocialPostfix(object __instance)
        {
            try
            {
                if (!social) return;
                if (access.TrySocialPortrait(__instance, out var id, out var address)) recorder.Portrait(id, address);
                if (biographies) Sample(access.SocialRelationship(__instance));
            }
            catch (Exception e) { social = false; Warn("social collection", e); }
        }
        private static void SampleAll()
        {
            foreach (var relation in access.Relationships()) Sample(relation);
        }
        private static void Sample(object relation)
        {
            if (relation != null && access.TryRelation(relation, out var type, out var value, out var active)
                && access.TryPrologue(out var prologue)) recorder.Relationship(type, value, active, prologue);
        }
        private static void Warn(string feature, Exception error)
        { try { log?.Warn("inyeon " + feature + " disabled: " + error.GetType().Name); } catch { } }
    }
}
