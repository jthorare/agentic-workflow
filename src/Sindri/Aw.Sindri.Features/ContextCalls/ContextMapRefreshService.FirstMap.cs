using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Features.ContextCalls;

// The first map's wait (#3126) and the quiet run's left-out note.
internal sealed partial class ContextMapRefreshService
{
    // The code index's build job (its slice owns the kind; AW1003 keeps slices off each other's types).
    private static JobKind IndexBuildKind { get; } = JobKind.From("BuildCodeIndex");

    // #3126: with no map yet (no current version, no held draft), the first build waits until a code index of a mapped repo has succeeded; a map built before its index is built again anyway (#1704).
    // The jobs view is read only then. Returns true while the first map waits.
    private async ValueTask<Result<bool>> FirstMapWaits(IReadOnlyList<RepoHead> heads, MapPublishState state, CancellationToken cancellationToken)
    {
        if (state.Current is not null || state.Hold is not null)
        {
            return false;
        }

        var builds = await jobs.List(JobState.Succeeded, IndexBuildKind, cancellationToken).ConfigureAwait(false);
        if (builds.IsError)
        {
            return builds.Error;
        }

        var mapped = heads.Select(static h => h.Repo.Value).ToHashSet(StringComparer.Ordinal);
        return !builds.Value.Any(j => j.State == JobState.Succeeded && j.Repo is { } repo && mapped.Contains(repo.Value));
    }

    private static string? Left(LeftOutRepos elsewhere)
    {
        string?[] notes =
        [
            elsewhere.OffTrunk.Length == 0 ? null : "not on trunk, left out: " + string.Join(", ", elsewhere.OffTrunk),
            elsewhere.NotCheckout.Length == 0 ? null : "not a git checkout, left out: " + string.Join(", ", elsewhere.NotCheckout) + " (fix: git init and commit in it, or remove it from repos:)",
        ];
        return notes.OfType<string>().ToList() is { Count: > 0 } said ? string.Join("; ", said) : null;
    }

    // The mapped repos a tick leaves out (#3371): a checkout off its default branch, and a profile repo that is no git checkout at all.
    private sealed record LeftOutRepos(string[] OffTrunk, string[] NotCheckout);
}
