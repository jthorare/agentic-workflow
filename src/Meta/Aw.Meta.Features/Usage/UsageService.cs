using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Ports;
using Aw.Meta.Core.Routing;
using Aw.Meta.Core.Usage;

namespace Aw.Meta.Features.Usage;

/// <summary>Reports which lanes and whether the meta stopped at a usage limit.</summary>
/// <param name="config">The team config.</param>
/// <param name="reader">Looks at the lanes and the meta.</param>
/// <param name="standings">Adds provider, model and usage windows.</param>
internal sealed class UsageService(IMetaConfig config, UsageReader reader, LaneStandingReader standings) : IService<UsageRequest, UsageResponse>
{
    public async ValueTask<Result<UsageResponse>> Handle(UsageRequest request, CancellationToken cancellationToken)
    {
        _ = standings;
        var team = await config.Load(cancellationToken).ConfigureAwait(false);
        if (team.IsError)
        {
            return team.Error;
        }

        var readings = await reader.Read(team.Value, cancellationToken).ConfigureAwait(false);
        return readings.IsError
            ? readings.Error
            : new UsageResponse([.. readings.Value.Select(Row)], readings.Value.Any(static r => r.State == UsageState.Limited));
    }

    private static UsageRow Row(UsageReading reading) => new(reading.Subject, reading.State, reading.Stop?.Kind, reading.Stop?.ResetsAt, reading.Source, reading.Reason);
}
