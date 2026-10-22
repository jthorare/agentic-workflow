using System.Diagnostics;
using System.Text.Json;
using Aw.Context.Core.Code;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Eval;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Graph;
using Aw.Context.Core.History;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;
using Aw.Context.Core.Proposals;
using Aw.Context.Core.Results;
using Aw.Context.Core.Review;
using Aw.Context.Core.Subgraph;
using Aw.Kernel;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>
/// Wraps the host's real context engine and records, for each query, the engine caller Sindri built and the span the call
/// ran inside, so a test can show an MCP call took the same query chain as the HTTP route.
/// </summary>
internal sealed class RecordingEngine(IContextEngine inner, List<(ContextCaller Caller, string? Span)> calls, Func<Context.Core.Eval.Backtest.BacktestRequest, CancellationToken, ValueTask<Result<Context.Core.Eval.Backtest.BacktestRun>>>? backtest = null, Action? beforeQueue = null, Func<ContextScope, ValueTask>? beforePromote = null) : IContextEngine
{
    /// <summary>Replaces the registered engine with one that records into <paramref name="calls"/>.</summary>
    /// <param name="services">The host's services.</param>
    /// <param name="calls">Receives each recorded call.</param>
    /// <param name="backtest">Answers EvalBacktest instead of the real engine (a backtest needs a git history and a brief source), or null.</param>
    /// <param name="beforeQueue">Runs before each review-queue read (a test moves the clock there to stand for a slow queue build), or null.</param>
    /// <param name="beforePromote">Runs before each promote, with its scope (a test replaces the draft there, as a refresh would between the publish check and the promote), or null.</param>
    public static void Install(IServiceCollection services, List<(ContextCaller Caller, string? Span)> calls, Func<Context.Core.Eval.Backtest.BacktestRequest, CancellationToken, ValueTask<Result<Context.Core.Eval.Backtest.BacktestRun>>>? backtest = null, Action? beforeQueue = null, Func<ContextScope, ValueTask>? beforePromote = null)
    {
        var original = services.Last(d => d.ServiceType == typeof(IContextEngine));
        _ = services.Remove(original);
        services.Add(new ServiceDescriptor(typeof(IContextEngine), sp => new RecordingEngine(Resolve(original, sp), calls, backtest, beforeQueue, beforePromote), original.Lifetime));
    }

    public Result<ContextSettings> CheckSettings(JsonElement? section, IReadOnlyList<ProfileRepoEntry> repos) => inner.CheckSettings(section, repos);

    public ValueTask<Result<ContextResult>> Query(ContextQuery query, ContextScope scope, CancellationToken cancellationToken)
    {
        lock (calls)
        {
            calls.Add((scope.Caller, Activity.Current?.DisplayName));
        }

        return inner.Query(query, scope, cancellationToken);
    }

    public ValueTask<Result<SourcesView>> Sources(ContextScope scope, CancellationToken cancellationToken) => inner.Sources(scope, cancellationToken);

    public ValueTask<Result<bool>> HasSyncGap(ContextScope scope, CancellationToken cancellationToken) => inner.HasSyncGap(scope, cancellationToken);

    public ValueTask<Result<RefreshReport>> Refresh(ContextScope scope, CancellationToken cancellationToken) => inner.Refresh(scope, cancellationToken);

    public ValueTask<Result<SyncReport>> Sync(string? source, ContextScope scope, CancellationToken cancellationToken) => inner.Sync(source, scope, cancellationToken);

    public ValueTask<Result<MapFilePreview>> PreviewMap(string path, ContextScope scope, CancellationToken cancellationToken) => inner.PreviewMap(path, scope, cancellationToken);

    public ValueTask<Result<MapImported>> ImportMap(string path, string confirmedHash, ContextScope scope, CancellationToken cancellationToken) =>
        inner.ImportMap(path, confirmedHash, scope, cancellationToken);

    public ValueTask<Result<MapExported>> ExportMap(string? path, ContextScope scope, CancellationToken cancellationToken) => inner.ExportMap(path, scope, cancellationToken);

