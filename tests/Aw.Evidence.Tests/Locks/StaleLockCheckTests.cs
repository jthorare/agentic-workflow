using System.Diagnostics;
using System.Globalization;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Locks;
using Aw.Evidence.Tests.Infrastructure;
using Aw.Kernel;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Locks;

/// <summary>
/// A lock folder left by a dead holder is reclaimed by the next waiter, whichever side held it (config/lib/locks.sh writes
/// '&lt;slot&gt;/pid', the C# holder writes '&lt;slot&gt;/owner'); a live holder's folder, and one with no holder file yet, are left alone.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class StaleLockCheckTests : IDisposable
{
    private readonly TempDirectory _root = new();

    public static TheoryData<LockKind> Kinds => [LockKind.QaStack, LockKind.HeavyJob];

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task A_lock_left_by_a_dead_csharp_holder_is_reclaimed(LockKind kind)
    {
        var slot = Slot(kind);
        var dead = await DeadPid();
        await Leave(slot, ("owner", string.Create(CultureInfo.InvariantCulture, $"{dead} 2026-10-25T06:39:00Z ui-evidence\n")));

        var taken = await Acquire(kind);

        Assert.True(taken.IsSuccess, "a waiter must take a lock whose C# holder is dead");
        await taken.Value.DisposeAsync();
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task A_lock_left_by_a_dead_bash_holder_is_reclaimed(LockKind kind)
    {
        var slot = Slot(kind);
        var dead = await DeadPid();
        await Leave(slot, ("pid", string.Create(CultureInfo.InvariantCulture, $"{dead}\n")));

        var taken = await Acquire(kind);

        Assert.True(taken.IsSuccess, "a waiter must take a lock whose bash holder is dead");
        await taken.Value.DisposeAsync();
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task A_held_lock_records_its_pid_in_the_file_the_shell_reads(LockKind kind)
    {
        await using var held = (await Acquire(kind)).Value;

        var pid = await File.ReadAllTextAsync(Path.Combine(Slot(kind), "pid"), TestContext.Current.CancellationToken);

        Assert.Equal(Environment.ProcessId.ToString(CultureInfo.InvariantCulture), pid.Trim());
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task A_lock_with_a_live_holder_is_not_reclaimed(LockKind kind)
    {
        var live = Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
        await Leave(Slot(kind), ("pid", live + "\n"), ("owner", live + " 2026-10-25T06:39:00Z ui-evidence\n"));

        var busy = await Acquire(kind);

        Assert.Equal(EvidenceErrors.LockBusy.Code, busy.Error.Code);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task A_lock_with_no_holder_file_is_not_reclaimed(LockKind kind)
    {
        await Leave(Slot(kind));   // a holder between its mkdir and its pid write

        var busy = await Acquire(kind);

        Assert.Equal(EvidenceErrors.LockBusy.Code, busy.Error.Code);
    }

    public void Dispose() => _root.Dispose();

    private string Slot(LockKind kind) =>
        kind == LockKind.QaStack ? LockTestEnvironment.Settings(_root).QaStack : LockTestEnvironment.HeavySlot(_root, 1);

    private async ValueTask<Result<ILockLease>> Acquire(LockKind kind) =>
        await LockTestEnvironment.Locks(_root, slots: "1").AcquireAsync(kind, TimeSpan.Zero, TestContext.Current.CancellationToken);

    private static async Task Leave(string slot, params (string Name, string Text)[] files)
    {
        _ = Directory.CreateDirectory(slot);
        foreach (var (name, text) in files)
        {
            await File.WriteAllTextAsync(Path.Combine(slot, name), text, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>The pid of a process that has just exited.</summary>
    private static async Task<int> DeadPid()
    {
        using var finished = Process.Start(new ProcessStartInfo("true") { UseShellExecute = false }) ?? throw new InvalidOperationException("can't start 'true'");
        await finished.WaitForExitAsync(TestContext.Current.CancellationToken);
        return finished.Id;
    }
}
