using Xunit;

namespace Jangseoru.Core.Tests
{
    public class StorePathsTests
    {
        [Fact]
        public void ResolveGallery_is_sibling_of_byeolho_file()
        {
            Assert.Equal(@"C:\x\Obb Studio\JangseoruMod\gallery.txt", StorePaths.ResolveGallery(@"C:\x\Obb Studio\Mortal"));
            Assert.Equal(@"C:\x\Obb Studio\JangseoruMod\gallery.txt", StorePaths.ResolveGallery(@"C:\x\Obb Studio\Mortal" + "\\"));
            Assert.Equal(@"C:\사용자\Obb Studio\JangseoruMod\gallery.txt", StorePaths.ResolveGallery(@"C:\사용자\Obb Studio\Mortal"));
            Assert.Equal(
                System.IO.Path.GetDirectoryName(StorePaths.Resolve(@"C:\x\Obb Studio\Mortal")),
                System.IO.Path.GetDirectoryName(StorePaths.ResolveGallery(@"C:\x\Obb Studio\Mortal")));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(@"C:\")]
        public void ResolveGallery_without_a_parent_returns_null(string input)
        {
            Assert.Null(StorePaths.ResolveGallery(input));
        }
    }
}