    public ValueTask<Result<MapStatusView>> MapStatus(string? anchor, ContextScope scope, CancellationToken cancellationToken) => inner.MapStatus(anchor, scope, cancellationToken);

    public ValueTask<Result<ProbeRun>> EvalProbes(string? file, Func<ProbeCase, CancellationToken, ValueTask<Result<ContextResult>>> ask, CancellationToken cancellationToken) =>
        inner.EvalProbes(file, ask, cancellationToken);

    public ValueTask<Result<Context.Core.Eval.Backtest.BacktestRun>> EvalBacktest(Context.Core.Eval.Backtest.BacktestRequest request, CancellationToken cancellationToken) =>
        backtest is null ? inner.EvalBacktest(request, cancellationToken) : backtest(request, cancellationToken);

    public ValueTask<Result<Context.Core.Eval.MapQuality.MapQualityRun>> EvalMap(Context.Core.Eval.MapQuality.MapQualityRequest request, CancellationToken cancellationToken) =>
        inner.EvalMap(request, cancellationToken);

    public ValueTask<Result<Context.Core.Eval.Backtest.EvalPurgeReport>> EvalPurge(Context.Core.Eval.MapQuality.EvalPurgeRequest request, CancellationToken cancellationToken) =>
        inner.EvalPurge(request, cancellationToken);

    public ValueTask<Result<NodeView>> ShowNode(string key, ContextScope scope, CancellationToken cancellationToken) => inner.ShowNode(key, scope, cancellationToken);

    public ValueTask<Result<NeighborsView>> Neighbors(string key, int? depth, ContextScope scope, CancellationToken cancellationToken) =>
        inner.Neighbors(key, depth, scope, cancellationToken);

    public ValueTask<Result<PathView>> Path(string from, string to, ContextScope scope, CancellationToken cancellationToken) => inner.Path(from, to, scope, cancellationToken);

    public ValueTask<Result<MapBuildReport>> BuildMap(IProgress<string>? progress, ContextScope scope, CancellationToken cancellationToken) =>
        inner.BuildMap(progress, scope, cancellationToken);

    public ValueTask<Result<ReviewQueueView>> ReviewQueue(int? limit, ContextScope scope, CancellationToken cancellationToken)
    {
        beforeQueue?.Invoke();
        return inner.ReviewQueue(limit, scope, cancellationToken);
    }

    public ValueTask<Result<ReviewResult>> ApplyReview(ReviewDecision decision, ContextScope scope, CancellationToken cancellationToken) =>
        inner.ApplyReview(decision, scope, cancellationToken);

    public ValueTask<Result<ReviewExported>> ExportReview(string? path, ContextScope scope, CancellationToken cancellationToken) =>
        inner.ExportReview(path, scope, cancellationToken);

    public ValueTask<Result<ReviewResult>> ApplyReviewFile(string path, ContextScope scope, CancellationToken cancellationToken) =>
        inner.ApplyReviewFile(path, scope, cancellationToken);

    public ValueTask<Result<ReviewHistoryView>> ReviewHistory(int? limit, ContextScope scope, CancellationToken cancellationToken) => inner.ReviewHistory(limit, scope, cancellationToken);

    public ValueTask<Result<ReviewFilePreview>> PreviewReviewFile(string path, ContextScope scope, CancellationToken cancellationToken) => inner.PreviewReviewFile(path, scope, cancellationToken);

    public async ValueTask<Result<MapPromoted>> Promote(ContextScope scope, CancellationToken cancellationToken, int? expectedVersion = null)
    {
        if (beforePromote is not null)
        {
            await beforePromote(scope).ConfigureAwait(false);
        }

        return await inner.Promote(scope, cancellationToken, expectedVersion).ConfigureAwait(false);
    }

    public ValueTask<Result<CleanupReport>> CleanDraft(ContextScope scope, CancellationToken cancellationToken, int? expectedVersion = null) => inner.CleanDraft(scope, cancellationToken, expectedVersion);

