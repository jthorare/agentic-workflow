using Aw.Judge.Core.Ports;
using Aw.Judge.Features.ApproveDecision;
using Aw.Judge.Features.AskQuestion;
using Aw.Judge.Features.CheckTraits;
using Aw.Judge.Features.ExplainDecision;
using Aw.Judge.Features.GetBrief;
using Aw.Judge.Features.GetConfig;
using Aw.Judge.Features.GetHealth;
using Aw.Judge.Features.MapBriefByDispatch;
using Aw.Judge.Features.MapBriefByName;
using Aw.Judge.Features.SaveBrief;
using Aw.Judge.Features.SetBriefAgentId;
using Aw.Judge.Features.SetConfig;
using Aw.Judge.Features.SetPromptSort;
using Aw.Judge.Features.SortPrompt;
using Aw.Judge.Features.UndoDecision;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Features;

/// <summary>Registers Aw.Judge.Features's slices: each service, its validator and its decorators (outermost first).</summary>
public static class JudgeFeaturesServiceCollectionExtensions
{
    /// <summary>Adds every Judge use case as an IService.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddJudgeFeatures(this IServiceCollection services)
    {
        AddHealth(services);
        AddBriefs(services);
        AddQuestions(services);
        _ = services
            .AddScoped<ExplainDecisionService>()
            .AddScoped<IValidator<ExplainDecisionRequest>, ExplainDecisionValidator>()
            .AddScoped<IService<ExplainDecisionRequest, ExplainDecisionResponse>>(static sp =>
                new ValidatingService<ExplainDecisionRequest, ExplainDecisionResponse>(
                    sp.GetRequiredService<ExplainDecisionService>(),
                    sp.GetRequiredService<IValidator<ExplainDecisionRequest>>()));
        _ = services
            .AddScoped<UndoDecisionService>()
            .AddScoped<IValidator<UndoDecisionRequest>, UndoDecisionValidator>()
            .AddScoped<IService<UndoDecisionRequest, UndoDecisionResponse>>(static sp =>
                new ValidatingService<UndoDecisionRequest, UndoDecisionResponse>(
                    sp.GetRequiredService<UndoDecisionService>(),
                    sp.GetRequiredService<IValidator<UndoDecisionRequest>>()));
        _ = services
            .AddScoped<ApproveDecisionService>()
            .AddScoped<IValidator<ApproveDecisionRequest>, ApproveDecisionValidator>()
            .AddScoped<IService<ApproveDecisionRequest, ApproveDecisionResponse>>(static sp =>
                new ValidatingService<ApproveDecisionRequest, ApproveDecisionResponse>(
                    sp.GetRequiredService<ApproveDecisionService>(),
                    sp.GetRequiredService<IValidator<ApproveDecisionRequest>>()));
        _ = services
            .AddScoped<GetConfigService>()
            .AddScoped<IValidator<GetConfigRequest>, GetConfigValidator>()
            .AddScoped<IService<GetConfigRequest, GetConfigResponse>>(static sp =>
                new ValidatingService<GetConfigRequest, GetConfigResponse>(
                    sp.GetRequiredService<GetConfigService>(),
                    sp.GetRequiredService<IValidator<GetConfigRequest>>()));
        _ = services
            .AddScoped<SetConfigService>()
            .AddScoped<IValidator<SetConfigRequest>, SetConfigValidator>()
            .AddScoped<IService<SetConfigRequest, SetConfigResponse>>(static sp =>
                new ValidatingService<SetConfigRequest, SetConfigResponse>(
                    sp.GetRequiredService<SetConfigService>(),
                    sp.GetRequiredService<IValidator<SetConfigRequest>>()));
        return services;
    }

    // AskQuestion: every 'judge <question>', validated, then the retention prune after it (outermost first).
    private static void AddQuestions(IServiceCollection services)
    {
        AddCutoverVerbs(services);
        _ = services
            .AddScoped<AskQuestionService>()
            .AddScoped<IValidator<AskQuestionRequest>, AskQuestionValidator>()
            .AddScoped<IService<AskQuestionRequest, AskQuestionResponse>>(static sp =>
                new PruneAfterQuestion(
                    new ValidatingService<AskQuestionRequest, AskQuestionResponse>(
                        sp.GetRequiredService<AskQuestionService>(),
                        sp.GetRequiredService<IValidator<AskQuestionRequest>>()),
                    sp.GetRequiredService<IDecisionLog>(),
                    sp.GetRequiredService<TimeProvider>()));
    }

