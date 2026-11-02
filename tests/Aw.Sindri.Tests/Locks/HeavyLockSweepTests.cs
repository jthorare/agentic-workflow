using System.Diagnostics;
using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Locks;
using Aw.Sindri.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Locks;

/// <summary>The session-end sweep of the heavy wrapper's 'heavy-session' folder (sandbox spec §3.3 rule 6, §15 item 8): dead slots go, live and fresh ones and heavy-job.lock stay.</summary>
[Lane(TestLane.Unit)]
public sealed class HeavyLockSweepTests : IDisposable
{
    private readonly string _locks = Directory.CreateTempSubdirectory("aw-heavy-sweep-").FullName;
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);

    public void Dispose() => Directory.Delete(_locks, recursive: true);

    private static string Pid(int pid) => pid.ToString(CultureInfo.InvariantCulture);

    private static int DeadPid()
    {
        using var process = Process.Start(new ProcessStartInfo("true") { UseShellExecute = false }) ?? throw new InvalidOperationException("can't start true");
        process.WaitForExit();
        return process.Id;
    }

    private string Slot(string key, string? owner)
    {
        var dir = Path.Combine(_locks, "heavy-session", key + ".lock");
        _ = Directory.CreateDirectory(dir);
        if (owner is not null)
        {
            File.WriteAllText(Path.Combine(dir, "owner"), owner);
        }

        return dir;
    }

    private static string OwnerLine(int pid) => string.Create(CultureInfo.InvariantCulture, $"{pid} {RealTime.NowDateTime:yyyy-MM-ddTHH:mm:ssZ} session=x dotnet build\n");

    private int Sweep() => new HeavyLockSweep(_clock).Sweep(_locks);

    [Fact]
    public void A_slot_whose_owner_and_key_are_dead_is_removed()
    {
        var dead = DeadPid();
        var slot = Slot(Pid(dead), OwnerLine(dead));

        Expect.Equal(1, Sweep());
        Expect.True(!Directory.Exists(slot));
    }

    [Fact]
    public void A_slot_whose_owner_is_alive_is_kept()
    {
        var slot = Slot(Pid(DeadPid()), OwnerLine(Environment.ProcessId));

        Expect.Equal(0, Sweep());
        Expect.True(Directory.Exists(slot));
    }

    [Fact]
    public void A_slot_whose_key_is_alive_is_kept_even_with_a_dead_owner()
    {
        var slot = Slot(Pid(Environment.ProcessId), OwnerLine(DeadPid()));

        Expect.Equal(0, Sweep());
        Expect.True(Directory.Exists(slot));
    }

    [Fact]
    public void The_heavy_job_slots_are_never_touched()
    {
        var dead = DeadPid();
        var job = Path.Combine(_locks, "heavy-job.lock");
        var second = job + ".2";
        foreach (var dir in new[] { job, second })
        {
            _ = Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "owner"), OwnerLine(dead));
        }

        Expect.Equal(0, Sweep());
        Expect.True(Directory.Exists(job) && Directory.Exists(second));
    }

    [Fact]
    public void A_fresh_slot_without_an_owner_is_kept_and_an_old_one_is_removed()
    {
        var slot = Slot(Pid(DeadPid()), owner: null);
        Directory.SetLastWriteTimeUtc(slot, _clock.GetUtcNow().UtcDateTime);

        Expect.Equal(0, Sweep());
        Expect.True(Directory.Exists(slot));

        _clock.Advance(HeavyLockSweep.NoRecordGrace + TimeSpan.FromSeconds(1));

        Expect.Equal(1, Sweep());
        Expect.True(!Directory.Exists(slot));
    }

    [Fact]
    public void A_missing_heavy_session_folder_is_nothing_to_sweep() => Expect.Equal(0, Sweep());
}
