using System;
using System.Runtime.CompilerServices;
using Xunit;

namespace Jangseoru.Core.Tests
{
    public class OriginTableTests
    {
        private sealed class FakeSave { }

        private static SnapshotKey Key(long tick = 100) => new SnapshotKey("p", "001", tick);

        [Fact]
        public void Origin_is_linked_to_the_same_instance_only()
        {
            var table = new OriginTable();
            var a = new FakeSave();
            var b = new FakeSave();

            table.Attach(a, Key(1));

            Assert.True(table.TryGet(a, out var found));
            Assert.Equal(Key(1), found);
            Assert.False(table.TryGet(b, out _));
        }

        [Fact]
        public void Attaching_again_replaces_the_origin()
        {
            var table = new OriginTable();
            var a = new FakeSave();

            table.Attach(a, Key(1));
            table.Attach(a, Key(2));

            Assert.True(table.TryGet(a, out var found));
            Assert.Equal(Key(2), found);
        }

        [Fact]
        public void Null_inputs_are_ignored_without_throwing()
        {
            var table = new OriginTable();

            table.Attach(null, Key());
            table.Attach(new FakeSave(), null);

            Assert.False(table.TryGet(null, out _));
            Assert.False(table.TryGet(new FakeSave(), out _));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference AttachAndForget(OriginTable table)
        {
            var save = new FakeSave();
            table.Attach(save, Key());
            return new WeakReference(save);
        }

        [Fact]
        public void The_table_does_not_keep_game_objects_alive()
        {
            var table = new OriginTable();
            var weak = AttachAndForget(table);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.False(weak.IsAlive);
        }
    }
}
