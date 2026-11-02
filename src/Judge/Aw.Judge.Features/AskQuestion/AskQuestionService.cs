using Aw.Judge.Core.Config;
using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.AskQuestion;

/// <summary>
/// Asks one question: the catalog builds the provider lineup from the config's providers block and the allowlist, the
/// question's config entry (enabled, threshold, providers) narrows it, then the evaluator decides or escalates. A config
/// that can't be read runs on the defaults (a question fails open), like an invalid one.
/// </summary>
internal sealed class AskQuestionService(QuestionEvaluator evaluator, IJudgeConfigStore configs, IProviderCatalog catalog, RulesProvider rules)
    : IService<AskQuestionRequest, AskQuestionResponse>
{
    public async ValueTask<Result<AskQuestionResponse>> Handle(AskQuestionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reading = ConfigReading.From(await configs.Load(cancellationToken).ConfigureAwait(false));
        var (config, warning) = reading.IsSuccess ? (reading.Value.Config, reading.Value.Warning) : (JudgeConfig.Default, reading.Error.Message);
        var lineup = catalog.Lineup(config.Providers, request.AllowedProviders, rules);
        var context = new EvaluationContext
        {
            Providers = lineup.Providers,
            Chain = lineup.Chain,
            Override = QuestionOverride.For(config, request.Question),
            InputCapChars = config.Providers?.InputCapChars,
            SessionId = request.SessionId,
        };
        var outcome = await evaluator.Ask(request.Question, request.Input, context, cancellationToken).ConfigureAwait(false);
        return new AskQuestionResponse(outcome, warning);
    }
}
