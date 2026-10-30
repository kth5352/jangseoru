using System;
using System.Collections.Generic;
using System.Globalization;

namespace Jangseoru.Core
{
    /// <summary>The game's three display languages (SystemSettings.LeanLanguages).</summary>
    public enum UiLanguage
    {
        Korean,
        ChineseTraditional,
        ChineseSimplified
    }

    public static class UiLanguages
    {
        /// <summary>The game's language name (LeanLocalization.CurrentLanguage); anything unknown or unreadable is Korean.</summary>
        public static UiLanguage FromGame(string name)
        {
            switch (name)
            {
                case "ChineseTraditional": return UiLanguage.ChineseTraditional;
                case "ChineseSimplified": return UiLanguage.ChineseSimplified;
                default: return UiLanguage.Korean;
            }
        }
    }

    /// <summary>Every text the mod shows itself (the game's own texts come from its localization), per display language.</summary>
    public static class UiText
    {
        public const string TabTitle = "TabTitle", Oath = "Oath", Event = "Event", Inyeon = "Inyeon";
        public const string Misc = "Misc", Book = "Book", Special = "Special";
        public const string Background = "Background", BigPicture = "BigPicture", Video = "Video";
        public const string LoadFailed = "LoadFailed", Biography = "Biography", People = "People";
        public const string ChoosePerson = "ChoosePerson", UnknownName = "UnknownName", NoPictures = "NoPictures";
        public const string Loading = "Loading", PictureFailed = "PictureFailed", Record = "Record";
        public const string ChoosePersonHint = "ChoosePersonHint", NoBiography = "NoBiography", NoBiographyYet = "NoBiographyYet";
        public const string ZoomHelp = "ZoomHelp", Close = "Close";
        public const string AdminQuestion = "AdminQuestion", AdminReset = "AdminReset", AdminUnlock = "AdminUnlock", AdminCancel = "AdminCancel";
        public const string AdminConfirmReset = "AdminConfirmReset", AdminConfirmUnlock = "AdminConfirmUnlock";

        // Korean, Traditional Chinese, Simplified Chinese.
        private static readonly Dictionary<string, string[]> Table = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [TabTitle] = new[] { "장서루", "藏書樓", "藏书楼" },
            [Oath] = new[] { "맹세", "盟誓", "盟誓" },
            [Event] = new[] { "사건", "事件", "事件" },
            [Inyeon] = new[] { "인연", "因緣", "因缘" },
            [Misc] = new[] { "잡물", "雜物", "杂物" },
            [Book] = new[] { "서적", "書籍", "书籍" },
            [Special] = new[] { "보물", "寶物", "宝物" },
            [Background] = new[] { "풍광", "風光", "风光" },
            [BigPicture] = new[] { "대사건", "大事件", "大事件" },
            [Video] = new[] { "환영", "幻影", "幻影" },
            [LoadFailed] = new[] { "이미지를 불러오지 못했습니다", "無法載入圖片", "无法载入图片" },
            [Biography] = new[] { "열전", "列傳", "列传" },
            [People] = new[] { "인물록", "人物錄", "人物录" },
            [ChoosePerson] = new[] { "인물 선택", "選擇人物", "选择人物" },
            [UnknownName] = new[] { "이름 미상", "姓名不詳", "姓名不详" },
            [NoPictures] = new[] { "수집한 그림이 없습니다", "尚未收集任何圖畫", "尚未收集任何图画" },
            [Loading] = new[] { "불러오는 중…", "載入中…", "载入中…" },
            [PictureFailed] = new[] { "그림을 불러올 수 없습니다", "無法載入圖畫", "无法载入图画" },
            [Record] = new[] { "기록 {0}", "記錄 {0}", "记录 {0}" },
            [ChoosePersonHint] = new[] { "인물을 선택하면 열전을 확인할 수 있습니다", "選擇人物即可查看列傳", "选择人物即可查看列传" },
            [NoBiography] = new[] { "열전이 없는 인물입니다", "此人物沒有列傳", "此人物没有列传" },
            [NoBiographyYet] = new[] { "아직 수집한 열전이 없습니다", "尚未收集列傳", "尚未收集列传" },
            [ZoomHelp] = new[] { "← → 그림 전환  ·  Esc 닫기", "← → 切換圖畫  ·  Esc 關閉", "← → 切换图画  ·  Esc 关闭" },
            [Close] = new[] { "닫기", "關閉", "关闭" },
            [AdminQuestion] = new[] { "장서루를 바꾸고 싶으십니까?", "要變更藏書樓嗎？", "要变更藏书楼吗？" },
            [AdminReset] = new[] { "초기화", "初始化", "初始化" },
            [AdminUnlock] = new[] { "전부 해금", "全部解鎖", "全部解锁" },
            [AdminCancel] = new[] { "취소", "取消", "取消" },
            [AdminConfirmReset] = new[] { "장서루 기록을 모두 지웁니다.\n정말 초기화하시겠습니까?", "將清除藏書樓的所有紀錄。\n確定要初始化嗎？", "将清除藏书楼的所有记录。\n确定要初始化吗？" },
            [AdminConfirmUnlock] = new[] { "장서루의 모든 항목을 엽니다.\n정말 전부 해금하시겠습니까?", "將解鎖藏書樓的所有項目。\n確定要全部解鎖嗎？", "将解锁藏书楼的所有项目。\n确定要全部解锁吗？" },
        };

        public static IReadOnlyCollection<string> Keys => Table.Keys;

        /// <summary>The text of <paramref name="key"/> in <paramref name="language"/>; an unknown key returns the key itself.</summary>
        public static string Get(string key, UiLanguage language)
        {
            if (key == null || !Table.TryGetValue(key, out var texts)) return key;
            var i = (int)language;
            return i >= 0 && i < texts.Length ? texts[i] : texts[0];
        }

        /// <summary><see cref="Get"/> with <c>{0}</c> filled in.</summary>
        public static string Format(string key, UiLanguage language, object arg)
            => string.Format(CultureInfo.InvariantCulture, Get(key, language), arg);
    }

    /// <summary>The game member the mod reads to know the display language; included in GameContract.Required.</summary>
    public static class UiContract
    {
        public static readonly MemberSpec CurrentLanguage =
            MemberSpec.Property("Lean.Localization.LeanLocalization", "CurrentLanguage", "System.String");

        public static readonly IReadOnlyList<MemberSpec> Required = new[] { CurrentLanguage };
    }
}
