using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Features.CodeIndexJobs;

/// <summary>
/// The work of a code index build job (code index spec §8): resolves the repo in the caller's scope, runs the facade's build with the job's progress, and, for a published index,
/// appends 'codeindex.built' just before the runner appends 'job.finished'.
/// </summary>
/// <param name="scopes">The profile's scope and the caller.</param>
/// <param name="index">The code index.</param>
/// <param name="events">Appends 'codeindex.built' under the held lease.</param>
/// <param name="jobs">The running job: its progress takes the step lines and its id keys the event.</param>
internal sealed class BuildCodeIndexService(CodeIndexJobScopes scopes, ICodeIndex index, CodeIndexEventSource events, IJobContextAccessor jobs) : IService<BuildCodeIndexRequest, BuildCodeIndexResult>
{
    public async ValueTask<Result<BuildCodeIndexResult>> Handle(BuildCodeIndexRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!RepoName.TryFrom(request.Repo, out var repo) || !CommitSha.TryFrom(request.Commit, out var commit))
        {
            return SindriErrors.InvalidInput.With("repo or commit: not a repo name or a 40-digit lower-case commit (fix: sindri jobs show on the job that started this)");
        }

        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return scope.Error;
        }

        var built = await index.Build(scope.Value, repo, commit, jobs.Current?.Progress ?? new Progress<string>(), cancellationToken).ConfigureAwait(false);
        if (built.IsError)
        {
            return built.Error;
        }

        var appended = await Record(built.Value, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : Result(built.Value);
    }

    // A published index is announced just before the runner appends 'job.finished'; a skipped build changed nothing.
    private async ValueTask<Result<Unit>> Record(BuildReport report, CancellationToken cancellationToken)
    {
        if (report.Outcome != BuildOutcome.Published || report.Commit is not { } published)
        {
            return Unit.Value;
        }

        var appended = await events.Built(Payload(report, published), jobs.Current?.Job.Value ?? Guid.NewGuid().ToString("N"), cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : Unit.Value;
    }

    private static CodeIndexBuilt Payload(BuildReport report, CommitSha commit) => new(report.Repo.Value, commit.Value,
        [.. report.Layers.Select(static l => new CodeIndexBuiltLayer(Word(l.Layer), Word(l.State), l.Code))],
        report.Counts.Files, report.Counts.Symbols, report.Counts.Edges, report.UnresolvedShare, report.AmbiguousShare,
        report.Embeddable == 0 ? 0 : (int)(100L * report.Embedded / report.Embeddable), report.Milliseconds, report.Incremental);

    private static BuildCodeIndexResult Result(BuildReport report) => new(report.Repo.Value, report.Commit?.Value, Word(report.Outcome),
        report.Counts.Files, report.Counts.Symbols, report.Counts.Edges, report.Incremental, report.Milliseconds)
    {
        LayersPending = [.. report.Layers.Where(static l => l.State == LayerState.Pending).Select(static l => Word(l.Layer))],
        Embedded = report.Embedded,
    };

    private static string Word(BuildOutcome outcome) => outcome switch
    {
        BuildOutcome.Published => "published",
        BuildOutcome.NotOnMain => "not-on-main",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, message: null),
    };

    private static string Word(LayerName layer) => layer.ToString().ToLowerInvariant();

    private static string Word(LayerState state) => state.ToString().ToLowerInvariant();
}
