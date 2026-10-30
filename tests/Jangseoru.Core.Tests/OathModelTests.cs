using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Jangseoru.Core.Tests
{
    public class OathModelTests
    {
        private static KeyValuePair<string, int> P(string id, int count) => new KeyValuePair<string, int>(id, count);

        [Fact]
        public void Build_orders_by_numeric_id_and_numbers_from_one()
        {
            var cards = OathModel.Build(new[] { P("40010", 1), P("40002", 0), P("9", 3) });

            Assert.Equal(new[] { "9", "40002", "40010" }, cards.Select(c => c.Id).ToArray());
            Assert.Equal(new[] { 1, 2, 3 }, cards.Select(c => c.Number).ToArray());
        }

        [Fact]
        public void Unlocked_only_when_count_is_positive()
        {
            var cards = OathModel.Build(new[] { P("40004", 0), P("40005", -2), P("40006", 1) });

            Assert.Equal(new[] { false, false, true }, cards.Select(c => c.Unlocked).ToArray());
        }

        [Fact]
        public void Build_ignores_non_numeric_ids()
        {
            var cards = OathModel.Build(new[] { P("abc", 1), P("", 1), P(null, 1), P("40001", 1) });

            Assert.Single(cards);
            Assert.Equal("40001", cards[0].Id);
            Assert.Equal(1, cards[0].Number);
        }

        [Fact]
        public void Build_of_null_is_empty()
        {
            Assert.Empty(OathModel.Build(null));
        }

        [Fact]
        public void Progress_formats_zero_and_full()
        {
            Assert.Equal("0/6", OathModel.Progress(0, 6));
            Assert.Equal("6/6", OathModel.Progress(6, 6));
            Assert.Equal("5/6", OathModel.Progress(5, 6));
        }
    }
}
