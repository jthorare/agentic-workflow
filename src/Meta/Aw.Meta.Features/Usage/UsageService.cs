using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Ports;
using Aw.Meta.Core.Routing;
using Aw.Meta.Core.Usage;

namespace Aw.Meta.Features.Usage;

/// <summary>Reports which lanes and whether the meta stopped at a usage limit, and what each lane runs on.</summary>
/// <param name="config">The team config.</param>
/// <param name="reader">Looks at the lanes and the meta.</param>
/// <param name="standings">Adds provider, model and usage windows.</param>
internal sealed class UsageService(IMetaConfig config, UsageReader reader, LaneStandingReader standings) : IService<UsageRequest, UsageResponse>
{
    public async ValueTask<Result<UsageResponse>> Handle(UsageRequest request, CancellationToken cancellationToken)
    {
        var team = await config.Load(cancellationToken).ConfigureAwait(false);
        if (team.IsError)
        {
            return team.Error;
        }

        var readings = await reader.Read(team.Value, cancellationToken).ConfigureAwait(false);
        if (readings.IsError)
        {
            return readings.Error;
        }

        var looked = await standings.Read(team.Value, readings.Value, cancellationToken).ConfigureAwait(false);
        var lanes = looked.ToDictionary(static s => s.Lane, StringComparer.Ordinal);
        var rows = StandingReadings.Merge(readings.Value, looked).Select(r => Row(r, lanes.GetValueOrDefault(r.Subject))).ToList();
        return new UsageResponse(rows, rows.Exists(static r => r.State == UsageState.Limited));
    }

    private static UsageRow Row(UsageReading reading, LaneStanding? lane)
    {
        var plain = new UsageRow(reading.Subject, reading.State, reading.Stop?.Kind, reading.Stop?.ResetsAt, reading.Source, reading.Reason);
        return lane is null
            ? plain
            : plain with { Agent = lane.Agent, Model = lane.Model, RemainingPct = lane.RemainingPct, Windows = lane.Signal?.Windows, SignalNote = lane.Note };
    }
}
