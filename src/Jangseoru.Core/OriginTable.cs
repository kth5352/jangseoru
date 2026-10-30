using System.Runtime.CompilerServices;

namespace Jangseoru.Core
{
    /// <summary>
    /// Remembers which save file version a game-created object was read from, keyed by object identity and
    /// held weakly: the game's objects are never modified and are never kept alive by this table.
    /// </summary>
    public sealed class OriginTable
    {
        private readonly ConditionalWeakTable<object, SnapshotKey> table = new ConditionalWeakTable<object, SnapshotKey>();

        public void Attach(object gameObject, SnapshotKey origin)
        {
            if (gameObject == null || origin == null) return;
            lock (table)
            {
                table.Remove(gameObject);
                table.Add(gameObject, origin);
            }
        }

        public bool TryGet(object gameObject, out SnapshotKey origin)
        {
            origin = null;
            if (gameObject == null) return false;
            lock (table) return table.TryGetValue(gameObject, out origin);
        }
    }
}
