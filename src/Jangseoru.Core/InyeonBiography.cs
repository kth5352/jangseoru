using System;
using System.Collections.Generic;

namespace Jangseoru.Core
{
    public sealed class InyeonBiographyPage
    {
        public string TextId { get; internal set; }
        public int Stage { get; internal set; }
        public string Text { get; internal set; }
    }

    public static class InyeonBiography
    {
        public static IReadOnlyList<InyeonBiographyPage> Pages(string id, InyeonStore store, Func<string, string> lookup)
        {
            var pages = new List<InyeonBiographyPage>();
            var person = InyeonCatalog.Find(id);
            if (person == null || store == null || lookup == null) return pages.AsReadOnly();
            foreach (var textId in person.BiographyIds)
                for (var stage = 1; stage <= store.BiographyStage(id, textId); stage++)
                {
                    var key = "CharacterIntro" + stage + "/" + textId;
                    var text = lookup(key);
                    if (string.IsNullOrWhiteSpace(text) || text == key) continue;
                    pages.Add(new InyeonBiographyPage { TextId = textId, Stage = stage, Text = text });
                }
            return pages.AsReadOnly();
        }
    }
}
