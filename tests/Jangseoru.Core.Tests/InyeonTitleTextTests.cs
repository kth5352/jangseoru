using Xunit;

namespace Jangseoru.Core.Tests
{
    public class InyeonTitleTextTests
    {
        [Theory]
        [InlineData("{title}", "무명소졸", "무명소졸")]
        [InlineData("{title}", null, "")]
        [InlineData("{title}", "  ", "")]
        [InlineData("전대 {title}", "협객", "전대 협객")]
        [InlineData("당문 장문인", "협객", "당문 장문인")]
        [InlineData(null, "협객", "")]
        public void FillPlayerTitle_replaces_the_placeholder_or_drops_it(string text, string playerTitle, string expected)
            => Assert.Equal(expected, InyeonTitleText.FillPlayerTitle(text, playerTitle));
    }
}
