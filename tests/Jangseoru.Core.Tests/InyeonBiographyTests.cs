using System.Linq;
using Xunit;

namespace Jangseoru.Core.Tests
{
    [Collection("InyeonCatalog")]
    public class InyeonBiographyTests
    {
        [InyeonFact]
        public void Only_existing_text_within_preserved_stage_is_visible()
        {
            var store = new InyeonStore();
            store.RecordBiography("master", "master", 3);
            var pages = InyeonBiography.Pages("master", store, key =>
                key == "CharacterIntro1/master" ? "First" : key == "CharacterIntro3/master" ? "Third" : key);
            Assert.Equal(new[] { 1, 3 }, pages.Select(p => p.Stage));
            Assert.Equal(new[] { "First", "Third" }, pages.Select(p => p.Text));
            Assert.Empty(InyeonBiography.Pages("girl2", store, _ => "Spoiler"));
        }

        [InyeonFact]
        public void Alternate_biographies_keep_their_own_unlock_limits()
        {
            var store = new InyeonStore();
            store.RecordBiography("girl5", "girl5", 2);
            store.RecordBiography("girl5", "girl5_11", 1);
            var pages = InyeonBiography.Pages("girl5", store, key => key.EndsWith("girl5_11") ? "Other" : "Main");
            Assert.Equal(3, pages.Count);
            Assert.Single(pages, p => p.TextId == "girl5_11");
            Assert.Empty(InyeonBiography.Pages("master", store, _ => " "));
        }
    }
}
