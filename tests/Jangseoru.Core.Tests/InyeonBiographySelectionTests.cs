using Xunit;

namespace Jangseoru.Core.Tests
{
    [Collection("InyeonCatalog")]
    public class InyeonBiographySelectionTests
    {
        [InyeonFact]
        public void Four_unlocked_stages_start_at_one_and_use_chinese_numerals()
        {
            var store = new InyeonStore();
            store.RecordBiography("master", "master", 4);
            var model = new InyeonBiographySelection();
            model.Reset(InyeonBiography.Pages("master", store, key => "Text " + key));
            Assert.Equal(1, model.Current.Stage);
            Assert.Equal(new[] { "一", "二", "三", "四" }, new[] {
                InyeonBiographySelection.Label(1), InyeonBiographySelection.Label(2),
                InyeonBiographySelection.Label(3), InyeonBiographySelection.Label(4) });
            model.SelectStage(4);
            Assert.Equal(4, model.Current.Stage);
            model.Reset(InyeonBiography.Pages("master", store, key => "Text " + key));
            Assert.Equal(1, model.Current.Stage);
        }

        [InyeonFact]
        public void Missing_or_locked_stages_cannot_be_selected()
        {
            var store = new InyeonStore();
            store.RecordBiography("master", "master", 3);
            var model = new InyeonBiographySelection();
            model.Reset(InyeonBiography.Pages("master", store, key => key.Contains("Intro2/") ? "Second" : key));
            Assert.Equal(2, model.Current.Stage); // first existing collected text if stage I has no body
            Assert.False(model.HasStage(1));
            model.SelectStage(4);
            Assert.Equal(2, model.Current.Stage);
            model.Reset(null);
            Assert.Null(model.Current);
            model.MoveSource(1);
            model.SelectStage(1);
            Assert.Null(model.Current);
        }

        [InyeonFact]
        public void Alternate_biographies_remain_accessible_without_mixing_their_stages()
        {
            var store = new InyeonStore();
            store.RecordBiography("girl5", "girl5", 4);
            store.RecordBiography("girl5", "girl5_11", 1);
            var model = new InyeonBiographySelection();
            model.Reset(InyeonBiography.Pages("girl5", store, key => "Text " + key));
            Assert.Equal(2, model.SourceCount);
            model.SelectStage(4);
            model.MoveSource(1);
            Assert.Equal("girl5_11", model.Current.TextId);
            Assert.Equal(1, model.Current.Stage);
            Assert.False(model.HasStage(4));
            model.MoveSource(1);
            Assert.Equal("girl5", model.Current.TextId);
            Assert.Equal(1, model.Current.Stage);
        }
    }
}
