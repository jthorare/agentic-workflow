using Aw.Judge.Features.Adjudicate;
using Aw.Judge.Features.AdjudicateSortItems;
using Aw.Judge.Features.EvaluateQuestion;
using Aw.Judge.Features.EvaluateSort;
using Aw.Judge.Features.ExplainPromptSort;
using Aw.Judge.Features.GetLabelStatus;
using Aw.Judge.Features.ImportLabels;
using Aw.Judge.Features.ImportSortItems;
using Aw.Judge.Features.ImportTurns;
using Aw.Judge.Features.LabelOutcomes;
using Aw.Judge.Features.LabelSortOutcomes;
using Aw.Judge.Features.PromoteScaffolds;
using Aw.Judge.Features.SetLabel;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Features;

/// <summary>
/// The by-hand verbs of the cutover's last two units: unit 6.3's label, adjudicate and eval verbs, and unit 6.4's prompt-sort
/// eval side as compiling stubs (the by-hand part of <see cref="AddJudgeFeatures"/>). Each is a service behind its validator, registered like every other slice.
/// </summary>
public static partial class JudgeFeaturesServiceCollectionExtensions
{
    private static void AddByHandVerbs(IServiceCollection services)
    {
        AddLabelling(services);
        AddScoring(services);
        AddSortLabelling(services);
        AddSortScoring(services);
    }

    // The label verbs: import, outcomes, set and status (unit 6.3).
    private static void AddLabelling(IServiceCollection services)
    {
        _ = services
            .AddScoped<ImportLabelsService>()
            .AddScoped<IValidator<ImportLabelsRequest>, ImportLabelsValidator>()
            .AddScoped<IService<ImportLabelsRequest, ImportLabelsResponse>>(static sp =>
                new ValidatingService<ImportLabelsRequest, ImportLabelsResponse>(
                    sp.GetRequiredService<ImportLabelsService>(),
                    sp.GetRequiredService<IValidator<ImportLabelsRequest>>()));
        _ = services
            .AddScoped<LabelOutcomesService>()
            .AddScoped<IValidator<LabelOutcomesRequest>, LabelOutcomesValidator>()
            .AddScoped<IService<LabelOutcomesRequest, LabelOutcomesResponse>>(static sp =>
                new ValidatingService<LabelOutcomesRequest, LabelOutcomesResponse>(
                    sp.GetRequiredService<LabelOutcomesService>(),
                    sp.GetRequiredService<IValidator<LabelOutcomesRequest>>()));
        _ = services
            .AddScoped<SetLabelService>()
            .AddScoped<IValidator<SetLabelRequest>, SetLabelValidator>()
            .AddScoped<IService<SetLabelRequest, SetLabelResponse>>(static sp =>
                new ValidatingService<SetLabelRequest, SetLabelResponse>(
                    sp.GetRequiredService<SetLabelService>(),
                    sp.GetRequiredService<IValidator<SetLabelRequest>>()));
        _ = services
            .AddScoped<GetLabelStatusService>()
            .AddScoped<IValidator<GetLabelStatusRequest>, GetLabelStatusValidator>()
            .AddScoped<IService<GetLabelStatusRequest, GetLabelStatusResponse>>(static sp =>
                new ValidatingService<GetLabelStatusRequest, GetLabelStatusResponse>(
                    sp.GetRequiredService<GetLabelStatusService>(),
                    sp.GetRequiredService<IValidator<GetLabelStatusRequest>>()));
    }

    // adjudicate, eval and eval import-turns (unit 6.3).
    private static void AddScoring(IServiceCollection services)
    {
        _ = services
            .AddScoped<AdjudicateService>()
            .AddScoped<IValidator<AdjudicateRequest>, AdjudicateValidator>()
            .AddScoped<IService<AdjudicateRequest, AdjudicateResponse>>(static sp =>
                new ValidatingService<AdjudicateRequest, AdjudicateResponse>(
                    sp.GetRequiredService<AdjudicateService>(),
                    sp.GetRequiredService<IValidator<AdjudicateRequest>>()));
        _ = services
            .AddScoped<EvaluateQuestionService>()
            .AddScoped<IValidator<EvaluateQuestionRequest>, EvaluateQuestionValidator>()
            .AddScoped<IService<EvaluateQuestionRequest, EvaluateQuestionResponse>>(static sp =>
                new ValidatingService<EvaluateQuestionRequest, EvaluateQuestionResponse>(
                    sp.GetRequiredService<EvaluateQuestionService>(),
                    sp.GetRequiredService<IValidator<EvaluateQuestionRequest>>()));
        _ = services
            .AddScoped<ImportTurnsService>()
            .AddScoped<IValidator<ImportTurnsRequest>, ImportTurnsValidator>()
            .AddScoped<IService<ImportTurnsRequest, ImportTurnsResponse>>(static sp =>
                new ValidatingService<ImportTurnsRequest, ImportTurnsResponse>(
                    sp.GetRequiredService<ImportTurnsService>(),
                    sp.GetRequiredService<IValidator<ImportTurnsRequest>>()));
    }

