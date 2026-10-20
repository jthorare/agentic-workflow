using Aw.Context.Core.Code;
using Aw.Context.Core.Eval.MapQuality;
using Aw.Context.Core.Facade;
using Aw.Context.Core.History;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Locks;
using Aw.Context.Core.Proposals;
using Aw.Context.Core.Review;
using Aw.Context.Core.Subgraph;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Locks;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.ContextCalls;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the context engine's slices, split from <see cref="SindriFeaturesServiceCollectionExtensions"/> to keep each file under the line cap.</summary>
internal static class ContextSlicesServiceCollectionExtensions
{
    /// <summary>The context engine's slices (stages S-3 and S-4): the review, promote and proposals commands and the reads of the map, lenses, subgraph and filemap.</summary>
    internal static IServiceCollection AddContextSlices(this IServiceCollection services)
    {
        _ = services
            .AddScoped<ContextScopes>()
            .AddScoped<ContextWarmup>()
            .AddScoped<IScopeProposals>(static sp => sp.GetRequiredService<IContextEngine>())
            .AddScoped<ContextChallenges>()
            .AddScoped<IScheduledService, ContextDocsRefreshService>()
            .AddScoped<IScheduledService, ContextPurgeService>()
            .AddScoped<IScheduledService, ContextRebuildService>()
            .AddScoped<IScheduledService, ContextSyncService>()
            .AddScoped<IScheduledService, ContextSyncGapsService>()
            .AddSingleton(ContextSyncGapsService.BuiltIn)
            .AddScoped<IScheduledService, ContextMapRefreshService>()
            .AddSingleton(ContextMapRefreshService.BuiltIn)
            .AddScoped<SyncGaps>()
            .AddScoped<ContextInputScopes>()
            .AddScoped<ContextInputFacts>()
            .AddScoped<IScopingTextShape, ContextTextShape>()
            .AddScoped<IScopeRunProposals, ScopeRunProposals>()
            .AddStepInput<ContextLensesInput>()
            .AddStepInput<ContextHistoryInput>()
            .AddStepInput<ContextCodeInput>()
            .AddSingleton<IHeavyJobLock>(static sp => new SindriHeavyJobLock(sp.GetRequiredService<IHeavySlots>(), SindriHeavyJobLock.DefaultWait));
        _ = services.AddScoped<IValidator<PromoteContextMapRequest>, AcceptAllValidator<PromoteContextMapRequest>>().AddCommand<PromoteContextMapRequest, PromoteContextMapResponse, PromoteContextMapService>();
        _ = services.AddScoped<IValidator<ProposeContextMapRequest>, AcceptAllValidator<ProposeContextMapRequest>>().AddCommand<ProposeContextMapRequest, ProposeContextMapResponse, ProposeContextMapService>();
        _ = services.AddScoped<IValidator<ReviewContextMapRequest>, AcceptAllValidator<ReviewContextMapRequest>>().AddCommand<ReviewContextMapRequest, ReviewContextMapResponse, ReviewContextMapService>();
        _ = services.AddScoped<IValidator<ExportContextReviewRequest>, AcceptAllValidator<ExportContextReviewRequest>>().AddQuery<ExportContextReviewRequest, ReviewExported, ExportContextReviewService>();
        _ = services.AddScoped<IValidator<GetContextFileMapRequest>, AcceptAllValidator<GetContextFileMapRequest>>().AddQuery<GetContextFileMapRequest, FileMapView, GetContextFileMapService>();
        _ = services.AddScoped<IValidator<GetContextLensesRequest>, AcceptAllValidator<GetContextLensesRequest>>().AddQuery<GetContextLensesRequest, LensesView, GetContextLensesService>();
        _ = services.AddScoped<IValidator<GetContextLensesSetRequest>, AcceptAllValidator<GetContextLensesSetRequest>>().AddQuery<GetContextLensesSetRequest, LensesSetView, GetContextLensesSetService>();
        _ = services.AddScoped<IValidator<GetContextSubgraphRequest>, AcceptAllValidator<GetContextSubgraphRequest>>().AddQuery<GetContextSubgraphRequest, SubgraphView, GetContextSubgraphService>();
        _ = services.AddScoped<IValidator<GetContextCodeRequest>, AcceptAllValidator<GetContextCodeRequest>>().AddQuery<GetContextCodeRequest, CodeView, GetContextCodeService>();
        _ = services.AddScoped<IValidator<GetContextLensesAnswerRequest>, AcceptAllValidator<GetContextLensesAnswerRequest>>().AddQuery<GetContextLensesAnswerRequest, LensesAnswer, GetContextLensesAnswerService>();
        _ = services.AddScoped<IValidator<GetContextHistoryRequest>, AcceptAllValidator<GetContextHistoryRequest>>().AddQuery<GetContextHistoryRequest, HistoryView, GetContextHistoryService>();
        _ = services.AddScoped<IValidator<GetContextEvalMapRequest>, AcceptAllValidator<GetContextEvalMapRequest>>().AddQuery<GetContextEvalMapRequest, MapQualityRun, GetContextEvalMapService>();
        _ = services.AddScoped<IValidator<GetContextProposalsRequest>, AcceptAllValidator<GetContextProposalsRequest>>().AddQuery<GetContextProposalsRequest, ProposalsView, GetContextProposalsService>();
        _ = services.AddScoped<IValidator<GetContextReviewRequest>, AcceptAllValidator<GetContextReviewRequest>>().AddQuery<GetContextReviewRequest, ReviewQueueView, GetContextReviewService>();
        _ = services.AddScoped<IValidator<GetContextReviewHistoryRequest>, AcceptAllValidator<GetContextReviewHistoryRequest>>().AddQuery<GetContextReviewHistoryRequest, ReviewHistoryView, GetContextReviewHistoryService>();
        return services;
    }
}
