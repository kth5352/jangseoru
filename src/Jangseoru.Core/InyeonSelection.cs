using System;
using System.Collections.Generic;
using System.Globalization;

namespace Jangseoru.Core
{
    public sealed class InyeonSelection
    {
        private readonly InyeonStore store;
        private IReadOnlyList<string> keys = new string[0];
        private int index;
        public InyeonSelection(InyeonStore store) { this.store = store; }
        public string CharacterId { get; private set; }
        public string CurrentKey => keys.Count > 0 ? keys[index] : null;
        public IReadOnlyList<string> Keys => keys;
        public int Index => index;
        /// <summary>Chooses one collected picture; an index outside the list is ignored.</summary>
        public void SelectIndex(int i)
        {
            if (i >= 0 && i < keys.Count) index = i;
        }
        public bool CanMove => keys.Count > 1;
        public string Counter => (keys.Count > 0 ? index + 1 : 0).ToString(CultureInfo.InvariantCulture) + "/" + keys.Count.ToString(CultureInfo.InvariantCulture);
        public void Select(string id)
        {
            CharacterId = id;
            keys = store?.Pictures(id) ?? new string[0];
            index = 0;
        }
        public void Move(int delta)
        {
            if (keys.Count == 0) return;
            index = (int)(((long)index + delta) % keys.Count);
            if (index < 0) index += keys.Count;
        }
    }
}
