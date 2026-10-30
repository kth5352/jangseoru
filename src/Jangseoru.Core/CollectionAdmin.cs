using System;
using System.Collections.Generic;

namespace Jangseoru.Core
{
    /// <summary>
    /// The 장서루 administration actions behind the F11 popup: open everything, or forget everything, in the mod's own records
    /// (사건, 인연, 잡물/서적/보물, 풍광/대사건/환영) and the mod's layer over the game's ending record (맹세; the game's own record
    /// is never written). The 별호 list is a separate collection and is not touched. Read-only (newer-version) stores and missing stores are left alone. Never throws.
    /// </summary>
    public static class CollectionAdmin
    {
        /// <summary>Opens every entry including the oaths (<paramref name="oathIds"/>: the game's ending ids).</summary>
        public static void UnlockAll(GalleryStore gallery, InyeonStore inyeon, ItemStore items, MediaStore media,
            IEnumerable<(ItemKind Kind, string Id)> gameItems, OathStore oath, IEnumerable<string> oathIds)
        {
            UnlockAll(gallery, inyeon, items, media, gameItems);
            Try(() =>
            {
                if (oath == null || oath.ReadOnly || oathIds == null) return;
                foreach (var id in oathIds) oath.Open(id);
            });
        }

        /// <summary>Forgets everything including the oaths: the game's current ending counts become the baselines.</summary>
        public static void Reset(GalleryStore gallery, InyeonStore inyeon, ItemStore items, MediaStore media,
            OathStore oath, IEnumerable<KeyValuePair<string, int>> oathCounts)
        {
            Reset(gallery, inyeon, items, media);
            Try(() => oath?.ResetTo(oathCounts));
        }

        /// <summary>Opens every catalog entry; <paramref name="items"/> are the game's items (kind, id), which only the game knows.</summary>
        public static void UnlockAll(GalleryStore gallery, InyeonStore inyeon, ItemStore items, MediaStore media,
            IEnumerable<(ItemKind Kind, string Id)> gameItems)
        {
            Try(() =>
            {
                if (gallery == null || gallery.ReadOnly) return;
                foreach (var key in PictureCatalog.Keys) gallery.AddPicture(key);
                foreach (var entry in GalleryLayout.EventEntries(PictureCatalog.Keys)) gallery.AddPicture(entry.UnlockKey);
            });
            Try(() =>
            {
                if (inyeon == null || inyeon.ReadOnly) return;
                foreach (var person in InyeonCatalog.Characters)
                {
                    foreach (var picture in person.Pictures) inyeon.RecordPicture(person.Id, picture);
                    foreach (var text in person.BiographyIds) inyeon.RecordBiography(person.Id, text, 4);
                }
            });
            Try(() =>
            {
                if (items == null || items.ReadOnly || gameItems == null) return;
                foreach (var item in gameItems) items.Add(item.Kind, item.Id);
            });
            Try(() =>
            {
                if (media == null || media.ReadOnly) return;
                foreach (MediaKind kind in Enum.GetValues(typeof(MediaKind)))
                    foreach (var entry in MediaCatalog.Entries(kind)) media.Add(kind, entry.Key);
            });
        }

        /// <summary>Forgets every entry of the four stores.</summary>
        public static void Reset(GalleryStore gallery, InyeonStore inyeon, ItemStore items, MediaStore media)
        {
            Try(() => gallery?.Clear());
            Try(() => inyeon?.Clear());
            Try(() => items?.Clear());
            Try(() => media?.Clear());
        }

        private static void Try(Action action)
        {
            try { action(); }
            catch (Exception) { }
        }
    }
}
