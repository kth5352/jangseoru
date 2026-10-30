using System.Collections.Generic;

namespace Jangseoru.Core
{
    /// <summary>Which polled input closes the full-screen media view.</summary>
    public static class MediaViewInput
    {
        /// <summary>
        /// Esc or a right click. A left click is deliberately not polled: it closes through the backdrop's own click handler, which
        /// consumes it; polling it too could let the same click reach the card underneath and reopen the view.
        /// </summary>
        public static bool ClosesOnPolledInput(bool escape, bool leftDown, bool rightDown) => escape || rightDown;
    }

    /// <summary>
    /// Remembers ids already looked at, so repeated scans read each object's details once. Bounded: when <c>cap</c> ids are held it
    /// forgets them all and starts over (re-inspecting is harmless where recording is idempotent).
    /// </summary>
    public sealed class SeenOnce
    {
        private readonly int cap;
        private readonly HashSet<int> seen = new HashSet<int>();

        public SeenOnce(int cap) { this.cap = cap < 1 ? 1 : cap; }

        public bool FirstTime(int id)
        {
            if (seen.Contains(id)) return false;
            if (seen.Count >= cap) seen.Clear();
            seen.Add(id);
            return true;
        }
    }
}
