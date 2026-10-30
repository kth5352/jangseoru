using System;

namespace Jangseoru.Core
{
    /// <summary>The three item kinds the library shows (the game's consumables are not part of it).</summary>
    public enum ItemKind
    {
        Book,
        Misc,
        Special
    }

    public static class ItemKinds
    {
        /// <summary>The prefix of the game's text keys and of the lines in the mod's item file.</summary>
        public static string Prefix(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.Book: return "Book";
                case ItemKind.Misc: return "Misc";
                default: return "Special";
            }
        }

        /// <summary>The library tab's caption, in Korean.</summary>
        public static string TabLabel(ItemKind kind) => TabLabel(kind, UiLanguage.Korean);

        /// <summary>The library tab's caption in the game's display language.</summary>
        public static string TabLabel(ItemKind kind, UiLanguage language) => UiText.Get(Key(kind), language);

        private static string Key(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.Book: return UiText.Book;
                case ItemKind.Misc: return UiText.Misc;
                default: return UiText.Special;
            }
        }

        public static bool TryFromPrefix(string prefix, out ItemKind kind)
        {
            kind = ItemKind.Book;
            if (prefix == null) return false;
            foreach (ItemKind k in Enum.GetValues(typeof(ItemKind)))
            {
                if (!string.Equals(Prefix(k), prefix, StringComparison.Ordinal)) continue;
                kind = k;
                return true;
            }
            return false;
        }
    }
}
