using Jangseoru.Core;

namespace Jangseoru.Plugin
{
    /// <summary>
    /// The display language of the texts the mod shows itself. Set by the library tab from the game's current language each time
    /// the library opens (before anything is built); <see cref="T"/> reads <see cref="UiText"/> in that language.
    /// </summary>
    internal static class Loc
    {
        public static UiLanguage Current = UiLanguage.Korean;

        public static string T(string key) => UiText.Get(key, Current);

        public static string Format(string key, object arg) => UiText.Format(key, Current, arg);
    }
}
