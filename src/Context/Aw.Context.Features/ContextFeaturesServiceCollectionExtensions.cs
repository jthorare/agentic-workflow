using Aw.Context.Core.Code;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Eval;
using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Core.Eval.MapQuality;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Graph;
using Aw.Context.Core.History;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Locks;
using Aw.Context.Core.Map;
using Aw.Context.Core.Proposals;
using Aw.Context.Core.Results;
using Aw.Context.Core.Review;
using Aw.Context.Core.Subgraph;
using Aw.Context.Core.Symbols;
using Aw.Context.Features.Adjudicate;
using Aw.Context.Features.ApplyReviewFile;
using Aw.Context.Features.BuildMap;
using Aw.Context.Features.CheckMap;
using Aw.Context.Features.CheckRetention;
using Aw.Context.Features.CleanMap;
using Aw.Context.Features.EvalMap;
using Aw.Context.Features.EvalProbes;
using Aw.Context.Features.EvalPurge;
using Aw.Context.Features.ExportMap;
using Aw.Context.Features.ExportReview;
using Aw.Context.Features.FindPath;
using Aw.Context.Features.GetCode;
using Aw.Context.Features.GetFileMap;
using Aw.Context.Features.GetHistory;
using Aw.Context.Features.GetLenses;
using Aw.Context.Features.GetProposals;
using Aw.Context.Features.GetSubgraph;
using Aw.Context.Features.HeldMap;
using Aw.Context.Features.HoldMap;
using Aw.Context.Features.ImportMap;
using Aw.Context.Features.ListNeighbors;
using Aw.Context.Features.MapHeads;
using Aw.Context.Features.PreviewMap;
using Aw.Context.Features.PreviewReviewFile;
using Aw.Context.Features.PromoteMap;
using Aw.Context.Features.ProposeFromScope;
using Aw.Context.Features.PurgeMap;
using Aw.Context.Features.QueryContext;
using Aw.Context.Features.RefreshDocs;
using Aw.Context.Features.ReleaseMap;
using Aw.Context.Features.ReviewHistory;
using Aw.Context.Features.ReviewMap;
using Aw.Context.Features.ReviewQueue;
using Aw.Context.Features.RunBacktest;
using Aw.Context.Features.ShowMapStatus;
using Aw.Context.Features.ShowNode;
using Aw.Context.Features.ShowPublishStatus;
using Aw.Context.Features.SourcesStatus;
using Aw.Context.Features.SyncSources;
using Aw.Kernel;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aw.Context.Features;

