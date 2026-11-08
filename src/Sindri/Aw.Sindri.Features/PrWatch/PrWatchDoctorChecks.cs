using Aw.Sindri.Core;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.PrWatch;

/// <summary>
/// Doctor's 'pr-watch' check (PR babysitter spec §6.2, API15): the PR watcher's last run, from the 'services' view. A run that failed in the forge's error leaves the pull requests of waiting
/// instances unwatched (their own timeouts still run), and this says which error and what fixes it. A watcher that has not run, or whose last run was ok, is ok.
/// </summary>
/// <param name="views">Reads the services view.</param>
internal sealed class PrWatchDoctorChecks(IViewReader views) : IDoctorChecks
{
    internal const string Name = "pr-watch";
    internal const string Service = "pr-watch";

    public async ValueTask<IReadOnlyList<DoctorCheck>> Run(CancellationToken cancellationToken)
    {
        var row = await views.Get<ServiceRow>(ViewNames.Services, Service, cancellationToken).ConfigureAwait(false);
        return [row.IsError || row.Value?.LastRun is not { Outcome: ServiceOutcome.Failed } failed ? Ok(row.IsError ? null : row.Value) : Failed(failed)];
    }

    private static DoctorCheck Ok(ServiceRow? row) =>
        new(Name, CheckState.Ok, row?.LastRun is null ? "the watcher has not run yet" : "the watcher's last run was ok", Fix: null);

    private static DoctorCheck Failed(LastRun run) => run.Code switch
    {
        var code when code == SindriErrors.NoForge.Code => new DoctorCheck(Name, CheckState.Warn, "the watcher can't poll: no forge is configured, so pull requests are not watched", "set workflow.forge.adapter in the profile"),
        var code when code == SindriErrors.ForgeRefused.Code => new DoctorCheck(Name, CheckState.Fail, "the forge refused the watcher's credential or host, so pull requests are not watched", "check workflow.forge.credential and workflow.forge.hosts (the forge:credential check shows the credential)"),
        var code when code == SindriErrors.ForgeUnavailable.Code => new DoctorCheck(Name, CheckState.Warn, "the forge did not answer three polls in a row; the watcher backs off to 15 minutes", "check the network and workflow.forge.hosts"),
        _ => new DoctorCheck(Name, CheckState.Warn, $"the watcher's last run failed with {run.Code}", "sindri services (the pr-watch row shows the error)"),
    };
}
