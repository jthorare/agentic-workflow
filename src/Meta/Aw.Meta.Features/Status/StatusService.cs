using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Gate;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.Status;

/// <summary>Where the meta stands: what it owes, the merge queue, the owner's queue and the lane map.</summary>
/// <param name="config">The team config.</param>
/// <param name="values">Holds the last health summary.</param>
/// <param name="status">Reads the parts.</param>
/// <param name="resources">Reads memory and disk for the builders advice.</param>
/// <param name="advisor">Recomputes the heavy reservation and computes the builders advice.</param>
/// <param name="processes">Tells whether the process that runs a train is still there.</param>
/// <param name="clock">The current time.</param>
internal sealed class StatusService(IMetaConfig config, StatusReader status, IKeyValueStore values, IHostResources resources, ResourceAdvisor advisor, IProcessTable processes, TimeProvider clock) : IService<StatusRequest, StatusResponse>
{
    public async ValueTask<Result<StatusResponse>> Handle(StatusRequest request, CancellationToken cancellationToken)
    {
        var team = await config.Load(cancellationToken).ConfigureAwait(false);
        if (team.IsError)
        {
            return team.Error;
        }

        var read = await status.Read(team.Value.MetaLane, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        var stored = await Stored(cancellationToken).ConfigureAwait(false);
        if (read.IsError || stored.IsError)
        {
            return read.IsError ? read.Error : stored.Error;
        }

        // The reservation and the advice are extras: a host or file that can't be read leaves them out instead of failing the status.
        var host = await resources.Read(cancellationToken).ConfigureAwait(false);
        var advice = host.IsError ? null : await advisor.Advise(team.Value, host.Value, refresh: true, cancellationToken).ConfigureAwait(false);
        return read.Value with { Context = await status.Context(team.Value, cancellationToken).ConfigureAwait(false), LastHealth = stored.Value.Health, Train = await Train(stored.Value.Train, cancellationToken).ConfigureAwait(false), Heavy = advice?.IsSuccess == true ? advice.Value.Heavy : null, Builders = advice?.IsSuccess == true ? advice.Value.Builders : null, Consumer = await Consumer(read.Value, cancellationToken).ConfigureAwait(false) };
    }

    // An unreadable heartbeat leaves the line out rather than failing the status.
    private async ValueTask<string?> Consumer(StatusResponse read, CancellationToken cancellationToken)
    {
        var line = await ConsumerState.Read(values, read.Obligations.Count(static o => o.Kind == ObligationKind.Reply), clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return line.IsError ? null : line.Value.Line;
    }

    // A board with a start and no end whose process is gone is a train that died without writing its end line (a SIGKILL, a power loss).
    private async ValueTask<string?> Train(string? board, CancellationToken cancellationToken)
    {
        if (board is null)
        {
            return null;
        }

        var snapshot = await processes.Snapshot(cancellationToken).ConfigureAwait(false);
        return snapshot.IsError ? board : TrainText.MarkStale(board, pid => snapshot.Value.Processes.Any(p => p.Pid == pid));
    }

    private async ValueTask<Result<(string? Health, string? Train)>> Stored(CancellationToken cancellationToken)
    {
        var health = await values.Get(KvKeys.HealthLast, cancellationToken).ConfigureAwait(false);
        var train = await values.Get(KvKeys.Train, cancellationToken).ConfigureAwait(false);
        return health.IsError || train.IsError ? health.IsError ? health.Error : train.Error : (health.Value, train.Value);
    }
}
