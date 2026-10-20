using System.Text.Json;
using Aw.Context.Core;
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
using Aw.Kernel;

namespace Aw.Context.Testing.Engine;

/// <summary>
/// The in-memory <see cref="IContextEngine"/>, for callers (Sindri's tests) that don't query context: it checks settings with the engine's own
/// pure rules (SettingsReader, ScopeRules for the repos) and the extra scrub patterns with <see cref="FakeScrubberFactory"/>, and answers every query or refresh with SND-CONTEXT-011. It
/// passes ContextEngineContract. The real engine and its fakes
/// are tested in Aw.Context.Tests.
/// </summary>
public sealed class FakeContextEngine : IContextEngine
{
    private readonly FakeScrubberFactory _scrubbers = new();

    private readonly ProbeSet? _probes;

    private FakeContextEngine(ProbeSet? probes)
    {
        _probes = probes;
    }

    /// <summary>The one instance, with no probe file.</summary>
    public static FakeContextEngine Instance { get; } = new(probes: null);

    /// <summary>An engine like <see cref="Instance"/> whose probe file is <paramref name="probes"/>: a run asks its questions through the caller's delegate and scores them with the real runner (nothing is saved).</summary>
    /// <param name="probes">The probes of the file.</param>
    /// <returns>The engine.</returns>
    public static FakeContextEngine WithProbes(ProbeSet probes) => new(probes);

    /// <inheritdoc />
    public Result<ContextSettings> CheckSettings(JsonElement? section, IReadOnlyList<ProfileRepoEntry> repos) =>
        SettingsReader.Read(section).Bind(read => _scrubbers.For(read.ExtraPatterns)).Bind(_ => SettingsReader.Check(section, repos));   // the keys, then the patterns, then the repos

