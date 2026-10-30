using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Jangseoru.Core.Tests
{
    [Collection("InyeonCatalog")]
    public class OathStoreTests
    {
        private static KeyValuePair<string, int> P(string id, int count) => new KeyValuePair<string, int>(id, count);

        [Fact]
        public void Without_overrides_the_game_count_decides()
        {
            var cards = OathModel.Build(new[] { P("40000", 1), P("40001", 0) }, new OathStore());
            Assert.True(cards[0].Unlocked);
            Assert.False(cards[1].Unlocked);
        }

        [Fact]
        public void An_opened_oath_shows_even_if_the_game_never_counted_it()
        {
            var store = new OathStore();
            store.Open("40001");
            Assert.True(OathModel.Build(new[] { P("40001", 0) }, store)[0].Unlocked);
        }

        [Fact]
        public void After_a_reset_only_a_new_viewing_unlocks_again()
        {
            var store = new OathStore();
            store.Open("40001");
            store.ResetTo(new[] { P("40000", 2), P("40001", 0) }); // what the game has counted at the moment of the reset
            var cards = OathModel.Build(new[] { P("40000", 2), P("40001", 0) }, store);
            Assert.All(cards, c => Assert.False(c.Unlocked));
            Assert.True(OathModel.Build(new[] { P("40000", 3) }, store)[0].Unlocked); // seen once more after the reset
        }

        [Fact]
        public void Text_round_trip_and_junk()
        {
            var store = new OathStore();
            store.Open("40003");
            store.ResetTo(new[] { P("40000", 2) });
            store.Open("40005");
            var text = store.Serialize();
            Assert.StartsWith("jangseoru.oath v1\n", text);
            var back = OathStore.Parse(text, out var corrupt);
            Assert.False(corrupt);
            Assert.Equal(text, back.Serialize());
            Assert.True(back.Opened("40005") && !back.Opened("40003"));
            Assert.Equal(2, back.Baseline("40000"));

            var junk = OathStore.Parse("jangseoru.oath v1\nOpen:abc\nBase:40000:x\n@@\nOpen:40001\n", out var c2);
            Assert.False(c2);
            Assert.True(junk.Opened("40001"));
            Assert.Equal(0, junk.Baseline("40000"));
            OathStore.Parse("nonsense", out var c3);
            Assert.True(c3);
            Assert.True(OathStore.Parse("jangseoru.oath v2\nOpen:1\n", out _).ReadOnly);
        }

        [Fact]
        public void Admin_unlock_and_reset_cover_the_oaths()
        {
            var oath = new OathStore();
            var counts = new[] { P("40000", 1), P("40001", 0), P("40002", 0) };
            CollectionAdmin.UnlockAll(null, null, null, null, null, oath, counts.Select(c => c.Key));
            Assert.All(OathModel.Build(counts, oath), c => Assert.True(c.Unlocked));
            CollectionAdmin.Reset(null, null, null, null, oath, counts);
            Assert.All(OathModel.Build(counts, oath), c => Assert.False(c.Unlocked));
        }

        [Fact]
        public void A_read_only_oath_store_is_never_changed()
        {
            var oath = OathStore.Parse("jangseoru.oath v9\nOpen:1\n", out _);
            var before = oath.Serialize();
            CollectionAdmin.UnlockAll(null, null, null, null, null, oath, new[] { "40000" });
            CollectionAdmin.Reset(null, null, null, null, oath, new[] { P("40000", 1) });
            Assert.Equal(before, oath.Serialize());
        }
    }
}
