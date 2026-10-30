using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Unity.Mono;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Jangseoru.Probe2
{
    /// <summary>
    /// TEMPORARY. Plan 2 UI probes P1-P5 (docs/infomation/22-jangseoru-ui-design.md): builds a prototype "Jangseoru" tab inside the
    /// library panel (cloned menu button with a replaced onClick, a panel assembled from clones of the EndGame panel's title/scroll/number/bar,
    /// cards built from scratch, a two-paper detail view). Nothing is saved; objects it creates are destroyed with the library panel.
    /// F8 = click the tab, F7 = open detail of card 1, F6 = close detail.
    /// </summary>
    [BepInPlugin("jangseoru.legend.probe2", "Jangseoru Probe2 (temporary)", "0.0.1")]
    /// <summary>Only the three handlers we need, so scroll and drag events keep bubbling to the ScrollRect.</summary>
    public sealed class CardHover : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
    {
        public Action OnSelected, OnDeselected, OnEnter;
        public void OnSelect(BaseEventData e) { OnSelected?.Invoke(); }
        public void OnDeselect(BaseEventData e) { OnDeselected?.Invoke(); }
        public void OnPointerEnter(PointerEventData e) { OnEnter?.Invoke(); }
    }

    public sealed class Probe2Plugin : BaseUnityPlugin
    {
        private static Probe2Plugin I;
        private const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private string outDir;
        private int exceptions;
        private int buildCount;

        private Component lib;
        private GameObject tabButton, root, detail;
        private Image detailImage;
        private Text detailName, detailInfo;
        private readonly List<Sprite> pics = new List<Sprite>();
        private readonly List<string> names = new List<string>();

        private void Awake()
        {
            I = this;
            try
            {
                var parent = Path.GetDirectoryName(Application.persistentDataPath.TrimEnd('/', '\\'));
                outDir = Path.Combine(parent, "JangseoruMod", "probe");
                Directory.CreateDirectory(outDir);

                var harmony = new Harmony("jangseoru.legend.probe2");
                var libType = AccessTools.TypeByName("Mortal.Core.LibraryPanel");
                Patch(harmony, libType, "OnPanelOpen", nameof(OpenPost));
                foreach (var n in new[] { "OpenEndPanel", "OpenDeadPanel", "OpenAchivementPanel", "OpenLegendPanel" })
                    Patch(harmony, libType, n, nameof(TabPost));
                Note("TEMPORARY probe2 loaded. F8 = click Jangseoru tab, F7 = open detail 1, F6 = close detail");
            }
            catch (Exception e) { Fail("Awake", e); }
        }

        private void Patch(Harmony harmony, Type type, string method, string postfix)
        {
            try
            {
                var target = type?.GetMethod(method, All, null, new Type[0], null);
                if (target == null) { Note("patch " + method + ": target not found"); return; }
                harmony.Patch(target, postfix: new HarmonyMethod(typeof(Probe2Plugin).GetMethod(postfix, BindingFlags.Static | BindingFlags.NonPublic)));
                Note("patch " + method + ": ok");
            }
            catch (Exception e) { Fail("patch " + method, e); }
        }

        private static void OpenPost(object __instance)
        {
            try { I.OnLibraryOpen(__instance as Component); }
            catch (Exception e) { I.Fail("OpenPost", e); }
        }

        private static void TabPost(MethodBase __originalMethod)
        {
            try
            {
                // Another tab was opened: hide ours (and a pending detail view).
                I.Note("P2 other tab opened: " + __originalMethod.Name + " -> hide ours");
                I.HideOurs();
            }
            catch (Exception e) { I.Fail("TabPost", e); }
        }

        // ---------------- build (P1, P2, P5) ----------------

        private void OnLibraryOpen(Component panel)
        {
            lib = panel;
            var libT = panel.transform;
            if (libT.Find("Panels/Jangseoru") != null)
            {
                Note("P5 reopen: our objects already exist (no rebuild). buttons=" + libT.Find("Buttons").childCount);
                CloseDetail();
                return;
            }
            Build(libT);
        }

        private void Build(Transform libT)
        {
            buildCount++;
            var buttons = libT.Find("Buttons");
            var src = buttons.Find("MenuButton_Library_Legend");
            var panels = libT.Find("Panels");
            var endGame = panels.Find("EndGame");
            var content = endGame.Find("Scroll View/Viewport/Content");
            var templ = content.GetChild(0);

            // --- tab button (P1) ---
            var clone = Object.Instantiate(src.gameObject, buttons, false);
            clone.name = "MenuButton_Library_Jangseoru";
            tabButton = clone;
            var btn = clone.GetComponent<Button>();
            var before = btn.onClick.GetPersistentEventCount();
            btn.onClick = new Button.ButtonClickedEvent();
            btn.onClick.AddListener(OnTabClick);
            Note("P1 clone onClick persistent " + before + " -> " + btn.onClick.GetPersistentEventCount());
            foreach (var c in clone.GetComponents<Component>())
            {
                if (c == null) continue;
                var t = c.GetType();
                if (t.Name == "MenuToggleButton")
                    foreach (var fn in new[] { "_normal", "_enable", "_hover", "_disable" })
                    {
                        var g = t.GetField(fn, All)?.GetValue(c) as GameObject;
                        Note("P1 MenuToggleButton." + fn + " -> " + (g == null ? "null" : (g.transform.IsChildOf(clone.transform) ? "own child '" + g.name + "'" : "FOREIGN '" + g.name + "'")));
                    }
                if (t.Name == "InputButton")
                {
                    var b = t.GetField("_button", All)?.GetValue(c) as Button;
                    var path = t.GetField("_actionPath", All)?.GetValue(c) as string;
                    Note("P1 InputButton._button own=" + (b == btn) + " _actionPath='" + path + "'");
                    if (!string.IsNullOrEmpty(path)) { t.GetMethod("SetActive", All)?.Invoke(c, new object[] { false }); Note("P1 InputButton disabled (had an action path)"); }
                }
            }
            foreach (var txt in clone.GetComponentsInChildren<Text>(true))
            {
                StripLocalizedText(txt.gameObject);
                txt.text = "장서루";
            }

            // --- panel root (P2) ---
            root = new GameObject("Jangseoru", typeof(RectTransform), typeof(CanvasGroup));
            var rt = (RectTransform)root.transform;
            rt.SetParent(panels, false);
            var ert = (RectTransform)endGame;
            rt.anchorMin = ert.anchorMin; rt.anchorMax = ert.anchorMax; rt.sizeDelta = ert.sizeDelta; rt.anchoredPosition = ert.anchoredPosition;
            foreach (var n in new[] { "Title", "Scroll View", "Number", "Process" })
            {
                var s = endGame.Find(n);
                if (s == null) { Note("P2 missing EndGame child " + n); continue; }
                Object.Instantiate(s.gameObject, root.transform, false).name = n;
            }
            StripLocalizedText(root.transform.Find("Title/Text").gameObject);
            root.transform.Find("Title/Text").GetComponent<Text>().text = "장서루";

            var newContent = root.transform.Find("Scroll View/Viewport/Content");
            for (var i = newContent.childCount - 1; i >= 0; i--) Object.DestroyImmediate(newContent.GetChild(i).gameObject);

            LoadEndCg();
            for (var i = 0; i < pics.Count; i++) MakeCard(i, i < 5, newContent, templ);
            root.transform.Find("Number").GetComponent<Text>().text = "5/" + pics.Count;
            var bar = root.transform.Find("Process/Bar").GetComponent<Image>();
            bar.fillAmount = pics.Count == 0 ? 0f : 5f / pics.Count;

            BuildDetail(libT, templ);
            root.SetActive(false);
            Note("P1/P2 build done (#" + buildCount + "), cards=" + pics.Count);
            DumpTree();
        }

        private static void StripLocalizedText(GameObject go)
        {
            foreach (var c in go.GetComponents<Component>())
                if (c != null && c.GetType().Name == "LeanLocalizedText") Object.Destroy(c);
        }

        private void LoadEndCg()
        {
            pics.Clear(); names.Clear();
            var lsT = AccessTools.TypeByName("Mortal.Core.LibrarySystem");
            var inst = lsT.GetProperty("Instance", All).GetValue(null, null);
            var endCg = lsT.GetProperty("EndCg", All).GetValue(inst, null);
            var list = (IEnumerable)endCg.GetType().GetProperty("List", All).GetValue(endCg, null);
            foreach (var item in list)
            {
                var it = item.GetType();
                var pic = it.GetProperty("Picture", All).GetValue(item, null) as Sprite;
                var count = (int)it.GetProperty("Count", All).GetValue(item, null);
                var nm = ((Object)item).name;
                pics.Add(pic); names.Add(nm);
                Note("P-data EndCg item '" + nm + "' count=" + count + " sprite=" + (pic == null ? "null" : pic.name + " " + (int)pic.rect.width + "x" + (int)pic.rect.height));
            }
        }

        private void MakeCard(int i, bool revealed, Transform content, Transform templ)
        {
            var card = new GameObject("JCard" + (i + 1), typeof(RectTransform));
            card.transform.SetParent(content, false);
            var back = Object.Instantiate(templ.Find("Back").gameObject, card.transform, false);
            var q = Object.Instantiate(templ.Find("NotAchieveText").gameObject, card.transform, false);
            q.SetActive(!revealed);
            var picGo = new GameObject("Picture", typeof(RectTransform), typeof(Image));
            picGo.transform.SetParent(card.transform, false);
            var pimg = picGo.GetComponent<Image>();
            pimg.sprite = pics[i]; pimg.preserveAspect = true; pimg.raycastTarget = false;
            var prt = (RectTransform)picGo.transform; var srt = (RectTransform)templ.Find("Picture");
            prt.anchorMin = srt.anchorMin; prt.anchorMax = srt.anchorMax; prt.sizeDelta = srt.sizeDelta; prt.anchoredPosition = srt.anchoredPosition;
            picGo.SetActive(revealed);
            var idx = Object.Instantiate(templ.Find("Index").gameObject, card.transform, false);
            StripLocalizedText(idx);
            idx.GetComponent<Text>().text = "엔딩 " + (i + 1);
            var border = Object.Instantiate(templ.Find("Border").gameObject, card.transform, false);
            border.SetActive(false);

            var button = card.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            var backImg = back.GetComponent<Image>();
            Note("diag template Back raycastTarget=" + backImg.raycastTarget);
            backImg.raycastTarget = true;
            button.targetGraphic = backImg;
            var index = i;
            button.onClick.AddListener(() => { Note("diag card click #" + (index + 1)); if (revealed) OpenDetail(index); });
            // Not EventTrigger: it implements IScrollHandler/IDragHandler and swallows them, so the parent ScrollRect never scrolls over a card.
            var hover = card.AddComponent<CardHover>();
            hover.OnSelected = () => border.SetActive(true);
            hover.OnDeselected = () => border.SetActive(false);
            hover.OnEnter = () => EventSystem.current?.SetSelectedGameObject(card);
        }

        private void AddDummyCards()
        {
            var content = root.transform.Find("Scroll View/Viewport/Content");
            var templ = lib.transform.Find("Panels/EndGame/Scroll View/Viewport/Content").GetChild(0);
            var n = pics.Count;
            for (var i = 0; i < 80; i++) { pics.Add(pics[i % n]); names.Add(names[i % n] + "_d"); MakeCard(pics.Count - 1, true, content, templ); }
            Note("P4 added 80 dummy cards, total=" + pics.Count);
        }

        // ---------------- detail (B) ----------------

        private void BuildDetail(Transform libT, Transform templ)
        {
            detail = new GameObject("Detail", typeof(RectTransform));
            detail.transform.SetParent(root.transform, false);

            var line = root.transform.Find("Title/Line1").GetComponent<Image>(); // whiteSquare sprite, reference only
            var dim = NewImage("Dim", detail.transform, line.sprite, new Color(0, 0, 0, 0.8f), new Vector2(4000, 2500), Vector2.zero);
            dim.raycastTarget = true;
            dim.gameObject.AddComponent<Button>().onClick.AddListener(CloseDetail);

            var paper = templ.Find("Back").GetComponent<Image>().sprite;
            var left = NewImage("PageLeft", detail.transform, paper, Color.white, new Vector2(620, 860), new Vector2(-310, -20));
            var right = NewImage("PageRight", detail.transform, paper, Color.white, new Vector2(620, 860), new Vector2(310, -20));
            right.rectTransform.localScale = new Vector3(-1, 1, 1);
            left.raycastTarget = false; right.raycastTarget = false;

            detailImage = NewImage("Big", detail.transform, null, Color.white, new Vector2(470, 700), new Vector2(-310, -20));
            detailImage.preserveAspect = true; detailImage.raycastTarget = false;
            var frame = NewImage("Frame", detail.transform, templ.Find("Border").GetComponent<Image>().sprite, Color.white, new Vector2(500, 730), new Vector2(-310, -20));
            frame.raycastTarget = false;

            var nameGo = Object.Instantiate(templ.Find("Index").gameObject, detail.transform, false);
            StripLocalizedText(nameGo);
            detailName = nameGo.GetComponent<Text>();
            detailName.fontSize = 56; detailName.alignment = TextAnchor.MiddleCenter; detailName.color = new Color(0.2f, 0.12f, 0.06f);
            var nrt = (RectTransform)nameGo.transform;
            nrt.pivot = new Vector2(0.5f, 0.5f);
            nrt.anchorMin = nrt.anchorMax = new Vector2(0.5f, 0.5f); nrt.sizeDelta = new Vector2(480, 120); nrt.anchoredPosition = new Vector2(310, 220);

            var infoGo = Object.Instantiate(nameGo, detail.transform, false);
            detailInfo = infoGo.GetComponent<Text>();
            detailInfo.fontSize = 32;
            var irt = (RectTransform)infoGo.transform;
            irt.pivot = new Vector2(0.5f, 0.5f);
            irt.anchoredPosition = new Vector2(310, 60); irt.sizeDelta = new Vector2(480, 300);
            detail.SetActive(false);
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite, Color color, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite; img.color = color;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.sizeDelta = size; rt.anchoredPosition = pos;
            return img;
        }

        private void OpenDetail(int i)
        {
            if (detail == null || i < 0 || i >= pics.Count) return;
            detailImage.sprite = pics[i];
            detailName.text = "엔딩 이미지 " + (i + 1);
            detailInfo.text = names[i] + "\n(probe)\n닫기: Esc / 바깥 클릭";
            detail.SetActive(true);
            SetInputClose(false);
            Note("B detail opened #" + (i + 1));
        }

        private void CloseDetail()
        {
            if (detail == null || !detail.activeSelf) return;
            detail.SetActive(false);
            SetInputClose(true);
            Note("B detail closed");
        }

        private void SetInputClose(bool active)
        {
            if (lib == null) return;
            var ib = lib.GetType().GetField("_inputCloseButton", All)?.GetValue(lib);
            ib?.GetType().GetMethod("SetActive", All)?.Invoke(ib, new object[] { active });
        }

        // ---------------- tab click (P2) ----------------

        private void OnTabClick()
        {
            try
            {
                foreach (var f in new[] { "_endPanel", "_deadPanel", "_achievementPanel", "_legendPanel" })
                {
                    var p = lib.GetType().GetField(f, All)?.GetValue(lib);
                    p?.GetType().GetMethod("Show", All, null, new[] { typeof(bool) }, null)?.Invoke(p, new object[] { false });
                }
                var fate = lib.GetType().GetField("_fatePanel", All)?.GetValue(lib) as GameObject;
                fate?.SetActive(true);
                root.SetActive(true);
                root.GetComponent<CanvasGroup>().alpha = 1f;
                Note("P2 tab clicked: panel shown");
            }
            catch (Exception e) { Fail("OnTabClick", e); }
        }

        private void HideOurs()
        {
            CloseDetail();
            if (root != null) root.SetActive(false);
        }

        // ---------------- input ----------------

        private void Update()
        {
            try
            {
                if (Input.GetMouseButtonDown(0) && EventSystem.current != null && root != null && root.activeInHierarchy)
                {
                    var data = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
                    var hits = new List<RaycastResult>();
                    EventSystem.current.RaycastAll(data, hits);
                    Note("diag mouse down at " + Input.mousePosition + " hits: " + string.Join(" | ", hits.Take(6).Select(h => PathName(h.gameObject.transform))));
                }
                if (Input.GetKeyDown(KeyCode.F8) && tabButton != null) tabButton.GetComponent<Button>().onClick.Invoke();
                if (Input.GetKeyDown(KeyCode.F5) && root != null) AddDummyCards();
                if (Input.GetKeyDown(KeyCode.F7)) OpenDetail(0);
                if (Input.GetKeyDown(KeyCode.F6)) CloseDetail();
                if (detail != null && detail.activeSelf && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton1)))
                {
                    Note("P3 cancel pressed while detail open");
                    CloseDetail();
                }
            }
            catch (Exception e) { Fail("Update", e); }
        }

        // ---------------- dump / log ----------------

        private static string PathName(Transform t)
        {
            var parts = new List<string>();
            for (var p = t; p != null && parts.Count < 4; p = p.parent) parts.Insert(0, p.name);
            return string.Join("/", parts);
        }

        private void DumpTree()
        {
            var sb = new StringBuilder();
            foreach (var t in new[] { tabButton.transform, root.transform })
                Dump(t, 0, sb);
            File.WriteAllText(Path.Combine(outDir, "p2_tree.txt"), sb.ToString(), Encoding.UTF8);
        }

        private static void Dump(Transform t, int d, StringBuilder sb)
        {
            if (d > 6) return;
            sb.Append(new string(' ', d * 2)).Append(t.name).Append(t.gameObject.activeSelf ? "" : " [inactive]");
            var comps = t.GetComponents<Component>().Where(c => c != null && !(c is Transform)).Select(c => c.GetType().Name);
            sb.Append("  <").Append(string.Join(",", comps)).AppendLine(">");
            for (var i = 0; i < t.childCount && i < 12; i++) Dump(t.GetChild(i), d + 1, sb);
        }

        private void Note(string m) => Logger.LogInfo("[Probe2] " + m);

        private void Fail(string where, Exception e)
        {
            exceptions++;
            Logger.LogWarning("[Probe2] exception in " + where + ": " + e.GetType().Name + ": " + e.Message + " @ " + e.StackTrace);
        }

        private void OnApplicationQuit() { Note("summary: exceptions=" + exceptions + " builds=" + buildCount); }
    }
}
