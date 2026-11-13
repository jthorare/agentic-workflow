using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>Reads the map jobs, the code index builds and the pauses for <see cref="FirstMapRow"/> (#3216). A read that fails counts as nothing: doctor runs with no control plane, and then the row says what the map waits on.</summary>
/// <param name="jobs">The 'jobs' view.</param>
/// <param name="pauses">The paused services.</param>
internal sealed class FirstMapDoctorCheck(IJobViews jobs, IServicePauses pauses) : IFirstMapCheck
{
    // The code index's build job (its slice owns the kind; AW1003 keeps slices off each other's types).
    private static JobKind IndexBuildKind { get; } = JobKind.From("BuildCodeIndex");

    private static JobKind BuildKind { get; } = JobKind.From("BuildContextMap");

    public async ValueTask<DoctorCheck> Check(LoadedProfile? approved, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> repos = approved is null ? [] : [.. ContextScopes.Entries(approved.Profile).Select(static r => r.Name)];
        var paused = await pauses.Paused(cancellationToken).ConfigureAwait(false);
        var refreshes = await jobs.List(state: null, ContextMapRefreshService.RefreshKind, cancellationToken).ConfigureAwait(false);
        var builds = await jobs.List(state: null, BuildKind, cancellationToken).ConfigureAwait(false);
        var indexBuilds = await jobs.List(JobState.Succeeded, IndexBuildKind, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<JobRow> mapJobs = [.. Rows(refreshes), .. Rows(builds)];
        var indexed = Rows(indexBuilds).Select(static j => j.Repo?.Value).OfType<string>().ToHashSet(StringComparer.Ordinal);
        return FirstMapRow.Of(repos, !paused.IsError && paused.Value.Contains(ContextMapRefreshService.ServiceId), mapJobs, indexed);
    }

    private static IReadOnlyList<JobRow> Rows(Aw.Kernel.Result<IReadOnlyList<JobRow>> read) => read.IsError ? [] : read.Value;
}
