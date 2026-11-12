using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Features.RestartServe;

/// <summary>The snapshot's launched sessions, looked up in the 'launches' view for their instance. An open run is a session whose launch has an instance: the filter scripts/lib/sindri-open-runs.sh uses.</summary>
internal sealed class OpenRuns(ISessionSnapshot snapshot, ILaunchViews launches) : IOpenRuns
{
    public async ValueTask<IReadOnlyList<KeptRun>> Read(CancellationToken cancellationToken)
    {
        if (snapshot.Current is not { } current)
        {
            return [];
        }

        var launched = current.Sessions.Where(static s => s.LaunchId is not null).ToList();
        var steps = await LaunchSteps.Of(launches, launched.Select(static s => s.LaunchId).OfType<LaunchId>(), cancellationToken).ConfigureAwait(false);
        var runs = new List<KeptRun>();
        foreach (var session in launched)
        {
            if (session.LaunchId is { } launch && steps.TryGetValue(launch, out var step) && step.Instance is { } instance)
            {
                runs.Add(new KeptRun($"{session.Ref}", instance.Value, step.State));
            }
        }

        return runs;
    }
}
