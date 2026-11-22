using Aw.Context.Core.Eval;
using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Core.Eval.MapQuality;
using Aw.Context.Core.Facade;
using Aw.Context.Core.History;
using Aw.Context.Core.Map;
using Aw.Context.Features.Adjudicate;
using Aw.Context.Features.CheckRetention;
using Aw.Context.Features.CheckStore;
using Aw.Context.Features.EvalMap;
using Aw.Context.Features.EvalProbes;
using Aw.Context.Features.EvalPurge;
using Aw.Context.Features.GetHistory;
using Aw.Context.Features.QueryContext;
using Aw.Context.Features.RunBacktest;
using Aw.Context.Features.RunUpkeep;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Context.Features;

public static partial class ContextFeaturesServiceCollectionExtensions
{
    private static IServiceCollection AddMapHealthChecks(this IServiceCollection services) => services
        .AddScoped<IService<CheckRetentionRequest, MapHealth>, CheckRetentionService>()
        .AddScoped<IService<CheckStoreRequest, MapHealth>, CheckStoreService>()
        .AddScoped<IService<RunUpkeepRequest, StoreUpkeep?>, RunUpkeepService>()
        .AddScoped<MapHealthChecks>();

    private static IServiceCollection AddHistory(this IServiceCollection services) => services
        .AddScoped<IService<GetHistoryRequest, HistoryView>, GetHistoryService>()
        .AddScoped<HistoryPipeline>()
        .AddScoped<IContextHistory>(static sp => sp.GetRequiredService<ContextGraph>())
        .AddScoped<IContextConcepts>(static sp => sp.GetRequiredService<ContextGraph>());

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