    // GetHealth.
    private static void AddHealth(IServiceCollection services)
    {
        _ = services
            .AddScoped<GetHealthService>()
            .AddScoped<IValidator<GetHealthRequest>, GetHealthValidator>()
            .AddScoped<IService<GetHealthRequest, GetHealthResponse>>(static sp =>
                new ValidatingService<GetHealthRequest, GetHealthResponse>(
                    sp.GetRequiredService<GetHealthService>(),
                    sp.GetRequiredService<IValidator<GetHealthRequest>>()));
    }

    // The brief slices: SaveBrief, GetBrief, MapBriefByName, MapBriefByDispatch, SetBriefAgentId.
    private static void AddBriefs(IServiceCollection services)
    {
        _ = services
            .AddScoped<SaveBriefService>()
            .AddScoped<IValidator<SaveBriefRequest>, SaveBriefValidator>()
            .AddScoped<IService<SaveBriefRequest, SaveBriefResponse>>(static sp =>
                new ValidatingService<SaveBriefRequest, SaveBriefResponse>(
                    sp.GetRequiredService<SaveBriefService>(),
                    sp.GetRequiredService<IValidator<SaveBriefRequest>>()));
        _ = services
            .AddScoped<GetBriefService>()
            .AddScoped<IValidator<GetBriefRequest>, GetBriefValidator>()
            .AddScoped<IService<GetBriefRequest, GetBriefResponse>>(static sp =>
                new ValidatingService<GetBriefRequest, GetBriefResponse>(
                    sp.GetRequiredService<GetBriefService>(),
                    sp.GetRequiredService<IValidator<GetBriefRequest>>()));
        _ = services
            .AddScoped<MapBriefByNameService>()
            .AddScoped<IValidator<MapBriefByNameRequest>, MapBriefByNameValidator>()
            .AddScoped<IService<MapBriefByNameRequest, MapBriefByNameResponse>>(static sp =>
                new ValidatingService<MapBriefByNameRequest, MapBriefByNameResponse>(
                    sp.GetRequiredService<MapBriefByNameService>(),
                    sp.GetRequiredService<IValidator<MapBriefByNameRequest>>()));
        _ = services
            .AddScoped<MapBriefByDispatchService>()
            .AddScoped<IValidator<MapBriefByDispatchRequest>, MapBriefByDispatchValidator>()
            .AddScoped<IService<MapBriefByDispatchRequest, MapBriefByDispatchResponse>>(static sp =>
                new ValidatingService<MapBriefByDispatchRequest, MapBriefByDispatchResponse>(
                    sp.GetRequiredService<MapBriefByDispatchService>(),
                    sp.GetRequiredService<IValidator<MapBriefByDispatchRequest>>()));
        _ = services
            .AddScoped<SetBriefAgentIdService>()
            .AddScoped<IValidator<SetBriefAgentIdRequest>, SetBriefAgentIdValidator>()
            .AddScoped<IService<SetBriefAgentIdRequest, SetBriefAgentIdResponse>>(static sp =>
                new ValidatingService<SetBriefAgentIdRequest, SetBriefAgentIdResponse>(
                    sp.GetRequiredService<SetBriefAgentIdService>(),
                    sp.GetRequiredService<IValidator<SetBriefAgentIdRequest>>()));
    }

    // The verbs the cutover builds on: SortPrompt and SetPromptSort (unit 6.1), CheckTraits (unit 6.2).
    private static void AddCutoverVerbs(IServiceCollection services)
    {
        _ = services
            .AddScoped<SortPromptService>()
            .AddScoped<IValidator<SortPromptRequest>, SortPromptValidator>()
            .AddScoped<IService<SortPromptRequest, SortPromptResponse>>(static sp =>
                new ValidatingService<SortPromptRequest, SortPromptResponse>(
                    sp.GetRequiredService<SortPromptService>(),
                    sp.GetRequiredService<IValidator<SortPromptRequest>>()));
        _ = services
            .AddScoped<SetPromptSortService>()
            .AddScoped<IValidator<SetPromptSortRequest>, SetPromptSortValidator>()
            .AddScoped<IService<SetPromptSortRequest, SetPromptSortResponse>>(static sp =>
                new ValidatingService<SetPromptSortRequest, SetPromptSortResponse>(
                    sp.GetRequiredService<SetPromptSortService>(),
                    sp.GetRequiredService<IValidator<SetPromptSortRequest>>()));
        _ = services
            .AddScoped<CheckTraitsService>()
            .AddScoped<IValidator<CheckTraitsRequest>, CheckTraitsValidator>()
            .AddScoped<IService<CheckTraitsRequest, CheckTraitsResponse>>(static sp =>
                new ValidatingService<CheckTraitsRequest, CheckTraitsResponse>(
                    sp.GetRequiredService<CheckTraitsService>(),
                    sp.GetRequiredService<IValidator<CheckTraitsRequest>>()));
    }
}
