using System.Linq;
using Xunit;

namespace Jangseoru.Core.Tests
{
    [Collection("InyeonCatalog")]
    public class InyeonPictureStripTests
    {
        private static InyeonSelection FourPictures(out InyeonStore store)
        {
            store = new InyeonStore();
            var model = new InyeonSelection(store);
            foreach (var key in InyeonCatalog.Find("master").Pictures.Take(4)) store.RecordPicture("master", key);
            model.Select("master");
            return model;
        }

        [InyeonFact]
        public void SelectIndex_chooses_that_picture_and_ignores_out_of_range()
        {
            var model = FourPictures(out var store);
            model.SelectIndex(2);
            Assert.Equal(store.Pictures("master")[2], model.CurrentKey);
            Assert.Equal(2, model.Index);
            model.SelectIndex(9);
            model.SelectIndex(-1);
            Assert.Equal(2, model.Index);
            Assert.Equal(4, model.Keys.Count);
        }
    }
}
