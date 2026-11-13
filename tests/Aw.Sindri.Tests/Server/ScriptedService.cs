using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Tests.Server;

/// <summary>A background service named 'tidy' that answers its runs from a script, one result per run; the last repeats.</summary>
internal sealed class ScriptedService(params Result<ScheduledResult>[] script) : IScheduledService
{
    private int _runs;

    public static Error Failure { get; } = SindriErrors.LedgerUnavailable.With("tidy: the ledger was busy");

    public static Result<ScheduledResult> Failed { get; } = Failure;

    /// <summary>The command gate stayed busy past its 5 s wait (SND-API-009), as a service's own command answers it.</summary>
    public static Result<ScheduledResult> Busy { get; } = SindriErrors.CommandBusy.With("another command held the command gate for 5 s; try again in a moment");

    public static Result<ScheduledResult> Worked { get; } = ScheduledResult.Worked;

    public static Result<ScheduledResult> Quiet { get; } = ScheduledResult.Quiet;

    public int Runs => Volatile.Read(ref _runs);

    public ServiceName Identity() => ServiceName.From("tidy");

    public ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken) =>
        ValueTask.FromResult(script[Math.Min(Interlocked.Increment(ref _runs), script.Length) - 1]);
}
