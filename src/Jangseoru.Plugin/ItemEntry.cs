using Jangseoru.Core;
using UnityEngine;

namespace Jangseoru.Plugin
{
    /// <summary>One game item as the item tabs read it. The sprite belongs to the game: never modified or destroyed.</summary>
    internal sealed class ItemEntry
    {
        public ItemKind Kind;
        public string Id;
        public Sprite Image;
        public string NameKey, DescKey, TitleKey;
    }
}
