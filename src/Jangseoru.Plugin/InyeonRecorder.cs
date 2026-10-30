using System;
using Jangseoru.Core;

namespace Jangseoru.Plugin
{
    /// <summary>Only observations change this file. Reset/load boundaries prevent transient biography unlocks.</summary>
    internal sealed class InyeonRecorder
    {
        private readonly InyeonFile file;
        private bool ready;
        public InyeonRecorder(InyeonFile file) { this.file = file; }
        public void Reset() { ready = false; }
        public void BeginPlay() { ready = true; }
        public void Portrait(string storyId, string address)
        {
            var id = InyeonCatalog.ResolveStory(storyId);
            var path = InyeonCatalog.ResolveAddress(address) ?? address;
            if (file != null && file.Store.RecordPicture(id, path)) file.MarkDirty(DateTime.UtcNow);
        }
        public void Relationship(int type, int value, bool active, bool prologue)
        {
            if (!ready || !active || file == null) return;
            var id = InyeonCatalog.Relationship(type);
            if (id != null && file.Store.RecordBiography(id.CharacterId, id.TextId, InyeonStore.StageForAffection(value, prologue)))
                file.MarkDirty(DateTime.UtcNow);
        }
    }
}
