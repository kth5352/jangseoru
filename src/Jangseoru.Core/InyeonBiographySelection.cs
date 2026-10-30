using System;
using System.Collections.Generic;
using System.Linq;

namespace Jangseoru.Core
{
    /// <summary>Chapter tabs select collected text within one biography; each opening starts at its first chapter.</summary>
    public sealed class InyeonBiographySelection
    {
        private IReadOnlyList<InyeonBiographyPage> pages = new InyeonBiographyPage[0];
        private string[] sources = new string[0];
        private int sourceIndex, stage;
        public int SourceCount => sources.Length;
        public int SourceIndex => sourceIndex;
        public InyeonBiographyPage Current => pages.FirstOrDefault(p => sources.Length > 0 && p.TextId == sources[sourceIndex] && p.Stage == stage);
        public bool HasStage(int value) => sources.Length > 0 && pages.Any(p => p.TextId == sources[sourceIndex] && p.Stage == value);
        public static string Label(int value) => value >= 1 && value <= 4 ? new[] { "一", "二", "三", "四" }[value - 1] : "";
        public void Reset(IReadOnlyList<InyeonBiographyPage> value)
        {
            pages = value ?? new InyeonBiographyPage[0];
            sources = pages.Select(p => p.TextId).Distinct().ToArray();
            sourceIndex = 0;
            FirstChapter();
        }
        public void SelectStage(int value) { if (HasStage(value)) stage = value; }
        public void MoveSource(int delta)
        {
            if (sources.Length == 0) return;
            sourceIndex = (int)(((long)sourceIndex + delta) % sources.Length);
            if (sourceIndex < 0) sourceIndex += sources.Length;
            FirstChapter();
        }
        private void FirstChapter()
            => stage = sources.Length == 0 ? 0 : pages.Where(p => p.TextId == sources[sourceIndex]).Min(p => p.Stage);
    }
}
