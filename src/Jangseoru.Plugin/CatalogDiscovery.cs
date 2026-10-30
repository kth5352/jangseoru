using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using Jangseoru.Core;
using UnityEngine;

namespace Jangseoru.Plugin
{
    /// <summary>
    /// Finds the game's pictures, backgrounds, big pictures and videos in its Addressables catalog (read-only, through reflection) and
    /// adds those the built-in lists lack, so content added by a game update appears without a new mod build. The built-in lists stay
    /// as they are; this only adds. Addressables may not be ready when the plugin starts (the game initializes it on its first load), so it keeps
    /// trying, every two seconds at first and then every ten, until the catalog answers. Any failure switches it off and leaves the built-in lists.
    /// </summary>
    internal static class CatalogDiscovery
    {
        private const float FastInterval = 2f, SlowInterval = 10f;
        private const int FastAttempts = 30; // two seconds apart; after that ten seconds apart, with no end
        private const string Root = "Assets/__Project/";
        private static ILog log;
        private static bool done, off;
        private static int attempts;
        private static float next;

        public static void Install(ILog logger) { log = logger; }

        public static void Disable() { off = true; }

        /// <summary>Called from the plugin's Update; does real work at most every two seconds and only until it has succeeded.</summary>
        public static void Tick()
        {
            if (done || off) return;
            try
            {
                var now = Time.unscaledTime;
                if (now < next) return;
                attempts++;
                next = now + (attempts < FastAttempts ? FastInterval : SlowInterval);
                if (TryDiscover()) done = true;
            }
            catch (Exception e)
            {
                off = true;
                Warn("catalog discovery disabled (" + e.GetType().Name + "); the built-in lists stay");
            }
        }

        private static Type Find(string fullName, string assembly)
        {
            var t = AccessTools.TypeByName(fullName);
            if (t != null) return t;
            try { return Assembly.Load(assembly).GetType(fullName, false); }
            catch (Exception) { return null; }
        }

        private static bool TryDiscover()
        {
            var addressables = Find("UnityEngine.AddressableAssets.Addressables", "Unity.Addressables");
            var locatorType = Find("UnityEngine.AddressableAssets.ResourceLocators.IResourceLocator", "Unity.Addressables");
            var locationType = Find("UnityEngine.ResourceManagement.ResourceLocations.IResourceLocation", "Unity.ResourceManager");
            var locatorsProperty = addressables?.GetProperty("ResourceLocators", BindingFlags.Public | BindingFlags.Static);
            var keysProperty = locatorType?.GetProperty("Keys");
            var locate = locatorType?.GetMethod("Locate");
            var idProperty = locationType?.GetProperty("InternalId");
            var typeProperty = locationType?.GetProperty("ResourceType");
            if (locatorsProperty == null || keysProperty == null || locate == null || idProperty == null || typeProperty == null)
            {
                off = true;
                Warn("catalog discovery off: the Addressables members were not found; the built-in lists stay");
                return false;
            }

            var locators = locatorsProperty.GetValue(null, null) as IEnumerable;
            if (locators == null) { Progress("no locator list yet"); return false; } // not ready yet

            var watch = Stopwatch.StartNew();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var found = new List<CatalogLocation>();
            int locatorCount = 0, keyCount = 0;
            foreach (var locator in locators)
            {
                locatorCount++;
                if (!(keysProperty.GetValue(locator, null) is IEnumerable keys)) continue;
                foreach (var keyObject in keys)
                {
                    var key = keyObject as string;
                    if (key == null || IsGuid(key)) continue;
                    keyCount++;
                    var args = new object[] { key, null, null };
                    if (!(bool)locate.Invoke(locator, args) || !(args[2] is IEnumerable locations)) continue;
                    foreach (var location in locations)
                    {
                        var id = idProperty.GetValue(location, null) as string;
                        if (id == null || !id.StartsWith(Root, StringComparison.Ordinal)) continue;
                        var type = (typeProperty.GetValue(location, null) as Type)?.Name;
                        if (type == null || !seen.Add(type + "|" + id)) continue;
                        found.Add(new CatalogLocation { Key = key, InternalId = id, ResourceType = type });
                    }
                }
            }

            var pictures = MediaDiscovery.Pictures(found);
            if (pictures.Count == 0)
            {
                Progress("locators " + locatorCount + ", keys " + keyCount + ", asset locations " + found.Count + ", no pictures yet");
                return false; // the catalog has not loaded its content yet: try again
            }

            var addedPictures = PictureCatalog.Extend(pictures);
            var added = new Dictionary<MediaKind, int>();
            var foundCount = new Dictionary<MediaKind, int>();
            foreach (MediaKind kind in Enum.GetValues(typeof(MediaKind)))
            {
                var entries = MediaDiscovery.Entries(kind, found);
                foundCount[kind] = entries.Count;
                added[kind] = MediaCatalog.Extend(kind, entries);
            }
            Info("catalog discovery done in " + watch.ElapsedMilliseconds + " ms (found/new/total): pictures " + pictures.Count + "/" + addedPictures + "/"
                 + PictureCatalog.Count + ", backgrounds " + foundCount[MediaKind.Background] + "/" + added[MediaKind.Background] + "/"
                 + MediaCatalog.Entries(MediaKind.Background).Count + ", big pictures " + foundCount[MediaKind.BigPicture] + "/"
                 + added[MediaKind.BigPicture] + "/" + MediaCatalog.Entries(MediaKind.BigPicture).Count + ", videos " + foundCount[MediaKind.Video]
                 + "/" + added[MediaKind.Video] + "/" + MediaCatalog.Entries(MediaKind.Video).Count);
            return true;
        }

        /// <summary>Logs the first attempt and then every tenth, so a catalog that never answers can be diagnosed from the log.</summary>
        private static void Progress(string status)
        {
            if (attempts == 1 || attempts == 10 || attempts == FastAttempts || (attempts > FastAttempts && attempts % 60 == 0)) Info("catalog discovery attempt " + attempts + ": " + status);
        }

        private static bool IsGuid(string s)
        {
            if (s.Length != 32) return false;
            foreach (var c in s)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
            return true;
        }

        private static void Info(string m) { try { log?.Info(m); } catch { } }

        private static void Warn(string m) { try { log?.Warn(m); } catch { } }
    }
}