/// <summary>Registers Aw.Context.Features: the IContextEngine facade and the slices behind it.</summary>
public static class ContextFeaturesServiceCollectionExtensions
{
    /// <summary>Adds the facade (IContextEngine, and IContextMap behind it) and every Context use case as an IService. The ports come from Aw.Context.Infrastructure.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddContextFeatures(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<ICodeSymbolSource, NoCodeSymbolSource>();   // the host that wires the code index (Sindri's Host adapter) replaces it
        services.TryAddSingleton<ILabeler, DisabledLabeler>();   // a host that sets up labeling (AddContextLabeling) replaces it
        services.TryAddSingleton<IHeavyJobLock, UnconfiguredHeavyJobLock>();   // a host that wires the box-wide lock replaces it
        services.TryAddSingleton<UnconfiguredBacktest>();   // hosts that wire the backtest ports (Infrastructure) replace these
        services.TryAddSingleton<IBriefSource>(sp => sp.GetRequiredService<UnconfiguredBacktest>());
        services.TryAddSingleton<IMergeHistory>(sp => sp.GetRequiredService<UnconfiguredBacktest>());
        services.TryAddSingleton<IAsOfDocs>(sp => sp.GetRequiredService<UnconfiguredBacktest>());
        services.TryAddSingleton<IEvalStore>(sp => sp.GetRequiredService<UnconfiguredBacktest>());
        return services.AddMapHeads().AddContextSlices();
    }

    private static IServiceCollection AddContextSlices(this IServiceCollection services) => services
        .AddBacktest()
        .AddHistory()
        .AddScoped<IService<QueryContextRequest, ContextResult>, QueryContextService>()
        .AddScoped<LiveInputs>()
        .AddScoped<IService<SourcesStatusRequest, SourcesView>, SourcesStatusService>()
        .AddScoped<IService<RefreshDocsRequest, RefreshReport>, RefreshDocsService>()
        .AddScoped<IService<SyncSourcesRequest, SyncReport>, SyncSourcesService>()
        .AddScoped<ContextSources>()
        .AddSingleton<SyncLocks>()
        .AddScoped<IService<PreviewMapRequest, MapFilePreview>, PreviewMapService>()
        .AddScoped<IService<ImportMapRequest, MapImported>, ImportMapService>()
        .AddScoped<IService<ExportMapRequest, MapExported>, ExportMapService>()
        .AddScoped<IService<ShowMapStatusRequest, MapStatusView>, ShowMapStatusService>()
        .AddScoped<IService<CheckMapRequest, MapHealth>, CheckMapService>()
        .AddScoped<IService<CheckRetentionRequest, MapHealth>, CheckRetentionService>()
        .AddScoped<IService<PurgeMapRequest, PurgeReport>, PurgeMapService>()
        .AddScoped<IService<ShowNodeRequest, NodeView>, ShowNodeService>()
        .AddScoped<IService<ListNeighborsRequest, NeighborsView>, ListNeighborsService>()
        .AddScoped<IService<FindPathRequest, PathView>, FindPathService>()
        .AddScoped<IService<BuildMapRequest, MapBuildReport>, BuildMapService>()
        .AddReviewSlices()
        .AddPromoteSlices()
        .AddScoped<IService<ProposeFromScopeRequest, ProposeResult>, ProposeFromScopeService>()
        .AddScoped<IService<GetProposalsRequest, ProposalsView>, GetProposalsService>()
        .AddScoped<IService<GetFileMapRequest, FileMapView>, GetFileMapService>()
        .AddSingleton<MapViewCache>()
        .AddScoped<MapReader>()
        .AddScoped<MapAdmin>()
        .AddScoped<MapReview>()
        .AddScoped<MapProposals>()
        .AddScoped<MapPublishing>()
        .AddPublishSlices()
        .AddScoped<IContextMap, ContextMap>()
        .AddScoped<ContextGraph>()
        .AddScoped<IContextGraph>(static sp => sp.GetRequiredService<ContextGraph>())
        .AddScoped<IContextLenses>(static sp => sp.GetRequiredService<ContextGraph>())
        .AddScoped<IContextEngine, ContextEngine>()
        .AddLensSlices();

    private static IServiceCollection AddReviewSlices(this IServiceCollection services) => services
        .AddScoped<IService<ReviewQueueRequest, ReviewQueueView>, ReviewQueueService>()
        .AddScoped<IService<ReviewHistoryRequest, ReviewHistoryView>, ReviewHistoryService>()
        .AddScoped<IService<ReviewMapRequest, ReviewResult>, ReviewMapService>()
        .AddScoped<IService<ExportReviewRequest, ReviewExported>, ExportReviewService>()
        .AddScoped<IService<PreviewReviewFileRequest, ReviewFilePreview>, PreviewReviewFileService>()
        .AddScoped<IService<ApplyReviewFileRequest, ReviewResult>, ApplyReviewFileService>();

    // The hold, its release and the two publish reads (F1P).
    private static IServiceCollection AddPublishSlices(this IServiceCollection services) => services
        .AddScoped<IService<HeldMapRequest, MapPublishState>, HeldMapService>()
        .AddScoped<IService<HoldMapRequest, MapHold>, HoldMapService>()
        .AddScoped<IService<ReleaseMapRequest, Unit>, ReleaseMapService>()
        .AddScoped<IService<ShowPublishStatusRequest, MapPublishView>, ShowPublishStatusService>();

    // The repo heads the map watcher reads (F1).
    private static IServiceCollection AddMapHeads(this IServiceCollection services) =>
        services.AddScoped<IService<MapHeadsRequest, IReadOnlyList<RepoHead>>, MapHeadsService>();

    // The lenses and the code lens share the basis reader and the map reader's views.
    private static IServiceCollection AddLensSlices(this IServiceCollection services) => services
        .AddScoped<IService<GetLensesRequest, LensesView>, GetLensesService>()
        .AddScoped<IService<GetLensesSetRequest, LensesSetView>, GetLensesSetService>()
        .AddScoped<IService<GetSubgraphRequest, SubgraphView>, GetSubgraphService>()
        .AddScoped<LensBasisReader>()
        .AddScoped<ContextCodeHistory>()
        .AddScoped<IService<GetCodeRequest, CodeView>, GetCodeService>()
        .AddScoped<CodeLexicalSearch>()
        .AddScoped<CodeSymbolSearch>()
        .AddScoped<LexicalCandidates>()
        .AddScoped<LexicalTestsRule>()
        .AddScoped<LexicalSchemaRule>()
        .AddScoped<IContextCode>(static sp => sp.GetRequiredService<ContextGraph>());

    private static IServiceCollection AddPromoteSlices(this IServiceCollection services) => services
        .AddScoped<IService<PromoteMapRequest, MapPromoted>, PromoteMapService>()
        .AddScoped<IService<CleanMapRequest, CleanupReport>, CleanMapService>();

    private static IServiceCollection AddHistory(this IServiceCollection services) => services
        .AddScoped<IService<GetHistoryRequest, HistoryView>, GetHistoryService>()
        .AddScoped<HistoryPipeline>()
        .AddScoped<IContextHistory>(static sp => sp.GetRequiredService<ContextGraph>());

    private static IServiceCollection AddBacktest(this IServiceCollection services) => services
        .AddScoped<IService<EvalProbesRequest, ProbeRun>, EvalProbesService>()
        .AddScoped<IService<BacktestRequest, BacktestRun>, RunBacktestService>()
        .AddScoped<IService<MapQualityRequest, MapQualityRun>, EvalMapService>()
        .AddScoped<IService<EvalPurgeRequest, EvalPurgeReport>, EvalPurgeService>()
        .AddScoped<ContextEval>()
        .AddScoped<QueryPipeline>()
        .AddScoped<IArmQuery, ArmQuery>()
        .AddScoped<AsOfMapBuilder>()
        .AddScoped<ArmRunner>()
        .AddScoped<CorpusLoader>()
        .AddScoped<IAdjudicator, Adjudicator>()
        .AddScoped<SampleScorer>();
}
