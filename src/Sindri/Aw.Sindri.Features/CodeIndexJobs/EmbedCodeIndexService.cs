using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Features.CodeIndexJobs;

/// <summary>
/// The work of a code index embed job (code index spec §6.3, §8): resolves the repo in the caller's scope and runs the facade's embed with the job's progress. It holds no heavy-job slot. A build holding the repo's index
/// (SND-CODEIDX-016) or an index not built yet (013) ends the job as the 'later' outcome, not a failure; a model that fails or does not match its pin fails it.
/// </summary>
/// <param name="scopes">The profile's scope and the caller.</param>
/// <param name="index">The code index.</param>
/// <param name="clock">Times the run.</param>
/// <param name="jobs">The running job: its progress takes the batch lines.</param>
internal sealed class EmbedCodeIndexService(CodeIndexJobScopes scopes, ICodeIndex index, TimeProvider clock, IJobContextAccessor jobs) : IService<EmbedCodeIndexRequest, EmbedCodeIndexResult>
{
    public async ValueTask<Result<EmbedCodeIndexResult>> Handle(EmbedCodeIndexRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!RepoName.TryFrom(request.Repo, out var repo))
        {
            return SindriErrors.InvalidInput.With("repo: not a repo name (fix: sindri jobs show on the job that started this)");
        }

        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return scope.Error;
        }

        var started = clock.GetTimestamp();
        var embedded = await index.Embed(scope.Value, repo, jobs.Current?.Progress ?? new Progress<string>(), cancellationToken).ConfigureAwait(false);
        var milliseconds = (long)clock.GetElapsedTime(started).TotalMilliseconds;
        if (embedded.IsError)
        {
            return embedded.Error.Code == CodeIndexErrors.IndexBusy.Code || embedded.Error.Code == CodeIndexErrors.IndexNotBuilt.Code
                ? new EmbedCodeIndexResult(request.Repo, EmbedCodeIndexResult.Later, Embedded: 0, Embeddable: 0, Carried: 0, Made: 0, milliseconds)
                : embedded.Error;
        }

        var report = embedded.Value;
        return new EmbedCodeIndexResult(request.Repo, report.Embeddings == LayerState.Ok ? EmbedCodeIndexResult.Ok : EmbedCodeIndexResult.Pending, report.Embedded, report.Embeddable, report.Carried, report.Made, milliseconds);
    }
}
