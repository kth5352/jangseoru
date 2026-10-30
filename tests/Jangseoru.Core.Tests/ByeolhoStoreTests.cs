using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace Jangseoru.Core.Tests
{
    public class ByeolhoStoreTests
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 20, 4, 0, 0, DateTimeKind.Utc);
        private const long T1 = 639280660540071068L;

        private static SnapshotKey Key(string profile = "76561198000000000", string stem = "001", long tick = T1)
            => new SnapshotKey(profile, stem, tick);

        private static int[] Active(ByeolhoStore s) => s.ActiveCollected.ToArray();
        private static string B64(string v) => Convert.ToBase64String(Encoding.UTF8.GetBytes(v));

        [Fact]
        public void Collect_valid_range_and_global_return_value()
        {
            var s = new ByeolhoStore();

            Assert.True(s.Collect(0, Now, true));
            Assert.True(s.Collect(25, Now, true));
            Assert.False(s.Collect(25, Now, true));
            Assert.Equal(new[] { 0, 25 }, s.Collected.ToArray());
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(26)]
        [InlineData(int.MaxValue)]
        [InlineData(int.MinValue)]
        public void Collect_out_of_range_is_ignored_and_changes_nothing(int id)
        {
            var s = new ByeolhoStore();

            Assert.False(s.Collect(id, Now, true));
            Assert.Empty(s.Collected);
            Assert.Empty(s.ActiveCollected);
        }

        [Fact]
        public void Active_change_when_global_already_contains_id()
        {
            var s = new ByeolhoStore();
            Assert.True(s.Collect(5, Now, includeActive: false));
            Assert.Empty(s.ActiveCollected);

            Assert.False(s.Collect(5, Now, includeActive: true));

            Assert.Equal(new[] { 5 }, Active(s));
        }

        [Fact]
        public void Collected_is_sorted_ascending_for_both_lists()
        {
            var s = new ByeolhoStore();
            foreach (var id in new[] { 9, 2, 25, 0, 11 }) s.Collect(id, Now, true);

            Assert.Equal(new[] { 0, 2, 9, 11, 25 }, s.Collected.ToArray());
            Assert.Equal(new[] { 0, 2, 9, 11, 25 }, Active(s));
        }

        [Fact]
        public void Returned_collections_cannot_mutate_internal_state()
        {
            var s = new ByeolhoStore();
            s.Collect(1, Now, true);

            Assert.Throws<NotSupportedException>(() => ((ICollection<int>)s.Collected).Add(7));
            Assert.Throws<NotSupportedException>(() => ((ICollection<int>)s.ActiveCollected).Add(7));

            Assert.Equal(new[] { 1 }, s.Collected.ToArray());
            Assert.Equal(new[] { 1 }, Active(s));
        }

        [Fact]
        public void Snapshot_is_a_copy()
        {
            var s = new ByeolhoStore();
            s.Collect(1, Now, true);
            Assert.True(s.RecordSnapshot(Key()));
            s.Collect(2, Now, true);

            Assert.Equal(SlotMatch.Exact, s.ActivateSnapshot(Key()));

            Assert.Equal(new[] { 1 }, Active(s));
        }

        [Fact]
        public void Exact_token_restores()
        {
            var s = new ByeolhoStore();
            s.Collect(1, Now, true);
            s.RecordSnapshot(Key(tick: T1));
            s.Collect(2, Now, true);
            s.RecordSnapshot(Key(tick: T1 + 1000));
            s.ClearActive();

            Assert.Equal(SlotMatch.Exact, s.ActivateSnapshot(Key(tick: T1)));
            Assert.Equal(new[] { 1 }, Active(s));
            Assert.Equal(SlotMatch.Exact, s.ActivateSnapshot(Key(tick: T1 + 1000)));
            Assert.Equal(new[] { 1, 2 }, Active(s));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(-1)]
        public void One_tick_difference_is_mismatch_and_empties_active(long delta)
        {
            var s = new ByeolhoStore();
            s.Collect(1, Now, true);
            s.RecordSnapshot(Key(tick: T1));

            Assert.Equal(SlotMatch.Mismatch, s.ActivateSnapshot(Key(tick: T1 + delta)));

            Assert.Empty(s.ActiveCollected);
            Assert.Equal(new[] { 1 }, s.Collected.ToArray());
        }

        [Fact]
        public void Unknown_slot_is_None_and_null_key_is_None_and_both_empty_active()
        {
            var s = new ByeolhoStore();
            s.Collect(1, Now, true);
            s.RecordSnapshot(Key());

            Assert.Equal(SlotMatch.None, s.ActivateSnapshot(Key(stem: "002")));
            Assert.Empty(s.ActiveCollected);

            s.Collect(3, Now, true);
            Assert.Equal(SlotMatch.None, s.ActivateSnapshot(null));
            Assert.Empty(s.ActiveCollected);
        }

        [Fact]
        public void Profiles_and_stems_are_isolated()
        {
            var s = new ByeolhoStore();
            s.Collect(1, Now, true); s.RecordSnapshot(Key("A", "001"));
            s.ClearActive(); s.Collect(2, Now, true); s.RecordSnapshot(Key("B", "001"));
            s.ClearActive(); s.Collect(3, Now, true); s.RecordSnapshot(Key("A", "002"));

            Assert.Equal(SlotMatch.Exact, s.ActivateSnapshot(Key("A", "001")));
            Assert.Equal(new[] { 1 }, Active(s));
            Assert.Equal(SlotMatch.Exact, s.ActivateSnapshot(Key("B", "001")));
            Assert.Equal(new[] { 2 }, Active(s));
            Assert.Equal(SlotMatch.Exact, s.ActivateSnapshot(Key("A", "002")));
            Assert.Equal(new[] { 3 }, Active(s));
            Assert.Equal(SlotMatch.None, s.ActivateSnapshot(Key("C", "001")));
        }

        [Fact]
        public void Failed_new_save_candidate_preserves_old_snapshot()
        {
            var s = new ByeolhoStore();
            s.Collect(1, Now, true);
            s.RecordSnapshot(Key(tick: T1));
            s.Collect(2, Now, true);
            s.RecordSnapshot(Key(tick: T1 + 5000)); // the game's write failed, but the attempt was observed

            Assert.Equal(SlotMatch.Exact, s.ActivateSnapshot(Key(tick: T1)));
            Assert.Equal(new[] { 1 }, Active(s));
        }

        [Fact]
        public void Conflicting_same_key_is_never_activated()
        {
            var s = new ByeolhoStore();
            s.Collect(1, Now, true);
            Assert.True(s.RecordSnapshot(Key()));
            Assert.False(s.RecordSnapshot(Key())); // identical list: ignored

            s.Collect(2, Now, true);
            Assert.True(s.RecordSnapshot(Key())); // same key, different list: becomes ambiguous
            Assert.False(s.RecordSnapshot(Key())); // already ambiguous

            Assert.Equal(SlotMatch.Ambiguous, s.ActivateSnapshot(Key()));
            Assert.Empty(s.ActiveCollected);
        }

        [Fact]
        public void New_game_does_not_inherit_active_titles()
        {
            var s = new ByeolhoStore();
            s.Collect(1, Now, true);
            s.Collect(2, Now, true);
            s.RecordSnapshot(Key(tick: T1));

            s.ClearActive();
            s.Collect(0, Now, true);
            s.RecordSnapshot(Key(tick: T1 + 9000));

            Assert.Equal(SlotMatch.Exact, s.ActivateSnapshot(Key(tick: T1 + 9000)));
            Assert.Equal(new[] { 0 }, Active(s));
            Assert.Equal(new[] { 0, 1, 2 }, s.Collected.ToArray());
        }

        [Fact]
        public void SnapshotKey_equality_and_validation()
        {
            Assert.Equal(Key("a", "001", 5), Key("a", "001", 5));
            Assert.Equal(Key("a", "001", 5).GetHashCode(), Key("a", "001", 5).GetHashCode());
            Assert.NotEqual(Key("a", "001", 5), Key("a", "001", 6));
            Assert.NotEqual(Key("a", "001", 5), Key("b", "001", 5));
            Assert.NotEqual(Key("a", "001", 5), Key("a", "002", 5));

            Assert.Throws<ArgumentException>(() => new SnapshotKey(null, "001", 5));
            Assert.Throws<ArgumentException>(() => new SnapshotKey("a", "", 5));
            Assert.Throws<ArgumentException>(() => new SnapshotKey("a", "001", 0));
            Assert.Throws<ArgumentException>(() => new SnapshotKey("a", "001", -1));
        }

        [Fact]
        public void Serialize_writes_documented_v1_format()
        {
            var s = new ByeolhoStore { GameVersion = "release_1.0.6000.4" };
            s.Collect(5, new DateTime(100, DateTimeKind.Utc), true);
            s.Collect(1, new DateTime(200, DateTimeKind.Utc), true);
            s.RecordSnapshot(new SnapshotKey("사용자", "auto", 77));

            var expected = "jangseoru.byeolho v1\n"
                         + "game " + B64("release_1.0.6000.4") + "\n"
                         + "global 1 200\n"
                         + "global 5 100\n"
                         + "snapshot " + B64("사용자") + " " + B64("auto") + " 77 1,5\n";
            Assert.Equal(expected, s.Serialize());
        }

        [Fact]
        public void Collect_keeps_the_first_seen_time_only()
        {
            var s = new ByeolhoStore();
            s.Collect(5, new DateTime(100, DateTimeKind.Utc), false);
            s.Collect(5, new DateTime(999, DateTimeKind.Utc), false);

            Assert.Contains("global 5 100\n", s.Serialize());
            Assert.DoesNotContain("999", s.Serialize());
        }

        [Fact]
        public void Roundtrip_preserves_all_generations_and_ambiguity()
        {
            var s = new ByeolhoStore { GameVersion = "release_1.0.6000.4" };
            s.Collect(0, Now, true);
            s.Collect(7, Now, true);
            s.RecordSnapshot(new SnapshotKey("사용자 1", "001", 10));
            s.Collect(9, Now, true);
            s.RecordSnapshot(new SnapshotKey("사용자 1", "001", 20));
            s.ClearActive();
            s.RecordSnapshot(new SnapshotKey("p", "auto", 30)); // empty list is written as "-"
            s.Collect(3, Now, true);
            s.RecordSnapshot(new SnapshotKey("p", "auto", 30)); // conflict -> ambiguous

            var text = s.Serialize();
            var parsed = ByeolhoStore.Parse(text, out var corrupt);

            Assert.False(corrupt);
            Assert.False(parsed.ReadOnly);
            Assert.Equal(text, parsed.Serialize());
            Assert.Equal(s.Collected.ToArray(), parsed.Collected.ToArray());
            Assert.Equal("release_1.0.6000.4", parsed.GameVersion);
            Assert.Equal(SlotMatch.Exact, parsed.ActivateSnapshot(new SnapshotKey("사용자 1", "001", 10)));
            Assert.Equal(new[] { 0, 7 }, Active(parsed));
            Assert.Equal(SlotMatch.Exact, parsed.ActivateSnapshot(new SnapshotKey("사용자 1", "001", 20)));
            Assert.Equal(new[] { 0, 7, 9 }, Active(parsed));
            Assert.Equal(SlotMatch.Ambiguous, parsed.ActivateSnapshot(new SnapshotKey("p", "auto", 30)));
        }

        [Fact]
        public void Empty_list_snapshot_roundtrips_as_dash()
        {
            var s = new ByeolhoStore();
            s.RecordSnapshot(new SnapshotKey("p", "001", 5));

            var text = s.Serialize();
            Assert.Contains(" 5 -\n", text);

            var parsed = ByeolhoStore.Parse(text, out var corrupt);
            Assert.False(corrupt);
            Assert.Equal(SlotMatch.Exact, parsed.ActivateSnapshot(new SnapshotKey("p", "001", 5)));
            Assert.Empty(parsed.ActiveCollected);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   \n\n")]
        [InlineData("hello")]
        [InlineData("jangseoru.byeolho")]
        [InlineData("jangseoru.byeolho v0\n")]
        [InlineData("jangseoru.byeolho vX\n")]
        [InlineData("jangseoru.byeolho v-1\n")]
        [InlineData("\0\u0001\u0002binary garbage ��")]
        public void Invalid_header_is_corrupt_and_yields_empty_writable_store(string text)
        {
            var s = ByeolhoStore.Parse(text, out var corrupt);

            Assert.True(corrupt);
            Assert.False(s.ReadOnly);
            Assert.Empty(s.Collected);
        }

        [Fact]
        public void Null_text_is_corrupt_without_throwing()
        {
            var s = ByeolhoStore.Parse(null, out var corrupt);
            Assert.True(corrupt);
            Assert.Empty(s.Collected);
        }

        [Theory]
        [InlineData("global 26 100")]
        [InlineData("global -1 100")]
        [InlineData("global 1 0")]
        [InlineData("global 1 abc")]
        [InlineData("global 1")]
        [InlineData("global 1 5\nglobal 1 6")]
        [InlineData("game !!!")]
        [InlineData("game")]
        [InlineData("snapshot !!! QQ== 5 -")]
        [InlineData("snapshot QQ== QQ== 5 1,x")]
        [InlineData("snapshot QQ== QQ== -5 -")]
        [InlineData("snapshot QQ== QQ== 5 1,1")]
        [InlineData("snapshot QQ== QQ== 5 26")]
        [InlineData("snapshot QQ== QQ== 5")]
        [InlineData("snapshot QQ== 5 -")]
        [InlineData("ambiguous QQ== QQ==")]
        [InlineData("ambiguous QQ== QQ== 0")]
        public void Known_record_syntax_errors_are_corrupt(string body)
        {
            var s = ByeolhoStore.Parse("jangseoru.byeolho v1\n" + body + "\n", out var corrupt);

            Assert.True(corrupt);
            Assert.Empty(s.Collected);
            Assert.False(s.ReadOnly);
        }

        [Fact]
        public void Future_header_is_readonly_and_roundtrips_verbatim()
        {
            var text = "jangseoru.byeolho v2\nsomething new \u0000 we do not understand\nglobal 99 -5\n";

            var s = ByeolhoStore.Parse(text, out var corrupt);

            Assert.False(corrupt);
            Assert.True(s.ReadOnly);
            Assert.Equal(text, s.Serialize());
        }

        [Fact]
        public void Unknown_lines_are_preserved()
        {
            var text = "jangseoru.byeolho v1\n"
                     + "global 3 50\n"
                     + "future-record keep me exactly\n";

            var s = ByeolhoStore.Parse(text, out var corrupt);
            Assert.False(corrupt);
            Assert.False(s.ReadOnly);
            Assert.Equal(new[] { 3 }, s.Collected.ToArray());

            var again = s.Serialize();
            Assert.Contains("future-record keep me exactly\n", again);

            var twice = ByeolhoStore.Parse(again, out _);
            Assert.Equal(again, twice.Serialize());
        }

        [Fact]
        public void Duplicate_snapshot_lines_with_different_lists_become_ambiguous()
        {
            var text = "jangseoru.byeolho v1\n"
                     + "snapshot " + B64("p") + " " + B64("001") + " 9 1\n"
                     + "snapshot " + B64("p") + " " + B64("001") + " 9 2\n";

            var s = ByeolhoStore.Parse(text, out var corrupt);

            Assert.False(corrupt);
            Assert.Equal(SlotMatch.Ambiguous, s.ActivateSnapshot(new SnapshotKey("p", "001", 9)));
        }

        [Fact]
        public void Parse_accepts_crlf_line_endings()
        {
            var s = ByeolhoStore.Parse("jangseoru.byeolho v1\r\nglobal 4 8\r\n", out var corrupt);

            Assert.False(corrupt);
            Assert.Equal(new[] { 4 }, s.Collected.ToArray());
        }
    }
}
