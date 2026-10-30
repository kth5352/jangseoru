using System;
using System.Text;

namespace Jangseoru.Core
{
    /// <summary>
    /// Shared disk-persistence body for the mod's text stores (debounce, five-minute retry, quarantine of
    /// corrupt files, future-version read-only, size warning). Never throws out of any public member and never
    /// overwrites a file it could not read or understand. <see cref="StoreFile"/> and <see cref="GalleryFile"/>
    /// are thin wrappers that supply the parse/serialize delegates.
    /// </summary>
    internal sealed class StoreFileCore<TStore> where TStore : class
    {
        internal delegate TStore ParseFunc(string text, out bool wasCorrupt);

        private const int SizeWarnBytes = 1024 * 1024;
        private static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(5);

        private readonly string path;
        private readonly ILog log;
        private readonly TimeSpan debounce;
        private readonly IFileOps ops;
        private readonly Func<TStore> create;
        private readonly ParseFunc parse;
        private readonly Func<TStore, string> serialize;
        private readonly Func<TStore, bool> isReadOnly;

        private bool dirty;
        private DateTime lastDirtyUtc;
        private DateTime nextRetryUtc;
        private bool sizeWarned;

        public StoreFileCore(string filePath, ILog log, TimeSpan debounce, IFileOps ops,
            Func<TStore> create, ParseFunc parse, Func<TStore, string> serialize, Func<TStore, bool> isReadOnly)
        {
            this.path = filePath;
            this.log = log;
            this.debounce = debounce;
            this.ops = ops;
            this.create = create;
            this.parse = parse;
            this.serialize = serialize;
            this.isReadOnly = isReadOnly;
            Store = create();
        }

        public TStore Store { get; private set; }

        public bool Degraded { get; private set; }

        public bool WritesBlocked { get; private set; }

        public void Load(DateTime utcNow)
        {
            Store = create();
            dirty = false;
            Degraded = false;
            WritesBlocked = false;

            if (string.IsNullOrWhiteSpace(path))
            {
                Block("no valid store path; collecting in memory only");
                return;
            }

            try
            {
                switch (AtomicFile.ReadAllText(path, out var text))
                {
                    case FileReadStatus.Missing:
                        return;

                    case FileReadStatus.Failed:
                        Block("store file could not be read; leaving it untouched");
                        return;

                    default:
                        var parsed = parse(text, out var corrupt);
                        if (corrupt)
                        {
                            var moved = AtomicFile.Quarantine(path, utcNow, null, ops);
                            if (moved == null)
                            {
                                Block("store file is corrupt and could not be set aside; leaving it untouched");
                                return;
                            }
                            Warn("store file was corrupt; moved aside to " + System.IO.Path.GetFileName(moved));
                            return;
                        }
                        Store = parsed;
                        if (isReadOnly(parsed)) Block("store file was written by a newer version; read-only");
                        return;
                }
            }
            catch (Exception e)
            {
                Store = create();
                Block("unexpected error loading store: " + e.GetType().Name);
            }
        }

        public void MarkDirty(DateTime utcNow)
        {
            if (WritesBlocked) return;
            dirty = true;
            lastDirtyUtc = utcNow;
        }

        public void FlushIfDue(DateTime utcNow)
        {
            if (!dirty || WritesBlocked) return;
            var due = Degraded ? utcNow >= nextRetryUtc : utcNow - lastDirtyUtc >= debounce;
            if (due) Write(utcNow);
        }

        public void FlushNow(DateTime utcNow)
        {
            if (!dirty || WritesBlocked) return;
            Write(utcNow);
        }

        private void Write(DateTime utcNow)
        {
            try
            {
                var text = serialize(Store);
                if (!sizeWarned && Encoding.UTF8.GetByteCount(text) > SizeWarnBytes)
                {
                    sizeWarned = true;
                    Warn("store size is above 1 MiB (" + text.Length + " chars); no automatic cleanup is performed");
                }
                AtomicFile.WriteAllText(path, text, ops);
                dirty = false;
                Degraded = false;
            }
            catch (Exception e)
            {
                Degraded = true;
                nextRetryUtc = utcNow + RetryInterval;
                Warn("store write failed (" + e.GetType().Name + "); will retry in 5 minutes");
            }
        }

        private void Block(string reason)
        {
            WritesBlocked = true;
            Warn(reason);
        }

        private void Warn(string message)
        {
            try { log?.Warn(message); } catch { /* logging must never break the game */ }
        }
    }
}
