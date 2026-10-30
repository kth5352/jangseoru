using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Jangseoru.Core.Tests
{
    [Collection("InyeonCatalog")]
    public class InyeonStoreTests
    {
        private const string Master = "Assets/__Project/Images/Characters/Master/normal.png";

        [InyeonFact]
        public void Only_seen_valid_pictures_are_collected_once()
        {
            var s = new InyeonStore();
            Assert.True(s.RecordPicture("master", Master));
            Assert.False(s.RecordPicture("master", Master));
            Assert.False(s.RecordPicture("girl2", Master));
            Assert.False(s.RecordPicture("master", "unknown"));
            Assert.Equal(new[] { Master }, s.Pictures("master"));
            Assert.Empty(s.Pictures("girl2"));
            Assert.True(s.IsKnown("master"));
            Assert.False(s.IsKnown("girl2"));
        }

        [InyeonFact]
        public void Biography_keeps_highest_stage_and_does_not_unlock_pictures()
        {
            var s = new InyeonStore();
            Assert.True(s.RecordBiography("master", "master", 3));
            Assert.False(s.RecordBiography("master", "master", 1));
            Assert.False(s.RecordBiography("master", "master", 5));
            Assert.False(s.RecordBiography("girl2", "master", 4));
            Assert.Equal(3, s.BiographyStage("master", "master"));
            Assert.Empty(s.Pictures("master"));
            Assert.True(s.IsKnown("master"));
        }

        [InyeonFact]
        public void Roundtrip_preserves_independent_people_and_unknown_future_records()
        {
            var s = new InyeonStore();
            s.RecordPicture("master", Master);
            s.RecordBiography("girl2", "girl2", 2);
            var text = s.Serialize() + "X\tfuture\tvalue\nP\tnew_person\tnew_picture\n";
            var parsed = InyeonStore.Parse(text, out var corrupt);
            Assert.False(corrupt);
            Assert.Equal(new[] { Master }, parsed.Pictures("master"));
            Assert.Equal(2, parsed.BiographyStage("girl2", "girl2"));
            Assert.Equal(0, parsed.BiographyStage("master", "master"));
            Assert.Contains("X\tfuture\tvalue", parsed.Serialize());
            Assert.Contains("P\tnew_person\tnew_picture", parsed.Serialize());
        }

        [InyeonFact]
        public void Missing_header_is_corrupt_and_future_version_is_readonly_verbatim()
        {
            InyeonStore.Parse("P\tmaster\tx", out var corrupt);
            Assert.True(corrupt);
            const string future = "jangseoru.inyeon v2\r\nnew data\r\n";
            var s = InyeonStore.Parse(future, out corrupt);
            Assert.False(corrupt);
            Assert.True(s.ReadOnly);
            s.RecordPicture("master", Master);
            Assert.Equal(future, s.Serialize());
        }

        [InyeonTheory]
        [InlineData(-1, false, 0)] [InlineData(0, false, 1)] [InlineData(39, false, 1)]
        [InlineData(40, false, 2)] [InlineData(69, false, 2)] [InlineData(70, false, 3)]
        [InlineData(99, false, 3)] [InlineData(100, false, 4)] [InlineData(100, true, 2)]
        public void Biography_stage_matches_game_thresholds_and_prologue(int value, bool prologue, int expected)
            => Assert.Equal(expected, InyeonStore.StageForAffection(value, prologue));

        [InyeonFact]
        public void File_debounces_and_protects_future_data()
        {
            var dir = Path.Combine(Path.GetTempPath(), "inyeon-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var path = Path.Combine(dir, "inyeon.txt");
                var now = new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc);
                var file = new InyeonFile(path, null, TimeSpan.FromSeconds(2));
                file.Load(now);
                file.Store.RecordPicture("master", Master);
                file.MarkDirty(now);
                file.FlushIfDue(now.AddSeconds(1));
                Assert.False(File.Exists(path));
                file.FlushIfDue(now.AddSeconds(2));
                Assert.Contains(Master, File.ReadAllText(path));
                File.WriteAllText(path, "jangseoru.inyeon v9\nfuture");
                file.Load(now);
                file.MarkDirty(now);
                file.FlushNow();
                Assert.True(file.WritesBlocked);
                Assert.Equal("jangseoru.inyeon v9\nfuture", File.ReadAllText(path));
            }
            finally { Directory.Delete(dir, true); }
        }

        [InyeonFact]
        public void Navigation_wraps_collected_pictures_and_resets_for_another_person()
        {
            var store = new InyeonStore();
            var model = new InyeonSelection(store);
            model.Select("master");
            model.Move(-1);
            Assert.Equal("0/0", model.Counter);
            Assert.Null(model.CurrentKey);
            foreach (var key in InyeonCatalog.Find("master").Pictures.Take(4)) store.RecordPicture("master", key);
            model.Select("master");
            Assert.Equal("1/4", model.Counter);
            model.Move(1);
            Assert.Equal("2/4", model.Counter);
            model.Move(-2);
            Assert.Equal("4/4", model.Counter);
            model.Select("girl2");
            Assert.Equal("0/0", model.Counter);
            store.RecordPicture("girl2", InyeonCatalog.Find("girl2").Pictures[0]);
            model.Select("girl2");
            model.Move(1);
            Assert.Equal("1/1", model.Counter);
            Assert.False(model.CanMove);
        }
    }
}
