using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Kernel;
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
/// <param name="engine">The context engine: the mapped repos' heads, which say the repos the map waits on.</param>
internal sealed class FirstMapDoctorCheck(IJobViews jobs, IServicePauses pauses, IContextEngine engine) : IFirstMapCheck
{
    // The code index's build job (its slice owns the kind; AW1003 keeps slices off each other's types).
    private static JobKind IndexBuildKind { get; } = JobKind.From("BuildCodeIndex");

    private static JobKind BuildKind { get; } = JobKind.From("BuildContextMap");

    public async ValueTask<DoctorCheck> Check(LoadedProfile? approved, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> named = approved is null ? [] : [.. ContextScopes.Entries(approved.Profile).Select(static r => r.Name)];
        var repos = approved is null || named.Count == 0 ? named : await WaitedOn(approved.Profile, named, cancellationToken).ConfigureAwait(false);
        var paused = await pauses.Paused(cancellationToken).ConfigureAwait(false);
        var refreshes = await jobs.List(state: null, ContextMapRefreshService.RefreshKind, cancellationToken).ConfigureAwait(false);
        var builds = await jobs.List(state: null, BuildKind, cancellationToken).ConfigureAwait(false);
        var indexBuilds = await jobs.List(JobState.Succeeded, IndexBuildKind, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<JobRow> mapJobs = [.. Rows(refreshes), .. Rows(builds)];
        var indexed = Rows(indexBuilds).Select(static j => j.Repo?.Value).OfType<string>().ToHashSet(StringComparer.Ordinal);
        return FirstMapRow.Of(repos, !paused.IsError && paused.Value.Contains(ContextMapRefreshService.ServiceId), mapJobs, indexed, [.. named.Except(repos, StringComparer.Ordinal)]);
    }

    // The repos the map waits on (#3216): a checkout off its default branch is left out, as the refresh tick leaves it out of the map and no code index builds it (#3181).
    // A repo with no head (a missing folder) stays named, so it keeps failing loudly; a heads read that fails names every repo, as before.
    private async ValueTask<IReadOnlyList<string>> WaitedOn(SindriProfile profile, IReadOnlyList<string> named, CancellationToken cancellationToken)
    {
        var scope = ContextScopes.For(profile, new ContextCaller(ContextCallerKind.Service, Repo: null, ContextClient.Service));
        var heads = await engine.MapHeads(scope, cancellationToken).ConfigureAwait(false);
        if (heads.IsError)
        {
            return named;
        }

        // why: when #3181 lands (RepoHead.DefaultTip), an off-default repo builds from its default tip; the rule then becomes "no default tip", and the PR landing second switches it
        var offDefault = heads.Value.Where(static h => !h.OnDefault).Select(static h => h.Repo.Value).ToHashSet(StringComparer.Ordinal);
        return [.. named.Where(r => !offDefault.Contains(r))];
    }

    private static IReadOnlyList<JobRow> Rows(Result<IReadOnlyList<JobRow>> read) => read.IsError ? [] : read.Value;
}
