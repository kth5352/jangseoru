using System;
using Xunit;

namespace Jangseoru.Core.Tests
{
    public class UiTextTests
    {
        [Theory]
        [InlineData("Korean", UiLanguage.Korean)]
        [InlineData("ChineseTraditional", UiLanguage.ChineseTraditional)]
        [InlineData("ChineseSimplified", UiLanguage.ChineseSimplified)]
        public void Game_language_names_map_to_ui_languages(string game, UiLanguage expected)
        {
            Assert.Equal(expected, UiLanguages.FromGame(game));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("English")]
        public void Unknown_or_unreadable_language_falls_back_to_korean(string game)
        {
            Assert.Equal(UiLanguage.Korean, UiLanguages.FromGame(game));
        }

        [Fact]
        public void Every_key_has_text_in_every_language()
        {
            foreach (var key in UiText.Keys)
                foreach (UiLanguage lang in Enum.GetValues(typeof(UiLanguage)))
                    Assert.False(string.IsNullOrWhiteSpace(UiText.Get(key, lang)), key + " / " + lang);
        }

        [Fact]
        public void The_library_tab_and_categories_are_translated()
        {
            Assert.Equal("장서루", UiText.Get(UiText.TabTitle, UiLanguage.Korean));
            Assert.Equal("藏書樓", UiText.Get(UiText.TabTitle, UiLanguage.ChineseTraditional));
            Assert.Equal("藏书楼", UiText.Get(UiText.TabTitle, UiLanguage.ChineseSimplified));
            Assert.Equal("因緣", UiText.Get(UiText.Inyeon, UiLanguage.ChineseTraditional));
            Assert.Equal("因缘", UiText.Get(UiText.Inyeon, UiLanguage.ChineseSimplified));
        }

        [Fact]
        public void Item_and_media_tab_labels_follow_the_language()
        {
            Assert.Equal("서적", ItemKinds.TabLabel(ItemKind.Book, UiLanguage.Korean));
            Assert.Equal("書籍", ItemKinds.TabLabel(ItemKind.Book, UiLanguage.ChineseTraditional));
            Assert.Equal("书籍", ItemKinds.TabLabel(ItemKind.Book, UiLanguage.ChineseSimplified));
            Assert.Equal("풍광", MediaKinds.TabLabel(MediaKind.Background, UiLanguage.Korean));
            Assert.Equal("風光", MediaKinds.TabLabel(MediaKind.Background, UiLanguage.ChineseTraditional));
            Assert.Equal("风光", MediaKinds.TabLabel(MediaKind.Background, UiLanguage.ChineseSimplified));
            Assert.Equal("幻影", MediaKinds.TabLabel(MediaKind.Video, UiLanguage.ChineseSimplified));
            // The one-argument form keeps returning Korean.
            Assert.Equal(ItemKinds.TabLabel(ItemKind.Misc, UiLanguage.Korean), ItemKinds.TabLabel(ItemKind.Misc));
            Assert.Equal(MediaKinds.TabLabel(MediaKind.BigPicture, UiLanguage.Korean), MediaKinds.TabLabel(MediaKind.BigPicture));
        }

        [Fact]
        public void The_f11_popup_asks_in_the_users_words()
        {
            Assert.Equal("장서루를 바꾸고 싶으십니까?", UiText.Get(UiText.AdminQuestion, UiLanguage.Korean));
            Assert.Equal("초기화", UiText.Get(UiText.AdminReset, UiLanguage.Korean));
            Assert.Equal("전부 해금", UiText.Get(UiText.AdminUnlock, UiLanguage.Korean));
            Assert.Equal("취소", UiText.Get(UiText.AdminCancel, UiLanguage.Korean));
        }

        [Fact]
        public void Numbered_text_and_unknown_keys()
        {
            Assert.Equal("기록 2", UiText.Format(UiText.Record, UiLanguage.Korean, 2));
            Assert.Equal("記錄 2", UiText.Format(UiText.Record, UiLanguage.ChineseTraditional, 2));
            Assert.Equal("no.such.key", UiText.Get("no.such.key", UiLanguage.Korean));
        }

        [Fact]
        public void The_current_language_member_is_in_the_game_contract()
        {
            Assert.Contains(UiContract.CurrentLanguage, GameContract.Required);
        }
    }
}
