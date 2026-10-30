using System;

namespace Jangseoru.Core
{
    /// <summary>
    /// Turns the game's events into store updates and enforces their ordering rules. Knows nothing about the
    /// game. Per-save behaviour (snapshots and restores) only runs once an event proves which play the active
    /// list belongs to (a reset or a completed load) and only while per-save tracking is enabled; the global
    /// list keeps growing regardless.
    /// </summary>
    public sealed class CollectionSession
    {
        private readonly StoreFile file;
        private readonly ILog log;
        private bool perSaveEnabled;
        private bool sessionActive;
        private bool disableLogged;

        public CollectionSession(StoreFile file, ILog log)
        {
            this.file = file;
            this.log = log;
        }

        /// <summary>Called once at start with whether every per-save hook and reader is available.</summary>
        public void Configure(bool perSaveAvailable)
        {
            if (perSaveAvailable) perSaveEnabled = true;
            else DisablePerSave("required hooks or accessors are unavailable");
        }

        /// <summary>Stops per-save tracking for the rest of the session; existing snapshots and the global list stay.</summary>
        public void DisablePerSave(string reason)
        {
            var wasEnabled = perSaveEnabled;
            perSaveEnabled = false;
            sessionActive = false;
            file.Store.ClearActive();
            if (wasEnabled || !disableLogged)
            {
                disableLogged = true;
                Warn("per-save tracking disabled: " + reason);
            }
        }

        /// <summary>The game reset the player (new game, or the first step of a load). Collects nothing.</summary>
        public void ResetActive()
        {
            file.Store.ClearActive();
            if (perSaveEnabled) sessionActive = true;
        }

        /// <summary>The game set the title stat to <paramref name="id"/> (the value actually in effect).</summary>
        public void ObserveTitle(int id, DateTime utcNow) => Collect(id, utcNow);

        /// <summary>The game wrote a save carrying <paramref name="savedTitle"/> under <paramref name="key"/>.</summary>
        public void ObserveSave(SnapshotKey key, int savedTitle, DateTime utcNow)
        {
            Collect(savedTitle, utcNow);
            if (perSaveEnabled && sessionActive && key != null && file.Store.RecordSnapshot(key))
                file.MarkDirty(utcNow);
        }

        /// <summary>
        /// A save was fully applied. <paramref name="keyOrNull"/> is the verified origin of that exact object,
        /// or null when it is unknown (memory restore, unlinked object).
        /// </summary>
        public void ObserveLoaded(SnapshotKey keyOrNull, int currentTitle, DateTime utcNow)
        {
            var store = file.Store;
            store.ClearActive();

            string result = null;
            if (perSaveEnabled)
            {
                sessionActive = true;
                result = keyOrNull == null ? "UnknownOrigin" : store.ActivateSnapshot(keyOrNull).ToString();
            }

            Collect(currentTitle, utcNow);

            // One line per load: what matched, which slot (never the user profile) and the final active list.
            if (result != null)
                Info("load: " + result + (keyOrNull != null ? " (slot " + keyOrNull.Stem + ")" : "")
                     + " active=[" + string.Join(",", store.ActiveCollected) + "]");
        }

        private void Collect(int id, DateTime utcNow)
        {
            var includeActive = perSaveEnabled && sessionActive;
            if (file.Store.Collect(id, utcNow, includeActive))
            {
                Info("byeolho collected: " + id);
                file.MarkDirty(utcNow);
            }
        }

        private void Info(string message)
        {
            try { log?.Info(message); } catch { }
        }

        private void Warn(string message)
        {
            try { log?.Warn(message); } catch { }
        }
    }
}
