using System;
using System.Linq;
using Xunit;

namespace Jangseoru.Core.Tests
{
    [Collection("InyeonCatalog")]
    public class CollectionAdminTests
    {
        private static readonly (ItemKind, string)[] SomeItems = { (ItemKind.Book, "1000"), (ItemKind.Misc, "2001"), (ItemKind.Special, "1000") };

        [InyeonFact]
        public void Unlock_all_opens_every_catalog_entry_of_every_tab()
        {
            var g = new GalleryStore();
            var i = new InyeonStore();
            var it = new ItemStore();
            var m = new MediaStore();
            CollectionAdmin.UnlockAll(g, i, it, m, SomeItems);

            Assert.All(GalleryLayout.EventEntries(PictureCatalog.Keys), e => Assert.True(g.HasPicture(e.UnlockKey), e.Key));
            Assert.All(InyeonCatalog.Characters.Where(c => c.Pictures.Count > 0 || c.BiographyIds.Count > 0),
                c => Assert.True(i.IsKnown(c.Id), c.Id));
            var someone = InyeonCatalog.Characters.First(c => c.BiographyIds.Count > 0);
            Assert.Equal(4, i.BiographyStage(someone.Id, someone.BiographyIds[0]));
            Assert.True(it.Has(ItemKind.Book, "1000") && it.Has(ItemKind.Misc, "2001") && it.Has(ItemKind.Special, "1000"));
            foreach (MediaKind kind in Enum.GetValues(typeof(MediaKind)))
                Assert.All(MediaCatalog.Entries(kind), e => Assert.True(m.Has(kind, e.Key), e.Key));
        }

        [Fact]
        public void Reset_empties_every_store_but_keeps_lines_of_unknown_kinds()
        {
            var g = new GalleryStore();
            var i = new InyeonStore();
            var it = ItemStore.Parse("jangseoru.items v1\nBook:1000\nFuture:abc\n", out _);
            var m = MediaStore.Parse("jangseoru.media v1\nVid:video_event_001\nFuture:abc\n", out _);
            CollectionAdmin.UnlockAll(g, i, new ItemStore(), new MediaStore(), SomeItems);

            CollectionAdmin.Reset(g, i, it, m);

            Assert.Equal(0, g.PictureCount);
            Assert.All(InyeonCatalog.Characters, c => Assert.False(i.IsKnown(c.Id)));
            Assert.Equal(0, it.Count);
            Assert.Equal(0, m.Count);
            Assert.Contains("Future:abc", it.Serialize()); // a newer build's lines are not ours to delete
            Assert.Contains("Future:abc", m.Serialize());
        }

        [Fact]
        public void Read_only_stores_from_a_newer_version_are_never_changed()
        {
            var it = ItemStore.Parse("jangseoru.items v9\nBook:1000\n", out _);
            var m = MediaStore.Parse("jangseoru.media v9\nVid:x\n", out _);
            var before = it.Serialize() + m.Serialize();
            CollectionAdmin.UnlockAll(null, null, it, m, SomeItems);
            CollectionAdmin.Reset(null, null, it, m);
            Assert.Equal(before, it.Serialize() + m.Serialize());
        }

        [Fact]
        public void Missing_stores_and_items_are_tolerated()
        {
            CollectionAdmin.UnlockAll(null, null, null, null, null);
            CollectionAdmin.Reset(null, null, null, null);
        }
    }
}
