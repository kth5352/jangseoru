using System;
using System.IO;
using Xunit;

namespace Jangseoru.Core.Tests
{
    [Collection("InyeonCatalog")]
    public class CombinedCollectionTests
    {
        [InyeonFact]
        public void Item_and_inyeon_collections_share_folder_without_overwriting_each_other()
        {
            using (var dir = new TempDir())
            {
                var gameSaves = Path.Combine(dir.Path, "GameSaves");
                var itemPath = StorePaths.ResolveItems(gameSaves);
                var inyeonPath = StorePaths.ResolveInyeon(gameSaves);
                Assert.Equal(Path.GetDirectoryName(itemPath), Path.GetDirectoryName(inyeonPath));
                Assert.NotEqual(itemPath, inyeonPath);
                var now = DateTime.UtcNow;
                var log = new ListLog();
                var items = new ItemFile(itemPath, log, TimeSpan.Zero);
                var inyeon = new InyeonFile(inyeonPath, log, TimeSpan.Zero);
                items.Load(now);
                inyeon.Load(now);
                items.Store.Add(ItemKind.Book, "1000");
                inyeon.Store.RecordBiography("master", "master", 3);
                inyeon.Store.RecordPicture("master", "Assets/__Project/Images/Characters/Master/normal.png");
                items.MarkDirty(now);
                inyeon.MarkDirty(now);
                items.FlushNow();
                var originalItems = File.ReadAllText(itemPath);
                inyeon.FlushNow();
                Assert.Equal(originalItems, File.ReadAllText(itemPath));

                var reloadedItems = new ItemFile(itemPath, log, TimeSpan.Zero);
                var reloadedInyeon = new InyeonFile(inyeonPath, log, TimeSpan.Zero);
                reloadedItems.Load(now);
                reloadedInyeon.Load(now);
                Assert.True(reloadedItems.Store.Has(ItemKind.Book, "1000"));
                Assert.Equal(3, reloadedInyeon.Store.BiographyStage("master", "master"));
                Assert.Single(reloadedInyeon.Store.Pictures("master"));
                Assert.False(Directory.Exists(gameSaves));
            }
        }
    }
}
