using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Locks;
using Aw.Kernel;
using Aw.Sindri.Core.Locks;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The context engine's heavy-job lock (context spec §4), over Sindri's own port <see cref="IHeavySlots"/> (the shared lock directory,
/// so builds, evidence runs and the shell tools count against one cap). A lock that stays busy, or a lock directory that can't be
/// used, is SND-CONTEXT-040: a map build never runs without its slot.
/// </summary>
internal sealed class SindriHeavyJobLock(IHeavySlots slots, TimeSpan wait) : IHeavyJobLock
{
    /// <summary>How long a build queues for a slot before it answers 040: hours, not a minute, so a busy box makes it wait with progress instead of fail (owner q93, 2026-10-23); cancelling the job ends the wait.</summary>
    internal static TimeSpan DefaultWait { get; } = TimeSpan.FromHours(12);

    public async ValueTask<Result<IAsyncDisposable>> Take(IProgress<string>? waiting, CancellationToken cancellationToken)
    {
        var taken = await slots.Take("sindri context-map-build", wait, waiting, cancellationToken).ConfigureAwait(false);
        if (taken.IsError)
        {
            return ContextErrors.HeavyLockBusy.With($"the heavy-job lock can't be used: {taken.Error.Message}");
        }

        return taken.Value.Lease is { } lease
            ? Results.Success(lease)
            : ContextErrors.HeavyLockBusy.With(string.Create(CultureInfo.InvariantCulture,
                $"the heavy-job lock stayed busy for {wait.TotalSeconds:0.#} s: {taken.Value.Busy} (fix: wait for that job to finish, then run it again)"));
    }
}
