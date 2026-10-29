using Aw.Context.Core.Facade;
using Aw.Context.Core.Sources;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The work of a named index sync job (owned-index spec §5.5; #1597): one tick of the named source's feed through <see cref="IContextEngine"/>, under the starting caller. It does
/// not go through the 'index-sync' service or its run guard, so the scheduled all-sources run being in flight never refuses it; only a sync of this same source already
/// running does, as that source's row code (SND-CONTEXT-056). A source that is down or capped is a row with its code, not a failed job.
/// </summary>
/// <param name="scopes">The profile's scope and the caller.</param>
/// <param name="engine">Context's facade: its named Tick.</param>
/// <param name="jobs">The running job: its progress takes a line.</param>
internal sealed class SyncIndexSourceService(ContextScopes scopes, IContextEngine engine, IJobContextAccessor jobs) : IService<SyncIndexSourceRequest, SyncIndexSourceResult>
{
    public async ValueTask<Result<SyncIndexSourceResult>> Handle(SyncIndexSourceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!SourceId.TryFrom(request.Source, out var source))
        {
            return SindriErrors.InvalidInput.With($"'{request.Source}' isn't a source id (kebab-case); fix: sindri index status");
        }

        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return scope.Error;
        }

        if (scope.Value.Settings is null)
        {
            return SindriErrors.InvalidInput.With("the approved profile has no 'context:' section, so no source is synced (fix: sindri profile edit)");
        }

        jobs.Current?.Progress.Report($"syncing {source.Value}");
        var report = await engine.Tick(scope.Value, source, cancellationToken).ConfigureAwait(false);
        if (report.IsError)
        {
            return report.Error;
        }

        var row = report.Value.Sources.FirstOrDefault(r => r.Source == source);
        return row is null
            ? SindriErrors.InvalidInput.With($"'{source.Value}' came back without a row")
            : new SyncIndexSourceResult(row.Source.Value, row.Fetched, row.Records, row.Code);
    }
}
