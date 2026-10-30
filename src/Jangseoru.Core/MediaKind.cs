namespace Jangseoru.Core
{
    /// <summary>The three picture/video tabs: story backgrounds, big pictures and videos.</summary>
    public enum MediaKind
    {
        Background,
        BigPicture,
        Video
    }

    public static class MediaKinds
    {
        /// <summary>The short tag a kind has in <c>media.txt</c> lines.</summary>
        public static string Prefix(MediaKind kind)
        {
            switch (kind)
            {
                case MediaKind.Background: return "Bg";
                case MediaKind.BigPicture: return "Big";
                default: return "Vid";
            }
        }

        public static bool TryFromPrefix(string prefix, out MediaKind kind)
        {
            switch (prefix)
            {
                case "Bg": kind = MediaKind.Background; return true;
                case "Big": kind = MediaKind.BigPicture; return true;
                case "Vid": kind = MediaKind.Video; return true;
                default: kind = MediaKind.Background; return false;
            }
        }

        /// <summary>The library tab's caption, in Korean.</summary>
        public static string TabLabel(MediaKind kind) => TabLabel(kind, UiLanguage.Korean);

        /// <summary>The library tab's caption in the game's display language.</summary>
        public static string TabLabel(MediaKind kind, UiLanguage language) => UiText.Get(Key(kind), language);

        private static string Key(MediaKind kind)
        {
            switch (kind)
            {
                case MediaKind.Background: return UiText.Background;
                case MediaKind.BigPicture: return UiText.BigPicture;
                default: return UiText.Video;
            }
        }
    }
}
