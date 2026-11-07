using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.SweepLostSessions;

/// <summary>Whether a person holds the Step a launch belongs to (the takeover pauses its timer), so the idle sweep leaves the session alone.</summary>
/// <param name="views">The 'launches' view: the instance a launch belongs to.</param>
/// <param name="instances">Reads the instance, to see whether a person took it over.</param>
internal sealed class StepTakeovers(ILaunchViews views, IInstanceReader instances)
{
    /// <summary>Whether a person holds the Step's instance, or it can't be read now: either way the session is left alone this sweep. A launch with no instance (attended) has no turn hooks.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>True when the session is to be left alone.</returns>
    public async ValueTask<bool> Held(LaunchId launch, CancellationToken cancellationToken)
    {
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

    /// <summary>Whether the launch is a skill block's session (skill flows spec §3.3 item 4): it waits on the owner for as long as the Step's timeout allows, so it is never idle or stalled.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>True for a skill launch.</returns>
    public async ValueTask<bool> SkillBlock(LaunchId launch, CancellationToken cancellationToken)
    {
        _ = await views.Get(launch, cancellationToken).ConfigureAwait(false);   // F-2a: the row's skill is not read yet
        return false;
    }
}
