using System.Globalization;
using Aw.Meta.Core;
using Aw.Meta.Infrastructure.Files;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Adapters;

public sealed class HeavyLockDirectoryTests : IDisposable
{
    private readonly TempDirectory _dir = new();

    private string TakeLock(int pid, DateTime takenAt)
    {
        var slot = Path.Combine(_dir.Path, "heavy-job.lock");
        _ = Directory.CreateDirectory(slot);
        var owner = Path.Combine(slot, "owner");
        File.WriteAllText(owner, string.Create(CultureInfo.InvariantCulture, $"{pid} 2026-10-17T09:00:00Z session=1 dotnet build"));
        File.SetLastWriteTimeUtc(owner, takenAt);
        return Path.Combine(_dir.Path, "heavy-job.lock");
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_holder_pid_that_a_later_process_reused_is_a_stale_lock()
    {
        var path = TakeLock(77, new DateTime(2026, 10, 17, 9, 0, 0, DateTimeKind.Utc));
        var liveness = new InMemoryProcessLiveness();
        liveness.Run(77, new DateTimeOffset(2026, 10, 17, 11, 0, 0, TimeSpan.Zero));

        var locks = await HeavyLockDirectory.List(path, liveness, TestContext.Current.CancellationToken);

        var only = Assert.Single(locks.Value);
        Assert.Equal((77, false), (only.Pid, only.Alive));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_holder_that_started_before_the_lock_was_taken_is_alive()
    {
        var path = TakeLock(77, new DateTime(2026, 10, 17, 9, 0, 0, DateTimeKind.Utc));
        var liveness = new InMemoryProcessLiveness();
        liveness.Run(77, new DateTimeOffset(2026, 10, 17, 8, 0, 0, TimeSpan.Zero));

        var locks = await HeavyLockDirectory.List(path, liveness, TestContext.Current.CancellationToken);

        Assert.True(Assert.Single(locks.Value).Alive);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_holder_the_host_cannot_be_asked_about_is_listed_alive_not_an_error()
    {
        var path = TakeLock(77, new DateTime(2026, 10, 17, 9, 0, 0, DateTimeKind.Utc));
        var liveness = new InMemoryProcessLiveness();
        liveness.FailWith(77, MetaErrors.HostUnreadable.With("unavailable on macOS: ps could not be run"));

        var locks = await HeavyLockDirectory.List(path, liveness, TestContext.Current.CancellationToken);

        Assert.True(locks.IsSuccess);
        Assert.Equal((77, true), (Assert.Single(locks.Value).Pid, Assert.Single(locks.Value).Alive));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_holders_are_checked_with_one_batch()
    {
        var path = TakeLock(77, new DateTime(2026, 10, 17, 9, 0, 0, DateTimeKind.Utc));
        var second = Path.Combine(_dir.Path, "heavy-job.lock.2");
        _ = Directory.CreateDirectory(second);
        await File.WriteAllTextAsync(Path.Combine(second, "owner"), "78 2026-10-17T09:00:00Z session=1 dotnet test", TestContext.Current.CancellationToken);
        var liveness = new InMemoryProcessLiveness();

        _ = await HeavyLockDirectory.List(path, liveness, TestContext.Current.CancellationToken);

        Assert.Equal((1, 0), (liveness.BatchCalls, liveness.SingleCalls));
    }

    public void Dispose() => _dir.Dispose();
}