    public ValueTask<Result<MapPublishState>> Held(ContextScope scope, CancellationToken cancellationToken) => inner.Held(scope, cancellationToken);

    public ValueTask<Result<MapHold>> Hold(MapHold hold, ContextScope scope, CancellationToken cancellationToken) => inner.Hold(hold, scope, cancellationToken);

    public ValueTask<Result<Unit>> Release(int version, ContextScope scope, CancellationToken cancellationToken) => inner.Release(version, scope, cancellationToken);

    public ValueTask<Result<MapPublishView>> PublishStatus(ContextScope scope, CancellationToken cancellationToken) => inner.PublishStatus(scope, cancellationToken);

    public ValueTask<Result<IReadOnlyList<RepoHead>>> MapHeads(ContextScope scope, CancellationToken cancellationToken) => inner.MapHeads(scope, cancellationToken);

    public ValueTask<Result<ProposeResult>> Propose(ScopeProposal proposal, ContextScope scope, CancellationToken cancellationToken) =>
        inner.Propose(proposal, scope, cancellationToken);

    public ValueTask<Result<ProposalsView>> Proposals(string run, ContextScope scope, CancellationToken cancellationToken) => inner.Proposals(run, scope, cancellationToken);

    public ValueTask<Result<LensesView>> Lenses(string anchor, MapRead read, int? depth, ContextScope scope, CancellationToken cancellationToken) =>
        inner.Lenses(anchor, read, depth, scope, cancellationToken);

    public ValueTask<Result<LensesSetView>> LensesOf(IReadOnlyList<string> anchors, MapRead read, int? depth, ContextScope scope, CancellationToken cancellationToken) =>
        inner.LensesOf(anchors, read, depth, scope, cancellationToken);

    public ValueTask<Result<SubgraphView>> Subgraph(SubgraphQuery query, ContextScope scope, CancellationToken cancellationToken) =>
        inner.Subgraph(query, scope, cancellationToken);

    public ValueTask<Result<CodeView>> Code(IReadOnlyList<string> anchors, bool draft, string? mapVersion, ContextScope scope, CancellationToken cancellationToken) =>
        inner.Code(anchors, draft, mapVersion, scope, cancellationToken);

    public ValueTask<Result<HistoryView>> History(IReadOnlyList<string> anchors, HistoryWindow window, MapRead read, ContextScope scope, CancellationToken cancellationToken) =>
        inner.History(anchors, window, read, scope, cancellationToken);

    public ValueTask<Result<LensesView>> Lenses(string anchor, MapRead read, LensRange range, ContextScope scope, CancellationToken cancellationToken) =>
        inner.Lenses(anchor, read, range, scope, cancellationToken);

    public ValueTask<Result<LensesSetView>> LensesOf(IReadOnlyList<string> anchors, MapRead read, LensRange range, ContextScope scope, CancellationToken cancellationToken) =>
        inner.LensesOf(anchors, read, range, scope, cancellationToken);

    public ValueTask<Result<FileMapView>> FileMap(FileMapQuery query, ContextScope scope, CancellationToken cancellationToken) =>
        inner.FileMap(query, scope, cancellationToken);

    public ValueTask<Result<MapHealth>> CheckMap(ContextScope? scope, CancellationToken cancellationToken) => inner.CheckMap(scope, cancellationToken);

    public ValueTask<Result<MapHealth>> CheckRetention(CancellationToken cancellationToken) => inner.CheckRetention(cancellationToken);

    public ValueTask<Result<PurgeReport>> PurgeExpired(CancellationToken cancellationToken) => inner.PurgeExpired(cancellationToken);

    private static IContextEngine Resolve(ServiceDescriptor original, IServiceProvider services) =>
        (IContextEngine)(original.ImplementationInstance
            ?? original.ImplementationFactory?.Invoke(services)
            ?? ActivatorUtilities.CreateInstance(services, original.ImplementationType!));   // why: a descriptor has one of the three
}
