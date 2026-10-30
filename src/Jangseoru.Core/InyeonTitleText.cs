namespace Jangseoru.Core
{
    /// <summary>The game's player epithet line is the literal text <c>{title}</c>, filled in by the game with the player's current 별호.</summary>
    public static class InyeonTitleText
    {
        public const string Placeholder = "{title}";

        /// <summary>Replaces the placeholder with the player's 별호; with none known the placeholder is dropped. Never throws.</summary>
        public static string FillPlayerTitle(string text, string playerTitle)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var fill = string.IsNullOrWhiteSpace(playerTitle) ? "" : playerTitle;
            return text.Replace(Placeholder, fill).Trim();
        }
    }
}
