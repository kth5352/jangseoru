using System;
using System.Reflection;
using HarmonyLib;
using Jangseoru.Core;

namespace Jangseoru.Plugin
{
    /// <summary>
    /// The only way this plugin patches the game: Postfix only, and a failure disables just that feature.
    /// </summary>
    internal static class SafePatcher
    {
        public static bool TryPostfix(Harmony harmony, MethodBase target, MethodInfo postfix, string feature, ILog log)
        {
            if (target == null)
            {
                log.Warn("feature '" + feature + "' disabled: target not found");
                return false;
            }
            if (postfix == null)
            {
                log.Warn("feature '" + feature + "' disabled: postfix method not found");
                return false;
            }

            try
            {
                harmony.Patch(target, postfix: new HarmonyMethod(postfix));
                log.Info("feature '" + feature + "' patched: " + target.DeclaringType?.Name + "." + target.Name);
                return true;
            }
            catch (Exception e)
            {
                log.Warn("feature '" + feature + "' disabled: patch failed (" + e.GetType().Name + ")");
                return false;
            }
        }
    }
}
