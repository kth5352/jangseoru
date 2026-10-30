using System;
using System.Reflection;
using BepInEx;
using BepInEx.Unity.Mono;
using System.Runtime.Serialization;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Jangseoru.VerifyHotkey
{
    /// <summary>
    /// TEMPORARY. Pressing F11 asks the game itself to run StatModifyManager.SetPlayer("title", 25) ("아무거나"),
    /// exactly what a story script does, so the real plugin's SetPlayer hook can be observed. It changes the
    /// current play's title, so only use it on a throw-away slot. Independent of the real plugin.
    /// </summary>
    [BepInPlugin("jangseoru.legend.verify.hotkey", "Jangseoru Verify Hotkey (temporary)", "0.0.1")]
    public sealed class VerifyHotkeyPlugin : BaseUnityPlugin
    {
        private const int TitleId = 25;
        // F12 is Steam's screenshot key, so F11 is used.
        private const KeyCode Key = KeyCode.F11;
        private Type managerType;
        private MethodInfo setPlayer;
        private bool inputBroken;

        private void Awake()
        {
            try
            {
                managerType = Type.GetType("Mortal.Story.StatModifyManager, Mortal.Story");
                setPlayer = managerType?.GetMethod("SetPlayer", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(string), typeof(int) }, null);
                Logger.LogWarning("[VerifyHotkey] TEMPORARY: F11 sets your title to id " + TitleId + " (아무거나) via the game's SetPlayer. SetPlayer " + (setPlayer != null ? "found" : "NOT found"));
            }
            catch (Exception e)
            {
                Logger.LogWarning("[VerifyHotkey] init failed: " + e.GetType().Name);
            }
        }

        private void Update()
        {
            if (inputBroken || setPlayer == null) return;
            try
            {
                if (!Input.GetKeyDown(Key)) return;
            }
            catch (Exception e)
            {
                inputBroken = true;
                Logger.LogWarning("[VerifyHotkey] legacy Input unavailable (" + e.GetType().Name + "); hotkey disabled");
                return;
            }

            try
            {
                var scene = SceneManager.GetActiveScene().name;
                if (scene == "Title" || scene == "CoreScenes")
                {
                    Logger.LogWarning("[VerifyHotkey] scene '" + scene + "' has no player yet; load a save first");
                    return;
                }

                // Story scenes have a StatModifyManager. Elsewhere (free-action map, etc.) call the same method on a
                // constructor-less instance: SetPlayer only touches a debug flag and the global player data.
                object instance = UnityEngine.Object.FindObjectOfType(managerType);
                var how = "scene object";
                if (instance == null)
                {
                    instance = FormatterServices.GetUninitializedObject(managerType);
                    how = "standalone instance";
                }

                Logger.LogInfo("[VerifyHotkey] scene '" + scene + "': calling SetPlayer(\"title\", " + TitleId + ") on a " + how);
                setPlayer.Invoke(instance, new object[] { "title", TitleId });
            }
            catch (Exception e)
            {
                Logger.LogWarning("[VerifyHotkey] action failed (" + e.GetType().Name + ")");
            }
        }
    }
}
