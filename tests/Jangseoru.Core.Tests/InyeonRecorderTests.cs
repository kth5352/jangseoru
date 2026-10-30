using System;
using System.IO;
using Jangseoru.Plugin;
using Xunit;

namespace Jangseoru.Core.Tests
{
    [Collection("InyeonCatalog")]
    public class InyeonRecorderTests
    {
        [InyeonFact]
        public void Reset_and_load_intermediate_values_never_unlock_biographies()
        {
            var file = new InyeonFile(null, null, TimeSpan.FromSeconds(2));
            var r = new InyeonRecorder(file);
            r.Relationship(5, 100, true, false);
            Assert.Equal(0, file.Store.BiographyStage("master", "master"));
            r.BeginPlay();
            r.Relationship(5, 50, true, false);
            Assert.Equal(2, file.Store.BiographyStage("master", "master"));
            r.Reset();
            r.Relationship(5, 100, true, false);
            r.BeginPlay();
            r.Relationship(5, 20, true, false);
            Assert.Equal(2, file.Store.BiographyStage("master", "master"));
        }

        [InyeonFact]
        public void Inactive_people_and_wrong_portraits_are_not_collected()
        {
            var file = new InyeonFile(null, null, TimeSpan.FromSeconds(2));
            var r = new InyeonRecorder(file);
            r.BeginPlay();
            r.Relationship(5, 100, false, false);
            r.Portrait("girl2", "Assets/__Project/Images/Characters/Master/normal.png");
            Assert.False(file.Store.IsKnown("master"));
            Assert.False(file.Store.IsKnown("girl2"));
            r.Portrait("girl7_3", "Assets/__Project/Images/Characters/Girl7_小竹_盛裝/normal.png");
            Assert.Single(file.Store.Pictures("girl7"));
        }

        [InyeonFact]
        public void Prologue_caps_biography_but_leaving_prologue_can_unlock_remaining_stages()
        {
            var file = new InyeonFile(null, null, TimeSpan.FromSeconds(2));
            var r = new InyeonRecorder(file);
            r.BeginPlay();
            r.Relationship(5, 100, true, true);
            Assert.Equal(2, file.Store.BiographyStage("master", "master"));
            r.Relationship(5, 100, true, false);
            r.Relationship(5, 10, true, false);
            Assert.Equal(4, file.Store.BiographyStage("master", "master"));
        }
    }
}
