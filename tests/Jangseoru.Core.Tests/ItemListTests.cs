using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Jangseoru.Core.Tests
{
    public class ItemListTests
    {
        private static ItemSource Src(ItemKind kind, string id, string name, bool image = true)
            => new ItemSource { Kind = kind, Id = id, Name = name, HasImage = image };

        private static ItemStore Store(params (ItemKind, string)[] owned)
        {
            var s = new ItemStore();
            foreach (var (k, id) in owned) s.Add(k, id);
            return s;
        }

        [Fact]
        public void Hides_items_without_image()
        {
            var rows = ItemList.Build(ItemKind.Misc, new[]
            {
                Src(ItemKind.Misc, "0000", "測試", image: false),
                Src(ItemKind.Misc, "0001", "금창약"),
            }, Store());
            Assert.Single(rows);
            Assert.Equal("0001", rows[0].Id);
        }

        [Fact]
        public void Locked_row_shows_hidden_label()
        {
            var rows = ItemList.Build(ItemKind.Book, new[] { Src(ItemKind.Book, "1000", "당문암기총강") }, Store());
            Assert.False(rows[0].Unlocked);
            Assert.Equal("???", rows[0].Label);
            Assert.Equal(ItemList.Hidden, rows[0].Label);
        }

        [Fact]
        public void Unlocked_row_shows_name()
        {
            var rows = ItemList.Build(ItemKind.Book, new[] { Src(ItemKind.Book, "1000", "당문암기총강") },
                Store((ItemKind.Book, "1000")));
            Assert.True(rows[0].Unlocked);
            Assert.Equal("당문암기총강", rows[0].Label);
            Assert.Equal(ItemKind.Book, rows[0].Kind);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Unreadable_name_stays_hidden_even_when_unlocked(string name)
        {
            var rows = ItemList.Build(ItemKind.Special, new[] { Src(ItemKind.Special, "1001", name) },
                Store((ItemKind.Special, "1001")));
            Assert.False(rows[0].Unlocked);
            Assert.Equal("???", rows[0].Label);
        }

        [Fact]
        public void Keeps_game_order_and_filters_by_kind()
        {
            var rows = ItemList.Build(ItemKind.Book, new[]
            {
                Src(ItemKind.Book, "1010", "b"),
                Src(ItemKind.Misc, "0001", "m"),
                Src(ItemKind.Book, "1000", "a"),
            }, Store());
            Assert.Equal(new[] { "1010", "1000" }, rows.Select(r => r.Id));
        }

        [Fact]
        public void An_unlock_of_another_kind_does_not_count()
        {
            var rows = ItemList.Build(ItemKind.Book, new[] { Src(ItemKind.Book, "1000", "a") }, Store((ItemKind.Misc, "1000")));
            Assert.False(rows[0].Unlocked);
        }

        [Fact]
        public void Null_inputs_give_empty_list_or_all_locked()
        {
            Assert.Empty(ItemList.Build(ItemKind.Book, null, Store()));
            var rows = ItemList.Build(ItemKind.Book, new List<ItemSource> { null, Src(ItemKind.Book, "1000", "a") }, null);
            Assert.Single(rows);
            Assert.False(rows[0].Unlocked);
        }

        [Fact]
        public void Progress_counts_unlocked()
        {
            var rows = ItemList.Build(ItemKind.Book, new[]
            {
                Src(ItemKind.Book, "1000", "a"), Src(ItemKind.Book, "1010", "b"), Src(ItemKind.Book, "1020", "c"),
            }, Store((ItemKind.Book, "1010")));
            Assert.Equal(OathModel.Progress(1, 3), ItemList.Progress(rows));
            Assert.Equal(OathModel.Progress(0, 0), ItemList.Progress(null));
        }
    }
}