    /// <inheritdoc />
    public ValueTask<Result<ContextResult>> Query(ContextQuery query, ContextScope scope, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<ContextResult>>(Down());

    /// <inheritdoc />
    public ValueTask<Result<SourcesView>> Sources(ContextScope scope, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<SourcesView>>(Down());

    /// <inheritdoc />
    public ValueTask<Result<bool>> HasSyncGap(ContextScope scope, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<bool>>(Down());

    /// <inheritdoc />
    public ValueTask<Result<RefreshReport>> Refresh(ContextScope scope, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<RefreshReport>>(Down());

    /// <summary>No map: every map call answers SND-CONTEXT-030.</summary>
    public ValueTask<Result<SyncReport>> Sync(string? source, ContextScope scope, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<SyncReport>>(Down());

    public ValueTask<Result<MapFilePreview>> PreviewMap(string path, ContextScope scope, CancellationToken cancellationToken) => NoMap<MapFilePreview>();

    /// <inheritdoc/>
    public ValueTask<Result<MapImported>> ImportMap(string path, string confirmedHash, ContextScope scope, CancellationToken cancellationToken) => NoMap<MapImported>();

    /// <inheritdoc/>
    public ValueTask<Result<MapExported>> ExportMap(string? path, ContextScope scope, CancellationToken cancellationToken) => NoMap<MapExported>();

    /// <inheritdoc/>
    public ValueTask<Result<MapStatusView>> MapStatus(string? anchor, ContextScope scope, CancellationToken cancellationToken) => NoMap<MapStatusView>();

    /// <summary>With no probes a run answers SND-CONTEXT-036; with <see cref="WithProbes"/> it runs them.</summary>
    public async ValueTask<Result<ProbeRun>> EvalProbes(string? file, Func<ProbeCase, CancellationToken, ValueTask<Result<ContextResult>>> ask, CancellationToken cancellationToken)
    {
        if (_probes is null)
        {
            return ContextErrors.NoProbeFile.With("this test engine has no probe file");
        }

        var report = await ProbeRunner.Run(new ProbeFile(file ?? "/probes/probes.json", "fake-hash", _probes), ask, baseline: null, DateTimeOffset.UnixEpoch, cancellationToken).ConfigureAwait(false);
        return report.IsError ? report.Error : new ProbeRun(report.Value, Path: null);
    }

    /// <summary>The fake keeps no history or issues: a backtest finds no eligible sample (SND-CONTEXT-080).</summary>
    public ValueTask<Result<BacktestRun>> EvalBacktest(BacktestRequest request, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<BacktestRun>>(ContextErrors.NoEligibleSample.With("this test engine has no merge history or issues"));

    /// <summary>The fake has no map: SND-CONTEXT-030.</summary>
    public ValueTask<Result<MapQualityRun>> EvalMap(MapQualityRequest request, CancellationToken cancellationToken) => NoMap<MapQualityRun>();

    /// <summary>The fake keeps no eval files.</summary>
    public ValueTask<Result<EvalPurgeReport>> EvalPurge(EvalPurgeRequest request, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<EvalPurgeReport>>(request.Scope.Caller.Kind == ContextCallerKind.Session
            ? ContextErrors.SessionsDisabled.With("purging eval files is a human or service call: a session can't")
            : new EvalPurgeReport(0, 0, 0));

    /// <inheritdoc/>
    public ValueTask<Result<NodeView>> ShowNode(string key, ContextScope scope, CancellationToken cancellationToken) => NoMap<NodeView>();

    /// <inheritdoc/>
    public ValueTask<Result<NeighborsView>> Neighbors(string key, int? depth, ContextScope scope, CancellationToken cancellationToken) => NoMap<NeighborsView>();

    /// <inheritdoc/>
    public ValueTask<Result<PathView>> Path(string from, string to, ContextScope scope, CancellationToken cancellationToken) => NoMap<PathView>();

    /// <inheritdoc/>
    public ValueTask<Result<MapBuildReport>> BuildMap(IProgress<string>? progress, ContextScope scope, CancellationToken cancellationToken) => NoMap<MapBuildReport>();

    /// <inheritdoc/>
    public ValueTask<Result<ReviewQueueView>> ReviewQueue(int? limit, ContextScope scope, CancellationToken cancellationToken) => NoMap<ReviewQueueView>();

    /// <inheritdoc/>
    public ValueTask<Result<ReviewResult>> ApplyReview(ReviewDecision decision, ContextScope scope, CancellationToken cancellationToken) => NoMap<ReviewResult>();

    /// <inheritdoc/>
    public ValueTask<Result<ReviewExported>> ExportReview(string? path, ContextScope scope, CancellationToken cancellationToken) => NoMap<ReviewExported>();

    /// <inheritdoc/>
    public ValueTask<Result<ReviewResult>> ApplyReviewFile(string path, ContextScope scope, CancellationToken cancellationToken) => NoMap<ReviewResult>();

    /// <inheritdoc/>
    public ValueTask<Result<ReviewHistoryView>> ReviewHistory(int? limit, ContextScope scope, CancellationToken cancellationToken) => NoMap<ReviewHistoryView>();

    /// <inheritdoc/>
    public ValueTask<Result<ReviewFilePreview>> PreviewReviewFile(string path, ContextScope scope, CancellationToken cancellationToken) => NoMap<ReviewFilePreview>();

    /// <inheritdoc/>
    public ValueTask<Result<MapPromoted>> Promote(ContextScope scope, CancellationToken cancellationToken) => NoMap<MapPromoted>();

    /// <inheritdoc/>
    public ValueTask<Result<CleanupReport>> CleanDraft(ContextScope scope, CancellationToken cancellationToken) => NoMap<CleanupReport>();

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<RepoHead>>> MapHeads(ContextScope scope, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<IReadOnlyList<RepoHead>>>(Array.Empty<RepoHead>());

    /// <inheritdoc/>
    public ValueTask<Result<ProposeResult>> Propose(ScopeProposal proposal, ContextScope scope, CancellationToken cancellationToken) =>
        proposal.Surfaces.Count == 0 ? ValueTask.FromResult(Results.Success(new ProposeResult(Draft: null, Added: 0, Existing: 0, Rejected: 0))) : NoMap<ProposeResult>();   // like the engine: with no map a withdrawal has nothing to take back

    /// <inheritdoc/>
    public ValueTask<Result<ProposalsView>> Proposals(string run, ContextScope scope, CancellationToken cancellationToken) => NoMap<ProposalsView>();

    /// <inheritdoc/>
    public ValueTask<Result<LensesView>> Lenses(string anchor, MapRead read, int? depth, ContextScope scope, CancellationToken cancellationToken) => NoMap<LensesView>();

    /// <inheritdoc/>
    public ValueTask<Result<LensesSetView>> LensesOf(IReadOnlyList<string> anchors, MapRead read, int? depth, ContextScope scope, CancellationToken cancellationToken) => NoMap<LensesSetView>();

    /// <inheritdoc/>
    public ValueTask<Result<SubgraphView>> Subgraph(SubgraphQuery query, ContextScope scope, CancellationToken cancellationToken) => NoMap<SubgraphView>();

    /// <inheritdoc/>
    public ValueTask<Result<CodeView>> Code(IReadOnlyList<string> anchors, bool draft, string? mapVersion, ContextScope scope, CancellationToken cancellationToken) => NoMap<CodeView>();

    /// <inheritdoc/>
    public ValueTask<Result<HistoryView>> History(IReadOnlyList<string> anchors, HistoryWindow window, MapRead read, ContextScope scope, CancellationToken cancellationToken) => NoMap<HistoryView>();

    /// <inheritdoc/>
    public ValueTask<Result<LensesView>> Lenses(string anchor, MapRead read, LensRange range, ContextScope scope, CancellationToken cancellationToken) => NoMap<LensesView>();

    /// <inheritdoc/>
    public ValueTask<Result<LensesSetView>> LensesOf(IReadOnlyList<string> anchors, MapRead read, LensRange range, ContextScope scope, CancellationToken cancellationToken) => NoMap<LensesSetView>();

    /// <inheritdoc/>
    public ValueTask<Result<FileMapView>> FileMap(FileMapQuery query, ContextScope scope, CancellationToken cancellationToken) => NoMap<FileMapView>();

    /// <inheritdoc/>
    public ValueTask<Result<MapHealth>> CheckMap(ContextScope? scope, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success(new MapHealth("context:map", MapHealthState.Warn, "no map yet", "sindri context map import <file>")));

    /// <inheritdoc/>
    public ValueTask<Result<MapHealth>> CheckRetention(CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success(new MapHealth("context:retention", MapHealthState.Ok, "nothing to purge", Fix: null)));

    /// <inheritdoc/>
    public ValueTask<Result<PurgeReport>> PurgeExpired(CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success(new PurgeReport(Exports: 0, Versions: 0)));

    private static ValueTask<Result<T>> NoMap<T>() => ValueTask.FromResult<Result<T>>(ContextErrors.NoMap.With("this test engine has no map"));

    private static Error Down() => ContextErrors.AllSourcesUnavailable.With("this test engine answers settings checks only");
}
