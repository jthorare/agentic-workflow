using System.Text.Json;
using Aw.Context.Core.Code;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Eval;
using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Core.Eval.MapQuality;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Graph;
using Aw.Context.Core.History;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;
using Aw.Context.Core.Proposals;
using Aw.Context.Core.Results;
using Aw.Context.Core.Review;
using Aw.Context.Core.Subgraph;
using Aw.Context.Core.Trust;
using Aw.Context.Features.MapHeads;
using Aw.Context.Features.QueryContext;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features;

/// <summary>
/// The facade (spec §4): each method dispatches to its slice; settings are read the same way by all of them. A settings
/// check also builds the scrubbers, so 'profile validate' and approve refuse an extra pattern Redaction refuses.
/// </summary>
internal sealed class ContextEngine(
    IService<QueryContextRequest, ContextResult> query,
    ContextSources sources,
    IScrubberFactory scrubbers,
    IContextMap map,
    ContextGraph graph,
    ContextEval eval,
    IService<MapHeadsRequest, IReadOnlyList<RepoHead>> heads) : IContextEngine
{
    public Result<ContextSettings> CheckSettings(JsonElement? section, IReadOnlyList<ProfileRepoEntry> repos) =>
        SettingsReader.Read(section).Bind(read => scrubbers.For(read.ExtraPatterns)).Bind(_ => SettingsReader.Check(section, repos));   // the keys, then the patterns, then the repos

    public ValueTask<Result<ContextResult>> Query(ContextQuery query1, ContextScope scope, CancellationToken cancellationToken) =>
        query.Handle(new QueryContextRequest(query1, scope), cancellationToken);

    public ValueTask<Result<SourcesView>> Sources(ContextScope scope, CancellationToken cancellationToken) =>
        sources.Status(scope, cancellationToken);

    public ValueTask<Result<bool>> HasSyncGap(ContextScope scope, CancellationToken cancellationToken) =>
        sources.HasSyncGap(scope, cancellationToken);

    public ValueTask<Result<RefreshReport>> Refresh(ContextScope scope, CancellationToken cancellationToken) =>
        sources.Refresh(scope, cancellationToken);

    public ValueTask<Result<SyncReport>> Sync(string? source, ContextScope scope, CancellationToken cancellationToken) => sources.Sync(source, scope, cancellationToken);

    public ValueTask<Result<ProbeRun>> EvalProbes(string? file, Func<ProbeCase, CancellationToken, ValueTask<Result<ContextResult>>> ask, CancellationToken cancellationToken) =>
        eval.Probes(file, ask, cancellationToken);

    public ValueTask<Result<BacktestRun>> EvalBacktest(BacktestRequest request, CancellationToken cancellationToken) => eval.Backtest(request, cancellationToken);

    public ValueTask<Result<MapQualityRun>> EvalMap(MapQualityRequest request, CancellationToken cancellationToken) => eval.Map(request, cancellationToken);

    public ValueTask<Result<EvalPurgeReport>> EvalPurge(EvalPurgeRequest request, CancellationToken cancellationToken) => eval.Purge(request, cancellationToken);

    public ValueTask<Result<HistoryView>> History(IReadOnlyList<string> anchors, HistoryWindow window, MapRead read, ContextScope scope, CancellationToken cancellationToken) =>
        graph.History(anchors, window, read, scope, cancellationToken);

    public ValueTask<Result<NodeView>> ShowNode(string key, ContextScope scope, CancellationToken cancellationToken) => graph.ShowNode(key, scope, cancellationToken);

    public ValueTask<Result<NeighborsView>> Neighbors(string key, int? depth, ContextScope scope, CancellationToken cancellationToken) =>
        graph.Neighbors(key, depth, scope, cancellationToken);

    public ValueTask<Result<PathView>> Path(string from, string to, ContextScope scope, CancellationToken cancellationToken) => graph.Path(from, to, scope, cancellationToken);

    public ValueTask<Result<MapFilePreview>> PreviewMap(string path, ContextScope scope, CancellationToken cancellationToken) =>
        map.PreviewMap(path, scope, cancellationToken);

    public ValueTask<Result<MapImported>> ImportMap(string path, string confirmedHash, ContextScope scope, CancellationToken cancellationToken) =>
        map.ImportMap(path, confirmedHash, scope, cancellationToken);

    public ValueTask<Result<MapExported>> ExportMap(string? path, ContextScope scope, CancellationToken cancellationToken) =>
        map.ExportMap(path, scope, cancellationToken);

    public ValueTask<Result<MapStatusView>> MapStatus(string? anchor, ContextScope scope, CancellationToken cancellationToken) => map.MapStatus(anchor, scope, cancellationToken);

    public ValueTask<Result<MapHealth>> CheckMap(ContextScope? scope, CancellationToken cancellationToken) => map.CheckMap(scope, cancellationToken);

    public ValueTask<Result<MapHealth>> CheckRetention(CancellationToken cancellationToken) => map.CheckRetention(cancellationToken);

    public ValueTask<Result<PurgeReport>> PurgeExpired(CancellationToken cancellationToken) => map.PurgeExpired(cancellationToken);

    public ValueTask<Result<MapBuildReport>> BuildMap(IProgress<string>? progress, ContextScope scope, CancellationToken cancellationToken) =>
        map.BuildMap(progress, scope, cancellationToken);

    public ValueTask<Result<ReviewQueueView>> ReviewQueue(int? limit, ContextScope scope, CancellationToken cancellationToken) => map.ReviewQueue(limit, scope, cancellationToken);

    public ValueTask<Result<ReviewResult>> ApplyReview(ReviewDecision decision, ContextScope scope, CancellationToken cancellationToken) =>
        map.ApplyReview(decision, scope, cancellationToken);

    public ValueTask<Result<ReviewHistoryView>> ReviewHistory(int? limit, ContextScope scope, CancellationToken cancellationToken) => map.ReviewHistory(limit, scope, cancellationToken);

    public ValueTask<Result<ReviewExported>> ExportReview(string? path, ContextScope scope, CancellationToken cancellationToken) =>
        map.ExportReview(path, scope, cancellationToken);

    public ValueTask<Result<ReviewFilePreview>> PreviewReviewFile(string path, ContextScope scope, CancellationToken cancellationToken) =>
        map.PreviewReviewFile(path, scope, cancellationToken);

    public ValueTask<Result<ReviewResult>> ApplyReviewFile(string path, ContextScope scope, CancellationToken cancellationToken) =>
        map.ApplyReviewFile(path, scope, cancellationToken);

    public ValueTask<Result<MapPromoted>> Promote(ContextScope scope, CancellationToken cancellationToken) => map.Promote(scope, cancellationToken);

    public ValueTask<Result<CleanupReport>> CleanDraft(ContextScope scope, CancellationToken cancellationToken) => map.CleanDraft(scope, cancellationToken);

    public ValueTask<Result<IReadOnlyList<RepoHead>>> MapHeads(ContextScope scope, CancellationToken cancellationToken) =>
        heads.Handle(new MapHeadsRequest(scope), cancellationToken);

    public ValueTask<Result<ProposeResult>> Propose(ScopeProposal proposal, ContextScope scope, CancellationToken cancellationToken) =>
        map.Propose(proposal, scope, cancellationToken);

    public ValueTask<Result<ProposalsView>> Proposals(string run, ContextScope scope, CancellationToken cancellationToken) => map.Proposals(run, scope, cancellationToken);

    public ValueTask<Result<LensesView>> Lenses(string anchor, MapRead read, int? depth, ContextScope scope, CancellationToken cancellationToken) =>
        graph.Lenses(anchor, read, depth, scope, cancellationToken);

    public ValueTask<Result<LensesSetView>> LensesOf(IReadOnlyList<string> anchors, MapRead read, int? depth, ContextScope scope, CancellationToken cancellationToken) =>
        graph.LensesOf(anchors, read, depth, scope, cancellationToken);

    public ValueTask<Result<SubgraphView>> Subgraph(SubgraphQuery query, ContextScope scope, CancellationToken cancellationToken) =>
        graph.Subgraph(query, scope, cancellationToken);

    public ValueTask<Result<CodeView>> Code(IReadOnlyList<string> anchors, bool draft, string? mapVersion, ContextScope scope, CancellationToken cancellationToken) =>
        graph.Code(anchors, draft, mapVersion, scope, cancellationToken);

    public ValueTask<Result<LensesView>> Lenses(string anchor, MapRead read, LensRange range, ContextScope scope, CancellationToken cancellationToken) =>
        graph.Lenses(anchor, read, range, scope, cancellationToken);

    public ValueTask<Result<LensesSetView>> LensesOf(IReadOnlyList<string> anchors, MapRead read, LensRange range, ContextScope scope, CancellationToken cancellationToken) =>
        graph.LensesOf(anchors, read, range, scope, cancellationToken);

    public ValueTask<Result<FileMapView>> FileMap(FileMapQuery query, ContextScope scope, CancellationToken cancellationToken) =>
        graph.FileMap(query, scope, cancellationToken);
}
