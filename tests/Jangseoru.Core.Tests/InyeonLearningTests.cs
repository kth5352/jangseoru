using System;
using System.Linq;
using Xunit;

namespace Jangseoru.Core.Tests
{
    [Collection("InyeonCatalog")]
    public class InyeonLearningTests : IDisposable
    {
        private const string Folder = "Assets/__Project/Images/Characters/Brother1/";
        private static readonly string Fresh = Folder + "brand_new_face.png";

        public InyeonLearningTests() => InyeonCatalog.ForgetLearned();

        public void Dispose() => InyeonCatalog.ForgetLearned();

        [InyeonFact]
        public void A_known_person_accepts_a_new_portrait_path_in_the_character_images()
        {
            Assert.False(InyeonCatalog.IsPicture("brother1", Fresh));
            Assert.True(InyeonCatalog.Accepts("brother1", Fresh));
            Assert.True(InyeonCatalog.Accepts("brother1", InyeonCatalog.Find("brother1").Pictures[0])); // the built-in ones too
        }

        [InyeonTheory]
        [InlineData("nobody", "Assets/__Project/Images/Characters/Brother1/x.png")]          // an unknown person is never guessed
        [InlineData("brother1", "Assets/__Project/Images/Background/x.png")]                 // not a character image
        [InlineData("brother1", "Assets/__Project/Images/Characters/Misc_group/x.png")]      // the generic folders were left out on purpose
        [InlineData("brother1", "Assets/__Project/Images/Characters/Brother1/x.jpg")]
        [InlineData("brother1", "Assets/__Project/Images/Characters/../Brother1/x.png")]
        [InlineData("brother1", "Assets/__Project/Images/Characters/Brother1/a\tb.png")]
        [InlineData("brother1", "Assets/__Project/Images/Characters/Brother1/a\nb.png")]
        [InlineData("brother1", null)]
        [InlineData(null, "Assets/__Project/Images/Characters/Brother1/x.png")]
        public void Anything_else_is_refused(string person, string key)
        {
            Assert.False(InyeonCatalog.Accepts(person, key));
        }

        [InyeonFact]
        public void A_learned_portrait_joins_the_persons_pictures_once()
        {
            var before = InyeonCatalog.Find("brother1").Pictures.Count;
            Assert.True(InyeonCatalog.Learn("brother1", Fresh));
            Assert.False(InyeonCatalog.Learn("brother1", Fresh));
            Assert.Equal(before + 1, InyeonCatalog.Find("brother1").Pictures.Count);
            Assert.True(InyeonCatalog.IsPicture("brother1", Fresh));
            Assert.False(InyeonCatalog.Learn("nobody", Fresh));
        }

        [InyeonFact]
        public void A_picture_one_person_owns_is_never_moved_to_another()
        {
            InyeonCatalog.Learn("brother1", Fresh);
            Assert.False(InyeonCatalog.Accepts("brother2", Fresh)); // learned for brother1
            var theirs = InyeonCatalog.Find("brother1").Pictures.First(k => k != Fresh);
            Assert.False(InyeonCatalog.Accepts("brother2", theirs)); // built in for brother1
            Assert.False(InyeonCatalog.Learn("brother2", Fresh));
        }

        [InyeonFact]
        public void Learning_forgets_nothing_built_in()
        {
            var builtIn = InyeonCatalog.Find("brother1").Pictures.ToList();
            InyeonCatalog.Learn("brother1", Fresh);
            Assert.All(builtIn, k => Assert.Contains(k, InyeonCatalog.Find("brother1").Pictures));
        }

        [InyeonFact]
        public void The_store_records_a_new_portrait_and_a_restart_reads_it_back()
        {
            var store = new InyeonStore();
            Assert.True(store.RecordPicture("brother1", Fresh));
            Assert.Contains(Fresh, store.Pictures("brother1"));
            var text = store.Serialize();
            Assert.Contains("P\tbrother1\t" + Fresh, text);

            InyeonCatalog.ForgetLearned(); // a new process: the catalog knows nothing of it yet
            var back = InyeonStore.Parse(text, out var corrupt);
            Assert.False(corrupt);
            Assert.Contains(Fresh, back.Pictures("brother1"));
            Assert.True(back.IsKnown("brother1"));
        }

        [InyeonFact]
        public void A_portrait_of_an_unknown_person_stays_in_the_file_untouched()
        {
            var line = "P\tsomeone_new\t" + Folder + "x.png";
            var store = InyeonStore.Parse("jangseoru.inyeon v1\n" + line + "\n", out _);
            Assert.Contains(line, store.Serialize());
            Assert.Null(InyeonCatalog.Find("someone_new"));
        }

        [InyeonFact]
        public void Learning_is_capped()
        {
            var learned = 0;
            for (var i = 0; i < InyeonCatalog.MaxLearned + 50; i++)
                if (InyeonCatalog.Learn("brother1", Folder + "gen_" + i + ".png")) learned++;
            Assert.Equal(InyeonCatalog.MaxLearned, learned);
        }
    }
}
