using Aw.Context.Core.ConceptSynthesis;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sync;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>Reads each feed's progress and where concept synthesis stands through the context engine's facade.</summary>
/// <param name="scopes">The profile's scope and the caller.</param>
/// <param name="engine">Context's facade: its record-feed members are Tick and Progress.</param>
internal sealed class GetIndexStatusService(ContextScopes scopes, IContextEngine engine) : IService<GetIndexStatusRequest, IndexStatusView>
{
    public async ValueTask<Result<IndexStatusView>> Handle(GetIndexStatusRequest request, CancellationToken cancellationToken)
    {
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return scope.Error;
        }

        var progress = await engine.Progress(scope.Value, cancellationToken).ConfigureAwait(false);
        if (progress.IsError)
        {
            return progress.Error;
        }

        // why: the concepts part is extra; a failed read leaves the feed rows standing and carries its code
        var concepts = await engine.ConceptStatus(scope.Value, cancellationToken).ConfigureAwait(false);
        return new IndexStatusView([.. progress.Value.Select(Row)]) { Concepts = concepts.IsError ? new IndexConceptsView(Enabled: true, Code: concepts.Error.Code) : Concepts(concepts.Value) };
    }

    private static IndexConceptsView Concepts(ConceptStatus c) =>
        new(c.Enabled, c.Concepts, c.MultiSource, c.Queued, c.Covered, c.Embedded, c.Pending, c.Embedded == 0 ? 0 : (double)c.Covered / c.Embedded, c.Watermark);

    private static IndexSourceRow Row(SourceProgress p) => new(p.Source.Value, Phase(p.Phase), p.Usable, p.Complete, p.LastSync, p.LastCode, p.ReReading, p.Pull, p.Records, p.LastCommit, p.LastError, p.LastErrorText, p.Failures, p.NextTry);

    private static string? Phase(SyncPhase? phase) => phase switch
    {
        SyncPhase.Recent => "recent",
        SyncPhase.Backfill => "backfill",
        SyncPhase.Incremental => "incremental",
        SyncPhase.Reconcile => "reconcile",
        _ => null,
    };
}
