using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Unity.Mono;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Jangseoru.Probe
{
    /// <summary>
    /// TEMPORARY. Plan 2 pre-experiments (see docs/infomation/20-plan2-preexperiments.md):
    /// E1 library panel tree dump, E2 sprite lookup by name per scene, E3 library open/tab hooks,
    /// E4 F10 BGM play and restore, E5 F9 Addressables image load and release, E6 exception counting.
    /// No game objects are created or destroyed; the only state touched is temporary audio (E4) and temporary asset handles (E5).
    /// </summary>
    [BepInPlugin("jangseoru.legend.probe", "Jangseoru Probe (temporary)", "0.0.1")]
    public sealed class ProbePlugin : BaseUnityPlugin
    {
        private static ProbePlugin instance;
        private const BindingFlags AllMembers = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly string[] SpriteNames =
        {
            "pic_book_1", "pic_book_cover_01", "pic_paper_002", "button_menu_2", "icon_close",
            "icon_item_back_3", "icon_fate_001", "icon_star_1", "icon_seal_2", "pic_bar_process_01", "icon_lock",
        };

        private static readonly string[] AddressKeys = { "pic_walk_001", "pic_big_fight_001" };

        private string outDir;
        private float scheduleAt = -1f;
        private string scheduleReason = "";
        private bool titleTreeDumped;
        private int exceptions;
        private int commonPanelLogs;

        private Type libraryPanelType, soundManagerType;

        // E4
        private string savedMusic;
        private bool musicSwapped;
        // A key different from the title music (Legend1) so a successful call changes the observable state.
        private const string TestMusic = "Danger1";

        // E5
        private readonly List<KeyValuePair<string, AsyncOperationHandle<Sprite>>> pending = new List<KeyValuePair<string, AsyncOperationHandle<Sprite>>>();

        private void Awake()
        {
            instance = this;
            try
            {
                var parent = Path.GetDirectoryName(Application.persistentDataPath.TrimEnd('/', '\\'));
                outDir = Path.Combine(parent, "JangseoruMod", "probe");
                Directory.CreateDirectory(outDir);

                libraryPanelType = AccessTools.TypeByName("Mortal.Core.LibraryPanel");
                soundManagerType = AccessTools.TypeByName("Mortal.Core.SoundManager");

                SceneManager.sceneLoaded += OnSceneLoaded;
                InstallHooks();
                Note("TEMPORARY probe loaded. F9 = Addressables image load/release, F10 = BGM play/restore. output: " + outDir);
            }
            catch (Exception e) { Fail("Awake", e); }
        }

        // ---------------- E3: hooks ----------------

        private void InstallHooks()
        {
            var harmony = new Harmony("jangseoru.legend.probe");

            var lib = libraryPanelType;
            Patch(harmony, lib, "OnPanelOpen", new Type[0], nameof(LibraryPost), "E3 LibraryPanel.OnPanelOpen");
            foreach (var n in new[] { "OpenEndPanel", "OpenDeadPanel", "OpenAchivementPanel", "OpenLegendPanel" })
                Patch(harmony, lib, n, new Type[0], nameof(LibraryPost), "E3 LibraryPanel." + n);

            Patch(harmony, AccessTools.TypeByName("Mortal.Core.TitleManager"), "ShowLibraryPanel", new[] { typeof(bool) }, nameof(TitleShowPost), "E3 TitleManager.ShowLibraryPanel");
            Patch(harmony, AccessTools.TypeByName("Mortal.Core.CommonPanel"), "Show", new[] { typeof(bool), typeof(bool) }, nameof(CommonPanelPost), "E3 CommonPanel.Show");
        }

        private void Patch(Harmony harmony, Type type, string method, Type[] args, string postfix, string label)
        {
            try
            {
                var target = type?.GetMethod(method, AllMembers, null, args, null);
                if (target == null) { Warn(label + ": target not found"); return; }
                harmony.Patch(target, postfix: new HarmonyMethod(typeof(ProbePlugin).GetMethod(postfix, BindingFlags.Static | BindingFlags.NonPublic)));
                Note(label + ": patched");
            }
            catch (Exception e) { Fail("patch " + label, e); }
        }

        private static void LibraryPost(object __instance, MethodBase __originalMethod)
        {
            try
            {
                instance.Note("E3 " + __originalMethod.Name + " (library panel)");
                if (__originalMethod.Name == "OnPanelOpen") instance.Schedule("library-open", 1.0f);
            }
            catch (Exception e) { instance?.Fail("LibraryPost", e); }
        }

        private static void TitleShowPost(bool show)
        {
            try { instance.Note("E3 TitleManager.ShowLibraryPanel(" + show + ")"); }
            catch (Exception e) { instance?.Fail("TitleShowPost", e); }
        }

        private static void CommonPanelPost(object __instance, bool show)
        {
            try
            {
                var go = (__instance as Component)?.gameObject;
                if (go == null) return;
                var name = go.name;
                if (instance.commonPanelLogs >= 80 && name.IndexOf("ibrary", StringComparison.OrdinalIgnoreCase) < 0) return;
                instance.commonPanelLogs++;
                instance.Note("E3 CommonPanel.Show('" + name + "', " + show + ") parent='" + (go.transform.parent != null ? go.transform.parent.name : "-") + "'");
            }
            catch (Exception e) { instance?.Fail("CommonPanelPost", e); }
        }

        // ---------------- scheduling ----------------

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Schedule("scene:" + scene.name, 3.0f);
        }

        private void Schedule(string reason, float delaySeconds)
        {
            scheduleReason = reason;
            scheduleAt = Time.realtimeSinceStartup + delaySeconds;
        }

        private void Update()
        {
            try
            {
                if (scheduleAt > 0f && Time.realtimeSinceStartup >= scheduleAt)
                {
                    var reason = scheduleReason;
                    scheduleAt = -1f;
                    RunE2(reason);
                    if (reason == "scene:Title" && !titleTreeDumped) { titleTreeDumped = true; DumpLibraryTree("title"); }
                    if (reason == "library-open") DumpLibraryTree("open");
                }

                if (Input.GetKeyDown(KeyCode.F10)) ToggleMusic();
                if (Input.GetKeyDown(KeyCode.F9)) StartImageLoads();
                PollImageLoads();
            }
            catch (Exception e) { Fail("Update", e); }
        }

        // ---------------- E2: sprite lookup ----------------

        private void RunE2(string reason)
        {
            var scene = SceneManager.GetActiveScene().name;
            var sprites = Resources.FindObjectsOfTypeAll<Sprite>();
            var textures = Resources.FindObjectsOfTypeAll<Texture2D>();
            var spriteByName = new Dictionary<string, Sprite>();
            foreach (var s in sprites) if (s != null && !spriteByName.ContainsKey(s.name)) spriteByName[s.name] = s;
            var texNames = new HashSet<string>(textures.Where(t => t != null).Select(t => t.name));

            var found = new List<string>();
            var textureOnly = new List<string>();
            var missing = new List<string>();
            foreach (var n in SpriteNames)
            {
                if (spriteByName.TryGetValue(n, out var sp)) found.Add(n + "(" + (int)sp.rect.width + "x" + (int)sp.rect.height + ")");
                else if (texNames.Contains(n)) textureOnly.Add(n);
                else missing.Add(n);
            }
            Note("E2 [" + reason + "] scene=" + scene + " sprites=" + sprites.Length + " textures=" + textures.Length
                 + " | sprite found " + found.Count + "/" + SpriteNames.Length + ": " + string.Join(", ", found)
                 + " | texture only: " + string.Join(", ", textureOnly)
                 + " | missing: " + string.Join(", ", missing));
        }

        // ---------------- E1: library panel tree ----------------

        private void DumpLibraryTree(string tag)
        {
            if (libraryPanelType == null) { Warn("E1 LibraryPanel type not found"); return; }
            Component panel = null;
            foreach (var o in Resources.FindObjectsOfTypeAll(libraryPanelType))
            {
                var c = o as Component;
                if (c != null && c.gameObject.scene.IsValid()) { panel = c; break; }
            }
            if (panel == null) { Note("E1 [" + tag + "] no LibraryPanel instance in the loaded scene"); return; }

            var sb = new StringBuilder();
            sb.AppendLine("scene=" + SceneManager.GetActiveScene().name + " tag=" + tag + " time=" + DateTime.Now.ToString("s"));
            sb.AppendLine("ancestors: " + string.Join(" > ", Ancestors(panel.transform)));
            sb.AppendLine("panel active=" + panel.gameObject.activeInHierarchy);
            sb.AppendLine();
            sb.AppendLine("== LibraryPanel fields that reference scene objects ==");
            foreach (var f in libraryPanelType.GetFields(AllMembers))
            {
                try
                {
                    var v = f.GetValue(panel);
                    if (v is Component c) sb.AppendLine(f.Name + " : " + f.FieldType.Name + " -> " + PathOf(c.transform));
                    else if (v is GameObject g) sb.AppendLine(f.Name + " : GameObject -> " + PathOf(g.transform));
                    else if (v is Array arr && arr.Length > 0 && arr.GetValue(0) is Component)
                        sb.AppendLine(f.Name + " : " + f.FieldType.Name + " [" + string.Join(", ", arr.Cast<Component>().Where(x => x != null).Select(x => PathOf(x.transform))) + "]");
                }
                catch (Exception) { }
            }
            sb.AppendLine();
            sb.AppendLine("== hierarchy (depth<=8, max 600 nodes) ==");
            var count = 0;
            DumpNode(panel.transform, 0, sb, ref count);

            var file = Path.Combine(outDir, "library_tree_" + tag + ".txt");
            File.WriteAllText(file, sb.ToString(), Encoding.UTF8);
            Note("E1 [" + tag + "] dumped " + count + " nodes to " + file);
        }

        private static void DumpNode(Transform t, int depth, StringBuilder sb, ref int count)
        {
            if (depth > 8 || count >= 600) return;
            count++;
            sb.Append(new string(' ', depth * 2)).Append(t.name).Append(t.gameObject.activeSelf ? "" : " [inactive]");

            var comps = t.GetComponents<Component>().Where(c => c != null && !(c is Transform)).Select(c => c.GetType().Name).ToArray();
            if (comps.Length > 0) sb.Append("  <").Append(string.Join(",", comps)).Append(">");

            var rt = t as RectTransform;
            if (rt != null)
                sb.Append("  rect(aMin=").Append(V(rt.anchorMin)).Append(" aMax=").Append(V(rt.anchorMax))
                  .Append(" size=").Append(V(rt.sizeDelta)).Append(" pos=").Append(V(rt.anchoredPosition)).Append(")");

            var img = t.GetComponent<Image>();
            if (img != null) sb.Append("  img=").Append(img.sprite != null ? img.sprite.name : "-");
            var txt = t.GetComponent<Text>();
            if (txt != null) sb.Append("  text=\"").Append((txt.text ?? "").Replace("\n", "\\n").Substring(0, Math.Min(24, (txt.text ?? "").Length))).Append("\"");
            var btn = t.GetComponent<Button>();
            if (btn != null) sb.Append("  button(interactable=").Append(btn.interactable).Append(" persistentEvents=").Append(btn.onClick.GetPersistentEventCount()).Append(")");
            sb.AppendLine();

            for (var i = 0; i < t.childCount; i++) DumpNode(t.GetChild(i), depth + 1, sb, ref count);
        }

        private static string V(Vector2 v) => "(" + v.x.ToString("0.#") + "," + v.y.ToString("0.#") + ")";

        private static IEnumerable<string> Ancestors(Transform t)
        {
            var list = new List<string>();
            for (var p = t; p != null; p = p.parent) list.Insert(0, p.name);
            return list;
        }

        private static string PathOf(Transform t) => string.Join("/", Ancestors(t));

        // ---------------- E4: BGM ----------------

        private void ToggleMusic()
        {
            var sm = soundManagerType?.GetProperty("Instance", AllMembers)?.GetValue(null, null);
            if (sm == null) { Warn("E4 SoundManager.Instance not available in this scene"); return; }

            var current = soundManagerType.GetField("_currentMusic", AllMembers)?.GetValue(sm) as string;
            var play = soundManagerType.GetMethod("PlayMusic", AllMembers, null, new[] { typeof(string) }, null);
            var stop = soundManagerType.GetMethod("StopMusic", AllMembers, null, new Type[0], null);
            if (play == null) { Warn("E4 PlayMusic not found"); return; }

            if (!musicSwapped)
            {
                savedMusic = current;
                Note("E4 current music key='" + savedMusic + "'; playing '" + TestMusic + "' (press F10 again to restore)");
                play.Invoke(sm, new object[] { TestMusic });
                Note("E4 after play: key='" + (soundManagerType.GetField("_currentMusic", AllMembers)?.GetValue(sm) as string) + "'");
                musicSwapped = true;
            }
            else
            {
                Note("E4 restoring music key='" + savedMusic + "' (was '" + current + "')");
                if (string.IsNullOrEmpty(savedMusic)) stop?.Invoke(sm, null);
                else play.Invoke(sm, new object[] { savedMusic });
                Note("E4 after restore: key='" + (soundManagerType.GetField("_currentMusic", AllMembers)?.GetValue(sm) as string) + "'");
                musicSwapped = false;
            }
        }

        // ---------------- E5: Addressables ----------------

        private void StartImageLoads()
        {
            foreach (var key in AddressKeys)
            {
                var handle = Addressables.LoadAssetAsync<Sprite>(key);
                pending.Add(new KeyValuePair<string, AsyncOperationHandle<Sprite>>(key, handle));
                Note("E5 load started: " + key);
            }
        }

        private void PollImageLoads()
        {
            for (var i = pending.Count - 1; i >= 0; i--)
            {
                var kv = pending[i];
                if (!kv.Value.IsDone) continue;
                pending.RemoveAt(i);

                var status = kv.Value.Status;
                var sprite = status == AsyncOperationStatus.Succeeded ? kv.Value.Result : null;
                Note("E5 load done: " + kv.Key + " status=" + status
                     + (sprite != null ? " sprite=" + sprite.name + " " + (int)sprite.rect.width + "x" + (int)sprite.rect.height : " (no result)"));
                try { Addressables.Release(kv.Value); Note("E5 released: " + kv.Key); }
                catch (Exception e) { Fail("release " + kv.Key, e); }
            }
        }

        // ---------------- logging / E6 ----------------

        private void Note(string m) => Logger.LogInfo("[Probe] " + m);
        private void Warn(string m) => Logger.LogWarning("[Probe] " + m);

        private void Fail(string where, Exception e)
        {
            exceptions++;
            Logger.LogWarning("[Probe] E6 exception in " + where + ": " + e.GetType().Name + ": " + e.Message);
        }

        private void OnApplicationQuit()
        {
            Note("E6 summary: exceptions=" + exceptions);
        }
    }
}