    // prompt-sort why, import and outcomes (unit 6.4's compiling stubs).
    private static void AddSortLabelling(IServiceCollection services)
    {
        _ = services
            .AddScoped<ExplainPromptSortService>()
            .AddScoped<IValidator<ExplainPromptSortRequest>, ExplainPromptSortValidator>()
            .AddScoped<IService<ExplainPromptSortRequest, ExplainPromptSortResponse>>(static sp =>
                new ValidatingService<ExplainPromptSortRequest, ExplainPromptSortResponse>(
                    sp.GetRequiredService<ExplainPromptSortService>(),
                    sp.GetRequiredService<IValidator<ExplainPromptSortRequest>>()));
        _ = services
            .AddScoped<ImportSortItemsService>()
            .AddScoped<IValidator<ImportSortItemsRequest>, ImportSortItemsValidator>()
            .AddScoped<IService<ImportSortItemsRequest, ImportSortItemsResponse>>(static sp =>
                new ValidatingService<ImportSortItemsRequest, ImportSortItemsResponse>(
                    sp.GetRequiredService<ImportSortItemsService>(),
                    sp.GetRequiredService<IValidator<ImportSortItemsRequest>>()));
        _ = services
            .AddScoped<LabelSortOutcomesService>()
            .AddScoped<IValidator<LabelSortOutcomesRequest>, LabelSortOutcomesValidator>()
            .AddScoped<IService<LabelSortOutcomesRequest, LabelSortOutcomesResponse>>(static sp =>
                new ValidatingService<LabelSortOutcomesRequest, LabelSortOutcomesResponse>(
                    sp.GetRequiredService<LabelSortOutcomesService>(),
                    sp.GetRequiredService<IValidator<LabelSortOutcomesRequest>>()));
    }

    // prompt-sort adjudicate, eval and promote (unit 6.4's compiling stubs).
    private static void AddSortScoring(IServiceCollection services)
    {
        _ = services
            .AddScoped<AdjudicateSortItemsService>()
            .AddScoped<IValidator<AdjudicateSortItemsRequest>, AdjudicateSortItemsValidator>()
            .AddScoped<IService<AdjudicateSortItemsRequest, AdjudicateSortItemsResponse>>(static sp =>
                new ValidatingService<AdjudicateSortItemsRequest, AdjudicateSortItemsResponse>(
                    sp.GetRequiredService<AdjudicateSortItemsService>(),
                    sp.GetRequiredService<IValidator<AdjudicateSortItemsRequest>>()));
        _ = services
            .AddScoped<EvaluateSortService>()
            .AddScoped<IValidator<EvaluateSortRequest>, EvaluateSortValidator>()
            .AddScoped<IService<EvaluateSortRequest, EvaluateSortResponse>>(static sp =>
                new ValidatingService<EvaluateSortRequest, EvaluateSortResponse>(
                    sp.GetRequiredService<EvaluateSortService>(),
                    sp.GetRequiredService<IValidator<EvaluateSortRequest>>()));
        _ = services
            .AddScoped<PromoteScaffoldsService>()
            .AddScoped<IValidator<PromoteScaffoldsRequest>, PromoteScaffoldsValidator>()
            .AddScoped<IService<PromoteScaffoldsRequest, PromoteScaffoldsResponse>>(static sp =>
                new ValidatingService<PromoteScaffoldsRequest, PromoteScaffoldsResponse>(
                    sp.GetRequiredService<PromoteScaffoldsService>(),
                    sp.GetRequiredService<IValidator<PromoteScaffoldsRequest>>()));
    }
}
