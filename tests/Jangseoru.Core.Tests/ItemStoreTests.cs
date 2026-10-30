using System.Linq;
using Xunit;

namespace Jangseoru.Core.Tests
{
    public class ItemStoreTests
    {
        [Fact]
        public void Add_accepts_valid_id_once()
        {
            var s = new ItemStore();
            Assert.True(s.Add(ItemKind.Book, "1000"));
            Assert.False(s.Add(ItemKind.Book, "1000"));
            Assert.True(s.Has(ItemKind.Book, "1000"));
            Assert.False(s.Has(ItemKind.Misc, "1000"));
            Assert.Equal(1, s.Count);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData(null)]
        [InlineData("a/b")]
        [InlineData("a\nb")]
        [InlineData("a:b")]
        [InlineData("1234567890123456789012345678901234567890X")] // 41 chars
        public void Add_rejects_invalid_id(string id)
        {
            var s = new ItemStore();
            Assert.False(s.Add(ItemKind.Special, id));
            Assert.Equal(0, s.Count);
        }

        [Fact]
        public void Serialize_roundtrips_in_stable_order()
        {
            var s = new ItemStore();
            s.Add(ItemKind.Special, "1001");
            s.Add(ItemKind.Book, "1010");
            s.Add(ItemKind.Book, "1000");
            s.Add(ItemKind.Misc, "0001");
            var text = s.Serialize();
            Assert.Equal("jangseoru.items v1\nBook:1000\nBook:1010\nMisc:0001\nSpecial:1001\n", text);

            var back = ItemStore.Parse(text, out var corrupt);
            Assert.False(corrupt);
            Assert.True(back.Has(ItemKind.Book, "1010"));
            Assert.True(back.Has(ItemKind.Special, "1001"));
            Assert.Equal(text, back.Serialize());
        }

        [Fact]
        public void Parse_unknown_kind_and_garbage_lines_are_kept_or_dropped_as_specified()
        {
            var text = "jangseoru.items v1\nBook:1000\nGadget:5\nthis is garbage\nBook:\n:7\n\nBook:1000\n";
            var s = ItemStore.Parse(text, out var corrupt);
            Assert.False(corrupt);
            Assert.True(s.Has(ItemKind.Book, "1000"));
            Assert.Equal(1, s.Count); // Gadget:5 is not counted as a known item
            var again = s.Serialize();
            Assert.Contains("Gadget:5\n", again);          // well-formed unknown kind survives a rewrite
            Assert.DoesNotContain("garbage", again);        // everything else is dropped
            Assert.Equal(1, again.Split('\n').Count(l => l == "Book:1000")); // duplicates collapse
        }

        [Fact]
        public void Parse_passthrough_is_capped()
        {
            var sb = new System.Text.StringBuilder("jangseoru.items v1\n");
            for (var i = 0; i < 2500; i++) sb.Append("Gadget:").Append(i).Append('\n');
            var s = ItemStore.Parse(sb.ToString(), out _);
            Assert.Equal(2000, s.Serialize().Split('\n').Count(l => l.StartsWith("Gadget:")));
        }

        [Fact]
        public void Parse_known_items_are_capped()
        {
            var sb = new System.Text.StringBuilder("jangseoru.items v1\n");
            for (var i = 0; i < 5000; i++) sb.Append("Book:").Append(i).Append('\n');
            var s = ItemStore.Parse(sb.ToString(), out _);
            Assert.Equal(4000, s.Count);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("garbage")]
        [InlineData("jangseoru.items v\nBook:1\n")]
        [InlineData("jangseoru.items v0\nBook:1\n")]
        public void Parse_garbled_header_is_corrupt(string text)
        {
            var s = ItemStore.Parse(text, out var corrupt);
            Assert.True(corrupt);
            Assert.False(s.ReadOnly);
            Assert.Equal(0, s.Count);
        }

        [Fact]
        public void Parse_newer_version_is_readonly_and_serialize_returns_original()
        {
            var text = "jangseoru.items v2\nBook:1000\nsomething new\n";
            var s = ItemStore.Parse(text, out var corrupt);
            Assert.False(corrupt);
            Assert.True(s.ReadOnly);
            Assert.Equal(text, s.Serialize());
        }

        [Fact]
        public void ItemKinds_prefix_and_label()
        {
            Assert.Equal("Book", ItemKinds.Prefix(ItemKind.Book));
            Assert.Equal("서적", ItemKinds.TabLabel(ItemKind.Book));
            Assert.Equal("잡물", ItemKinds.TabLabel(ItemKind.Misc));
            Assert.Equal("보물", ItemKinds.TabLabel(ItemKind.Special));
            Assert.True(ItemKinds.TryFromPrefix("Special", out var k));
            Assert.Equal(ItemKind.Special, k);
            Assert.False(ItemKinds.TryFromPrefix("Gadget", out _));
            Assert.False(ItemKinds.TryFromPrefix(null, out _));
        }

        [Fact]
        public void StorePaths_resolves_items_file_next_to_the_others()
        {
            var p = StorePaths.ResolveItems(@"C:\Users\x\AppData\LocalLow\Obb Studio\Mortal\123");
            Assert.EndsWith(@"JangseoruMod\items.txt", p);
            Assert.Null(StorePaths.ResolveItems(null));
        }
    }
}
