using System.Diagnostics;
using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Locks;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Locks;

/// <summary>The heavy-job lock as directories (context spec §4): slot 1 is the path, slot k is '&lt;path&gt;.k', the same protocol as config/lib/locks.sh and the heavy wrapper.</summary>
public sealed class DirectoryHeavySlotsTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("aw-heavy-slots-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static string Pid => Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string Now => RealTime.NowDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);

    private string Lock => Path.Combine(_root, "locks", "heavy-job.lock");

    private DirectoryHeavySlots Slots(int slots = 2, bool held = false) => new(new HeavyLockSettings(Lock, slots, held), TimeProvider.System);

    private static async Task<IAsyncDisposable> Taken(DirectoryHeavySlots slots, string holder = "test")
    {
        var taken = (await slots.Take(holder, TimeSpan.Zero, waiting: null, TestContext.Current.CancellationToken)).Value;
        return taken.Lease ?? throw new InvalidOperationException("busy: " + taken.Busy);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_first_holder_takes_slot_1_the_second_slot_2_and_the_third_finds_both_busy_naming_who_holds_them()
    {
        var slots = Slots();

        await using var first = await Taken(slots, "first job");
        await using var second = await Taken(slots, "second job");
        var third = (await slots.Take("third", TimeSpan.Zero, waiting: null, TestContext.Current.CancellationToken)).Value;

        Assert.True(Directory.Exists(Lock));
        Assert.True(Directory.Exists(Lock + ".2"));
        Assert.Null(third.Lease);
        Assert.Contains("slot 1/2 held by " + Pid, third.Busy, StringComparison.Ordinal);
        Assert.Contains("first job", third.Busy, StringComparison.Ordinal);
        Assert.Contains("slot 2/2", third.Busy, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_owner_file_holds_pid_time_and_who()
    {
        var slots = Slots();

        await using var lease = await Taken(slots, "sindri context-map-build");

        var owner = (await File.ReadAllTextAsync(Path.Combine(Lock, "owner"), TestContext.Current.CancellationToken)).Trim().Split(' ', 3);
        Assert.Equal((Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture), "sindri context-map-build"), (owner[0], owner[2]));
        Assert.Matches(@"^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\dZ$", owner[1]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Releasing_frees_the_slot_for_the_next_holder_and_twice_is_harmless()
    {
        var slots = Slots(slots: 1);
        var first = await Taken(slots);

        await first.DisposeAsync();
        await first.DisposeAsync();
        await using var again = await Taken(slots);

        Assert.True(Directory.Exists(Lock));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_parent_that_holds_a_slot_makes_taking_one_a_no_op()
    {
        var slots = Slots(held: true);

        await using var lease = await Taken(slots);

        Assert.False(Directory.Exists(Lock));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_slot_whose_owner_process_is_gone_is_reclaimed_like_the_heavy_wrapper_does()
    {
        Directory.CreateDirectory(Lock);
        await File.WriteAllTextAsync(Path.Combine(Lock, "owner"), "2147483646 2026-10-01T00:00:00Z dead job\n", TestContext.Current.CancellationToken);

        await using var lease = await Taken(Slots(slots: 1));

        Assert.Contains(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture), await File.ReadAllTextAsync(Path.Combine(Lock, "owner"), TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_slot_with_no_owner_record_stays_held_before_the_six_hour_last_resort_because_locks_sh_and_ui_evidence_write_none()
    {
        Directory.CreateDirectory(Lock);
        Directory.SetLastWriteTimeUtc(Lock, RealTime.NowDateTime.AddHours(-6).AddMinutes(1));   // just under the boundary

        var taken = (await Slots(slots: 1).Take("x", TimeSpan.Zero, waiting: null, TestContext.Current.CancellationToken)).Value;

        Assert.Null(taken.Lease);
        Assert.Contains("an unknown holder", taken.Busy, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_slot_with_no_record_is_taken_only_after_the_six_hour_last_resort_the_TS_sindri_uses()
    {
        Directory.CreateDirectory(Lock);
        Directory.SetLastWriteTimeUtc(Lock, RealTime.NowDateTime.AddHours(-7));

        await using var lease = await Taken(Slots(slots: 1));

        Assert.Contains(Pid, await File.ReadAllTextAsync(Path.Combine(Lock, "owner"), TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_TS_sindri_holder_record_with_a_live_pid_keeps_the_slot_held()
    {
        Directory.CreateDirectory(Lock);
        await File.WriteAllTextAsync(Lock + ".holder.json", $"{{\"kind\":\"index-build:x\",\"pid\":{Pid},\"host\":\"{Environment.MachineName}\",\"startedAt\":\"{Now}\"}}", TestContext.Current.CancellationToken);

        var taken = (await Slots(slots: 1).Take("x", TimeSpan.Zero, waiting: null, TestContext.Current.CancellationToken)).Value;

        Assert.Null(taken.Lease);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_TS_sindri_holder_record_with_a_dead_pid_on_this_host_is_reclaimed()
    {
        Directory.CreateDirectory(Lock);
        await File.WriteAllTextAsync(Lock + ".holder.json", $"{{\"kind\":\"index-build:x\",\"pid\":2147483646,\"host\":\"{Environment.MachineName}\",\"startedAt\":\"2026-10-01T00:00:00Z\"}}", TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(Lock + ".holder.json", RealTime.NowDateTime.AddMinutes(1));

        await using var lease = await Taken(Slots(slots: 1));

        Assert.Contains(Pid, await File.ReadAllTextAsync(Path.Combine(Lock, "owner"), TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_dead_pid_recorded_on_another_host_is_not_ours_to_judge()
    {
        Directory.CreateDirectory(Lock);
        await File.WriteAllTextAsync(Lock + ".holder.json", "{\"kind\":\"k\",\"pid\":2147483646,\"host\":\"some-other-box\",\"startedAt\":\"2026-10-01T00:00:00Z\"}", TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(Lock + ".holder.json", RealTime.NowDateTime.AddMinutes(1));

        var taken = (await Slots(slots: 1).Take("x", TimeSpan.Zero, waiting: null, TestContext.Current.CancellationToken)).Value;

        Assert.Null(taken.Lease);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_owner_write_fails_the_take_and_releases_the_slot()
    {
        // A read-only slot directory makes the owner write fail, as a full disk would.
        var slots = new DirectoryHeavySlots(new HeavyLockSettings(Lock, 1, Held: false), TimeProvider.System)
        {
            AfterMkdir = directory =>
            {
                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserExecute);
                }
            },
        };

        var taken = await slots.Take("x", TimeSpan.Zero, waiting: null, TestContext.Current.CancellationToken);

        Assert.True(taken.IsError);
        Assert.False(Directory.Exists(Lock));
    }

    private DirectoryHeavySlots OneSlot() => new(new HeavyLockSettings(Lock, 1, Held: false), TimeProvider.System);

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_owner_write_after_the_file_was_created_removes_the_file_and_the_slot_and_says_so()
    {
        var slots = new DirectoryHeavySlots(new HeavyLockSettings(Lock, 1, Held: false), TimeProvider.System)
        {
            WriteFile = (path, _) =>
            {
                File.WriteAllText(path, string.Empty);   // the file exists, then the write fails: a full disk
                throw new IOException("No space left on device");
            },
        };

        var taken = await slots.Take("x", TimeSpan.Zero, waiting: null, TestContext.Current.CancellationToken);

        Assert.True(taken.IsError);
        Assert.Contains("so the slot was released", taken.Error.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Lock));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_owner_write_that_leaves_the_slot_behind_names_the_folder_and_does_not_say_released()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var slots = new DirectoryHeavySlots(new HeavyLockSettings(Lock, 1, Held: false), TimeProvider.System)
        {
            WriteFile = (path, _) =>
            {
                File.WriteAllText(path, string.Empty);
                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(Path.GetDirectoryName(path)!, UnixFileMode.UserRead | UnixFileMode.UserExecute);   // why: the file sits in a folder; nothing in it can now be deleted
                }

                throw new IOException("No space left on device");
            },
        };

        try
        {
            var taken = await slots.Take("x", TimeSpan.Zero, waiting: null, TestContext.Current.CancellationToken);

            Assert.True(taken.IsError);
            Assert.DoesNotContain("so the slot was released", taken.Error.Message, StringComparison.Ordinal);
            Assert.Contains(Lock, taken.Error.Message, StringComparison.Ordinal);
            Assert.Contains("removed by hand", taken.Error.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(Lock, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
        }
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_slot_with_an_empty_owner_file_is_a_holder_mid_write_and_is_left_alone_even_past_the_six_hour_takeover()
    {
        Directory.CreateDirectory(Lock);
        await File.WriteAllTextAsync(Path.Combine(Lock, "owner"), string.Empty, TestContext.Current.CancellationToken);
        var later = new FakeTimeProvider(RealTime.NowOffset + DirectoryHeavySlots.NoRecordGrace + TimeSpan.FromHours(1));   // the slot is older than the takeover age

        var taken = (await new DirectoryHeavySlots(new HeavyLockSettings(Lock, 1, Held: false), later).Take("x", TimeSpan.Zero, waiting: null, TestContext.Current.CancellationToken)).Value;

        Assert.Null(taken.Lease);
        Assert.True(File.Exists(Path.Combine(Lock, "owner")));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_slot_with_no_owner_file_at_all_is_taken_over_past_the_six_hours()
    {
        Directory.CreateDirectory(Lock);
        var later = new FakeTimeProvider(RealTime.NowOffset + DirectoryHeavySlots.NoRecordGrace + TimeSpan.FromHours(1));

        var taken = (await new DirectoryHeavySlots(new HeavyLockSettings(Lock, 1, Held: false), later).Take("x", TimeSpan.Zero, waiting: null, TestContext.Current.CancellationToken)).Value;

        Assert.NotNull(taken.Lease);
        await taken.Lease.DisposeAsync();
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_live_holder_that_took_its_slot_in_the_second_it_started_is_not_a_reused_pid()
    {
        // The record is written to the second, so it can name a time up to a second before the process's own start time: the tolerance keeps that holder's slot.
        var started = Process.GetCurrentProcess().StartTime.ToUniversalTime().AddSeconds(-1);
        Directory.CreateDirectory(Lock);
        var owner = Path.Combine(Lock, "owner");
        var line = $"{Pid} {started.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture)} live job\n";
        await File.WriteAllTextAsync(owner, line, TestContext.Current.CancellationToken);

        var taken = (await OneSlot().Take("x", TimeSpan.Zero, waiting: null, TestContext.Current.CancellationToken)).Value;

        Assert.Null(taken.Lease);
        Assert.Equal(line, await File.ReadAllTextAsync(owner, TestContext.Current.CancellationToken));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_TS_holder_record_older_than_the_slot_is_a_leftover_and_the_slot_stays_held()
    {
        Directory.CreateDirectory(Lock);
        await File.WriteAllTextAsync(Lock + ".holder.json", $"{{\"kind\":\"k\",\"pid\":2147483646,\"host\":\"{Environment.MachineName}\",\"startedAt\":\"2026-10-01T00:00:00Z\"}}", TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(Lock + ".holder.json", RealTime.NowDateTime.AddHours(-1));
        Directory.SetLastWriteTimeUtc(Lock, RealTime.NowDateTime);   // a locks.sh holder took the slot after the record was written

        var taken = (await OneSlot().Take("x", TimeSpan.Zero, waiting: null, TestContext.Current.CancellationToken)).Value;

        Assert.Null(taken.Lease);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_TS_holder_on_a_host_whose_name_has_a_domain_is_judged_on_the_short_name()
    {
        Directory.CreateDirectory(Lock);
        await File.WriteAllTextAsync(Lock + ".holder.json", $"{{\"kind\":\"k\",\"pid\":2147483646,\"host\":\"{Environment.MachineName}.local\",\"startedAt\":\"2026-10-01T00:00:00Z\"}}", TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(Lock + ".holder.json", RealTime.NowDateTime.AddMinutes(1));

        await using var lease = await Taken(OneSlot());

        Assert.Contains(Pid, await File.ReadAllTextAsync(Path.Combine(Lock, "owner"), TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_live_pid_that_started_after_the_record_was_written_is_a_reused_pid_and_the_slot_is_reclaimed()
    {
        Directory.CreateDirectory(Lock);
        await File.WriteAllTextAsync(Path.Combine(Lock, "owner"), $"{Pid} 2020-01-01T00:00:00Z a job from before this process existed\n", TestContext.Current.CancellationToken);

        await using var lease = await Taken(OneSlot());

        Assert.DoesNotContain("2020-01-01T00:00:00Z", await File.ReadAllTextAsync(Path.Combine(Lock, "owner"), TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_slot_held_over_six_hours_is_taken_even_when_its_record_names_a_live_pid_like_the_TS_sindri_does()
    {
        Directory.CreateDirectory(Lock);
        await File.WriteAllTextAsync(Path.Combine(Lock, "owner"), $"{Pid} {Now} long job\n", TestContext.Current.CancellationToken);
        Directory.SetLastWriteTimeUtc(Lock, RealTime.NowDateTime.AddHours(-7));

        await using var lease = await Taken(OneSlot());

        Assert.DoesNotContain("long job", await File.ReadAllTextAsync(Path.Combine(Lock, "owner"), TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Two_waiters_that_both_judged_a_record_less_old_slot_stale_leave_the_slot_to_the_one_that_retook_it()
    {
        Directory.CreateDirectory(Lock);
        Directory.SetLastWriteTimeUtc(Lock, RealTime.NowDateTime.AddHours(-7));
        var slots = new DirectoryHeavySlots(new HeavyLockSettings(Lock, 1, Held: false), TimeProvider.System)
        {
            BeforeTakeover = directory =>
            {
                Directory.Delete(directory, recursive: true);
                Directory.CreateDirectory(directory);   // A took it: fresh, and (a moment later) with its owner
            },
        };

        var b = (await slots.Take("b's job", TimeSpan.Zero, waiting: null, TestContext.Current.CancellationToken)).Value;

        Assert.Null(b.Lease);
        Assert.True(Directory.Exists(Lock));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Two_waiters_that_both_judged_a_dead_slot_stale_leave_the_slot_to_the_one_that_retook_it()
    {
        // B read the dead owner; before B takes the slot over, A reclaimed it and holds it now. B must leave A's slot alone.
        Directory.CreateDirectory(Lock);
        await File.WriteAllTextAsync(Path.Combine(Lock, "owner"), "2147483646 2026-10-01T00:00:00Z dead job\n", TestContext.Current.CancellationToken);
        var aOwner = $"{Pid} {Now} a's job\n";
        var slots = new DirectoryHeavySlots(new HeavyLockSettings(Lock, 1, Held: false), TimeProvider.System)
        {
            BeforeTakeover = directory =>
            {
                Directory.Delete(directory, recursive: true);
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "owner"), aOwner);
            },
        };

        var b = (await slots.Take("b's job", TimeSpan.Zero, waiting: null, TestContext.Current.CancellationToken)).Value;

        Assert.Null(b.Lease);
        Assert.Equal(aOwner, await File.ReadAllTextAsync(Path.Combine(Lock, "owner"), TestContext.Current.CancellationToken));
        Assert.Empty(Directory.GetFiles(Lock, "owner.reclaim.*"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Releasing_a_slot_whose_owner_line_names_another_process_leaves_that_holders_slot_alone()
    {
        var slots = Slots(slots: 1);
        var ours = await Taken(slots);
        await File.WriteAllTextAsync(Path.Combine(Lock, "owner"), "2147483646 2026-10-01T00:00:00Z other\n", TestContext.Current.CancellationToken);   // someone removed ours and another job took the slot

        await ours.DisposeAsync();

        Assert.True(Directory.Exists(Lock));
        Assert.StartsWith("2147483646 ", await File.ReadAllTextAsync(Path.Combine(Lock, "owner"), TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Waiting_tells_the_progress_once_and_gives_up_at_the_timeout()
    {
        Directory.CreateDirectory(Lock);
        await File.WriteAllTextAsync(Path.Combine(Lock, "owner"), $"{Pid} {Now} other\n", TestContext.Current.CancellationToken);
        var told = new SyncProgress();
        var clock = new FakeTimeProvider();
        var slots = new DirectoryHeavySlots(new HeavyLockSettings(Lock, 1, Held: false), clock);

        var waiting = slots.Take("x", TimeSpan.FromSeconds(3), told, TestContext.Current.CancellationToken).AsTask();
        while (!waiting.IsCompleted)
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }

        var taken = (await waiting).Value;
        Assert.Null(taken.Lease);
        Assert.Contains("waiting for heavy-job lock (slot 1/1 held by " + Pid, Assert.Single(told.Lines), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_settings_read_the_variables_the_shell_tools_read()
    {
        var plain = HeavyLockSettings.Read(_ => null, "/state");
        var set = HeavyLockSettings.Read(name => name switch { "AW_HEAVY_JOB_LOCK" => "/x/lock", "AW_HEAVY_SLOTS" => "3", "AW_HEAVY_LOCK_HELD" => "1", _ => null }, "/state");

        Assert.Equal(("/state/locks/heavy-job.lock", 1, false), (plain.Lock, plain.Slots, plain.Held));
        Assert.Equal(("/x/lock", 3, true), (set.Lock, set.Slots, set.Held));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(null, 1)]
    [InlineData("", 1)]
    [InlineData("2", 2)]
    [InlineData("1", 1)]
    [InlineData("3", 3)]
    [InlineData("03", 3)]
    [InlineData("0", 1)]
    [InlineData("zero", 1)]
    [InlineData("-2", 1)]
    [InlineData("2.5", 1)]
    public void The_slot_count_is_AW_HEAVY_SLOTS_when_set_and_1_when_unset_empty_or_malformed_as_config_lib_locks_sh_reads_it(string? value, int expected)
    {
        var settings = HeavyLockSettings.Read(name => name == "AW_HEAVY_SLOTS" ? value : null, "/state");

        Assert.Equal(expected, settings.Slots);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_lock_read_from_AW_HEAVY_SLOTS_3_takes_three_holders_and_names_slot_3_of_3_to_the_fourth()
    {
        var settings = HeavyLockSettings.Read(name => name switch { "AW_HEAVY_SLOTS" => "3", "AW_HEAVY_JOB_LOCK" => Lock, _ => null }, _root);
        var slots = new DirectoryHeavySlots(settings, TimeProvider.System);

        await using var one = await Taken(slots);
        await using var two = await Taken(slots);
        await using var three = await Taken(slots);
        var fourth = (await slots.Take("fourth", TimeSpan.Zero, waiting: null, TestContext.Current.CancellationToken)).Value;

        Assert.Null(fourth.Lease);
        Assert.Contains("slot 3/3", fourth.Busy, StringComparison.Ordinal);
        Assert.True(Directory.Exists(Lock + ".3"));
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task It_shares_the_lock_directory_with_config_lib_locks_sh_in_both_directions()
    {
        var ct = TestContext.Current.CancellationToken;
        var script = Path.Combine(FindRepoRoot(), "config", "lib", "locks.sh");
        var slots = Slots();
        await using var ours = await Taken(slots);
        var bash = Run($". '{script}'; slot=\"$(acquire_any_lock '{Lock}' 2 0)\" && echo \"$slot\"");

        var bashSlot = (await bash.StandardOutput.ReadToEndAsync(ct)).Trim();
        await bash.WaitForExitAsync(ct);
        var busy = (await slots.Take("third", TimeSpan.Zero, waiting: null, ct)).Value;

        Assert.Equal(Lock + ".2", bashSlot);   // slot 1 was ours, so the shell took slot 2
        Assert.Null(busy.Lease);
        await ours.DisposeAsync();
        var again = await Taken(slots);   // slot 1 is free again, the shell's slot 2 still stands
        await again.DisposeAsync();
        Assert.True(Directory.Exists(Lock + ".2"));
    }

    private static Process Run(string script)
    {
        var info = new ProcessStartInfo("bash") { RedirectStandardOutput = true, UseShellExecute = false };
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add(script);
        return Process.Start(info) ?? throw new InvalidOperationException("bash didn't start");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AgenticWorkflow.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("the repo root isn't above " + AppContext.BaseDirectory);
    }
}
