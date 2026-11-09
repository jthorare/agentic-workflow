using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.SweepLostSessions;

/// <summary>Whether a person holds the Step a launch belongs to (the takeover pauses its timer), so the idle sweep leaves the session alone.</summary>
/// <param name="views">The 'launches' view: the instance a launch belongs to.</param>
/// <param name="instances">Reads the instance, to see whether a person took it over.</param>
/// <param name="files">The per-launch files: the marker the engine leaves while a person holds a graph node's session (PR 7); null in a test that has none.</param>
internal sealed class StepTakeovers(ILaunchViews views, IInstanceReader instances, ILaunchFiles? files = null)
{
    /// <summary>Whether a person holds the Step's instance, or it can't be read now: either way the session is left alone this sweep. A launch with no instance (attended) has no turn hooks.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>True when the session is to be left alone.</returns>
    public async ValueTask<bool> Held(LaunchId launch, CancellationToken cancellationToken)
    {
        if (files?.TakenOver(launch) == true)
        {
            return true;   // a graph node's session a person holds: the takeover paused the node's timeout, and the idle sweep leaves the session alone too
        }

        var row = await views.Get(launch, cancellationToken).ConfigureAwait(false);
        if (row.IsError)
        {
            return true;
        }

        if (row.Value?.Step.Instance is not { } address)
        {
            return false;
        }

        var instance = await instances.Get(address, cancellationToken).ConfigureAwait(false);
        return instance.IsError || instance.Value.TakenOverBy is not null;
    }
}
