using Aw.Kernel;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The profile's 'workflow:' times (engine spec S1-D11) for the worker: 'workflow.deskTimeout', the floor every desk
/// wait is raised to, 'workflow.eventHold', how long a buffered event signal waits, and (engine P6 unit (d))
/// 'workflow.dispatchDelay', the undo window before each effect, read from the approved profile like the allowlist. 'LoadRecipe' reads it when an
/// instance starts, so the interpreter takes the value from history and a replay sees the same one. A profile that
/// doesn't load gives the defaults (2d, 24h): serve refuses to run on an invalid profile before any instance starts.
/// </summary>
/// <param name="profiles">The live profile.</param>
/// <param name="approved">The approved profile: 'workflow.dispatchDelay' (S1-D11, engine P6 unit (d)).</param>
internal sealed class ProfileWorkflowTimes(IProfileSource profiles, IApprovedProfile approved)
{
    /// <summary>The times the profile sets, each key's default where it sets none.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The times.</returns>
    public async ValueTask<WorkflowTimes> Read(CancellationToken cancellationToken)
    {
        var loaded = await profiles.Load(cancellationToken).ConfigureAwait(false);
        return loaded.IsError ? WorkflowTimes.Defaults : WorkflowTimes.Of(loaded.Value.Profile);
    }

    /// <summary>The approved profile's 'workflow.dispatchDelay', or its default (10s) when none is approved or it sets none.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The delay; an error when the approvals can't be read.</returns>
    public async ValueTask<Result<TimeSpan>> DispatchDelay(CancellationToken cancellationToken)
    {
        var latest = await approved.Latest(cancellationToken).ConfigureAwait(false);
        if (latest.IsError)
        {
            return latest.Error;
        }

        return latest.Value is { } snapshot ? EffectSettings.Of(snapshot.Profile).DispatchDelay : EffectSettings.DefaultDispatchDelay;
    }
}
