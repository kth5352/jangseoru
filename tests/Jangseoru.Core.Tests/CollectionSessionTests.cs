using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Jangseoru.Core.Tests
{
    public class CollectionSessionTests
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 20, 4, 0, 0, DateTimeKind.Utc);

        private static SnapshotKey K(long tick, string stem = "001", string profile = "p") => new SnapshotKey(profile, stem, tick);

        private sealed class Rig : IDisposable
        {
            private readonly TempDir dir = new TempDir();
            public readonly string Path;
            public readonly ListLog Log = new ListLog();
            public readonly StoreFile File;
            public readonly CollectionSession Session;

            public Rig(bool perSave = true)
            {
                Path = dir.File("byeolho.txt");
                File = new StoreFile(Path, Log, TimeSpan.FromSeconds(2));
                File.Load(Now);
                Session = new CollectionSession(File, Log);
                Session.Configure(perSave);
            }

            public ByeolhoStore Store => File.Store;
            public int[] Active => Store.ActiveCollected.ToArray();
            public int[] Global => Store.Collected.ToArray();

            /// <summary>Forces pending changes to disk and returns the file text.</summary>
            public string Disk()
            {
                File.FlushNow();
                return System.IO.File.Exists(Path) ? System.IO.File.ReadAllText(Path) : "";
            }

            public void Dispose() => dir.Dispose();
        }

        [Fact]
        public void Failed_save_attempt_then_loading_the_old_token_restores_the_old_list()
        {
            using (var r = new Rig())
            {
                r.Session.ResetActive();
                r.Session.ObserveTitle(1, Now);
                r.Session.ObserveSave(K(1), 1, Now);
                r.Session.ObserveTitle(2, Now);
                r.Session.ObserveSave(K(2), 2, Now); // the game's write failed; the attempt was still observed

                r.Session.ResetActive();
                r.Session.ObserveLoaded(K(1), 1, Now);

                Assert.Equal(new[] { 1 }, r.Active);
                Assert.Equal(new[] { 1, 2 }, r.Global);
            }
        }

        [Fact]
        public void Each_token_restores_its_own_list_and_one_tick_off_only_keeps_the_current_title()
        {
            using (var r = new Rig())
            {
                r.Session.ResetActive();
                r.Session.ObserveTitle(1, Now);
                r.Session.ObserveSave(K(10), 1, Now);
                r.Session.ObserveTitle(2, Now);
                r.Session.ObserveSave(K(20), 2, Now);

                r.Session.ObserveLoaded(K(10), 1, Now);
                Assert.Equal(new[] { 1 }, r.Active);

                r.Session.ObserveLoaded(K(20), 2, Now);
                Assert.Equal(new[] { 1, 2 }, r.Active);

                r.Session.ObserveLoaded(K(21), 2, Now);
                Assert.Equal(new[] { 2 }, r.Active);
                Assert.Contains(r.Log.Infos.Concat(r.Log.Warns), l => l.Contains("Mismatch"));
            }
        }

        [Fact]
        public void New_game_does_not_inherit_the_previous_play()
        {
            using (var r = new Rig())
            {
                r.Session.ResetActive();
                r.Session.ObserveTitle(1, Now);
                r.Session.ObserveTitle(2, Now);
                r.Session.ObserveSave(K(10), 2, Now);

                r.Session.ResetActive();                 // new game: ResetData
                r.Session.ObserveSave(K(30), 0, Now);    // NewGameData saves the fresh state (title 0)

                r.Session.ObserveLoaded(K(30), 0, Now);
                Assert.Equal(new[] { 0 }, r.Active);
                r.Session.ObserveLoaded(K(10), 2, Now);
                Assert.Equal(new[] { 1, 2 }, r.Active);
                Assert.Equal(new[] { 0, 1, 2 }, r.Global);
            }
        }

        [Fact]
        public void Reset_inside_a_load_does_not_collect_the_temporary_default()
        {
            using (var r = new Rig())
            {
                r.Session.ResetActive();
                r.Session.ObserveTitle(5, Now);
                r.Session.ObserveSave(K(10), 5, Now);

                r.Session.ResetActive();                // PlayerStatManagerData.Load -> ResetData (title is temporarily 0)
                r.Session.ObserveLoaded(K(10), 5, Now); // application finished

                Assert.Equal(new[] { 5 }, r.Active);
                Assert.DoesNotContain(0, r.Global);
            }
        }

        [Fact]
        public void Missing_origin_clears_the_previous_list_and_keeps_only_the_current_title()
        {
            using (var r = new Rig())
            {
                r.Session.ResetActive();
                r.Session.ObserveTitle(1, Now);
                r.Session.ObserveTitle(2, Now);

                r.Session.ObserveLoaded(null, 4, Now); // memory restore or an unlinked object

                Assert.Equal(new[] { 4 }, r.Active);
                Assert.Equal(new[] { 1, 2, 4 }, r.Global);
                Assert.Contains(r.Log.Infos.Concat(r.Log.Warns), l => l.Contains("UnknownOrigin"));
            }
        }

        [Fact]
        public void Unknown_slot_starts_from_the_current_title_only()
        {
            using (var r = new Rig())
            {
                r.Session.ObserveLoaded(K(99, "007"), 3, Now);

                Assert.Equal(new[] { 3 }, r.Active);
                Assert.Contains(r.Log.Infos.Concat(r.Log.Warns), l => l.Contains("None"));
            }
        }

        [Fact]
        public void Ambiguous_key_is_never_restored()
        {
            using (var r = new Rig())
            {
                r.Session.ResetActive();
                r.Session.ObserveTitle(1, Now);
                r.Session.ObserveSave(K(10), 1, Now);
                r.Session.ObserveTitle(2, Now);
                r.Session.ObserveSave(K(10), 2, Now); // same key, different list

                r.Session.ObserveLoaded(K(10), 1, Now);

                Assert.Equal(new[] { 1 }, r.Active);
                Assert.Contains(r.Log.Infos.Concat(r.Log.Warns), l => l.Contains("Ambiguous"));
            }
        }

        [Fact]
        public void Profiles_and_auto_slots_do_not_mix_with_manual_slots()
        {
            using (var r = new Rig())
            {
                r.Session.ResetActive();
                r.Session.ObserveTitle(1, Now);
                r.Session.ObserveSave(K(10, "001"), 1, Now);
                r.Session.ObserveTitle(2, Now);
                r.Session.ObserveSave(K(20, "auto"), 2, Now);
                r.Session.ResetActive();
                r.Session.ObserveTitle(9, Now);
                r.Session.ObserveSave(K(30, "001", "q"), 9, Now);

                r.Session.ObserveLoaded(K(10, "001"), 1, Now);
                Assert.Equal(new[] { 1 }, r.Active);
                r.Session.ObserveLoaded(K(20, "auto"), 2, Now);
                Assert.Equal(new[] { 1, 2 }, r.Active);
                r.Session.ObserveLoaded(K(30, "001", "q"), 9, Now);
                Assert.Equal(new[] { 9 }, r.Active);
                r.Session.ObserveLoaded(K(10, "001", "q"), 1, Now);
                Assert.Equal(new[] { 1 }, r.Active); // same stem+token under another profile: not found
            }
        }

        [Fact]
        public void Per_save_disabled_never_records_or_restores_but_keeps_collecting_globally()
        {
            using (var r = new Rig(perSave: false))
            {
                r.Session.ResetActive();
                r.Session.ObserveTitle(3, Now);
                r.Session.ObserveSave(K(10), 3, Now);
                r.Session.ObserveLoaded(K(10), 4, Now);

                Assert.Empty(r.Active);
                Assert.Equal(new[] { 3, 4 }, r.Global);
                Assert.DoesNotContain("snapshot ", r.Disk());
            }
        }

        [Fact]
        public void Disabling_per_save_midway_keeps_old_snapshots_and_stops_new_ones()
        {
            using (var r = new Rig())
            {
                r.Session.ResetActive();
                r.Session.ObserveTitle(1, Now);
                r.Session.ObserveSave(K(10), 1, Now);
                var before = r.Store.Serialize();

                r.Session.DisablePerSave("test");
                Assert.Empty(r.Active);

                r.Session.ObserveLoaded(K(10), 1, Now);   // an exact snapshot exists but must not be used
                r.Session.ObserveSave(K(20), 2, Now);     // must not add a snapshot
                Assert.Empty(r.Active);
                Assert.Equal(new[] { 1, 2 }, r.Global);

                var after = r.Store.Serialize();
                Assert.Equal(before.Split('\n').Count(l => l.StartsWith("snapshot ")), after.Split('\n').Count(l => l.StartsWith("snapshot ")));
                Assert.Contains(r.Log.Warns, w => w.Contains("test"));
            }
        }

        [Fact]
        public void Before_any_reset_or_load_only_the_global_list_grows()
        {
            using (var r = new Rig())
            {
                r.Session.ObserveTitle(7, Now);
                r.Session.ObserveSave(K(10), 7, Now);

                Assert.Equal(new[] { 7 }, r.Global);
                Assert.Empty(r.Active);
                Assert.DoesNotContain("snapshot ", r.Disk());
            }
        }

        [Fact]
        public void A_title_already_in_the_global_list_still_gets_snapshotted_and_saved_to_disk()
        {
            using (var r = new Rig())
            {
                r.Session.ResetActive();
                r.Session.ObserveTitle(5, Now);
                r.Session.ObserveSave(K(10), 5, Now);
                r.Disk(); // flush

                r.Session.ResetActive();
                r.Session.ObserveTitle(5, Now);         // already global: Collect returns false, active changes
                Assert.Equal(new[] { 5 }, r.Active);
                r.Session.ObserveSave(K(20), 5, Now);

                var disk = r.Disk();
                Assert.Equal(2, disk.Split('\n').Count(l => l.StartsWith("snapshot ")));
            }
        }

        [Fact]
        public void Out_of_range_values_are_ignored()
        {
            using (var r = new Rig())
            {
                r.Session.ResetActive();
                r.Session.ObserveTitle(26, Now);
                r.Session.ObserveTitle(-1, Now);
                r.Session.ObserveSave(K(10), 99, Now);
                r.Session.ObserveLoaded(K(10), -1, Now);

                Assert.Empty(r.Global);
                Assert.Empty(r.Active);
            }
        }

        [Fact]
        public void A_new_global_title_is_written_to_disk_and_logged_once()
        {
            using (var r = new Rig())
            {
                r.Session.ResetActive();
                r.Session.ObserveTitle(3, Now);
                r.Session.ObserveTitle(3, Now);

                Assert.Contains("global 3 ", r.Disk());
                Assert.Equal(1, r.Log.Infos.Count(l => l.Contains("byeolho collected: 3")));
            }
        }

        [Fact]
        public void Exact_match_is_logged_on_load()
        {
            using (var r = new Rig())
            {
                r.Session.ResetActive();
                r.Session.ObserveTitle(1, Now);
                r.Session.ObserveSave(K(10), 1, Now);

                r.Session.ObserveLoaded(K(10), 1, Now);

                Assert.Contains(r.Log.Infos, l => l.Contains("Exact"));
            }
        }

        [Fact]
        public void Load_log_names_the_slot_and_the_titles_that_ended_up_active_but_never_the_profile()
        {
            using (var r = new Rig())
            {
                r.Session.ResetActive();
                r.Session.ObserveTitle(13, Now);
                r.Session.ObserveTitle(25, Now);
                r.Session.ObserveSave(new SnapshotKey("76561198000000000", "007", 10), 25, Now);

                r.Session.ObserveLoaded(new SnapshotKey("76561198000000000", "007", 10), 25, Now);
                r.Session.ObserveLoaded(new SnapshotKey("76561198000000000", "009", 99), 0, Now);
                r.Session.ObserveLoaded(null, 4, Now);

                var loads = r.Log.Infos.Where(l => l.StartsWith("load:")).ToArray();
                Assert.Equal("load: Exact (slot 007) active=[13,25]", loads[0]);
                Assert.Equal("load: None (slot 009) active=[0]", loads[1]);
                Assert.Equal("load: UnknownOrigin active=[4]", loads[2]);
                Assert.DoesNotContain(r.Log.Infos.Concat(r.Log.Warns), l => l.Contains("76561198000000000"));
            }
        }

        [Fact]
        public void A_blocked_store_still_collects_in_memory_without_throwing()
        {
            using (var dir = new TempDir())
            {
                var path = dir.File("byeolho.txt");
                File.WriteAllText(path, "jangseoru.byeolho v2\nfuture\n");
                var log = new ListLog();
                var file = new StoreFile(path, log, TimeSpan.FromSeconds(2));
                file.Load(Now);
                var session = new CollectionSession(file, log);
                session.Configure(true);

                session.ResetActive();
                session.ObserveTitle(1, Now);
                session.ObserveSave(K(10), 1, Now);
                file.FlushNow();

                Assert.True(file.WritesBlocked);
                Assert.Equal("jangseoru.byeolho v2\nfuture\n", File.ReadAllText(path));
            }
        }
    }
}
