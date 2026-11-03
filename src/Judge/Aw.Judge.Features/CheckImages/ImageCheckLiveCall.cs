using System.Text.Json.Nodes;
using Aw.Judge.Core.Config;
using Aw.Judge.Core.Decisions;
using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Images;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;
using Aw.Judge.Core.Questions;
using ProviderNames = Aw.Judge.Core.Providers.ProviderNames;

namespace Aw.Judge.Features.CheckImages;

/// <summary>
/// One real visual-critique call through the chain with Jev forced to take images: its line, and, when another provider decided, why Jev
/// was passed over.
/// </summary>
internal sealed class ImageCheckLiveCall(
    QuestionEvaluator evaluator, IProviderCatalog catalog, RulesProvider rules, IImageWorkspaces workspaces, IDecisionLog log)
{
    public async ValueTask<IReadOnlyList<string>> Run(
        IReadOnlyList<ImageCase> set, IReadOnlyList<ProviderName>? allowed, ProvidersConfig? providers, IModelProvider jev, CancellationToken cancellationToken)
    {
        var lineup = catalog.Lineup(providers, allowed, rules);
        var context = new EvaluationContext
        {
            Providers = [.. lineup.Providers.Select(p => p.Name == ProviderName.Jev ? jev : p)],
            Chain = lineup.Chain,
            ImageReader = new ImageCheckCases(set),
            ImageWorkspaces = workspaces,
        };
        var first = set.FirstOrDefault(c => string.Equals(c.Expected, "looks-right", StringComparison.Ordinal)) ?? set[0];
        var input = new JsonObject { ["afterScreenshot"] = first.Name + ".png", ["evidenceDir"] = ImageCheckCases.Root };
        var outcome = await evaluator.Evaluate(VisualCritiqueQuestion.Definition, input.ToJsonString(), context, cancellationToken).ConfigureAwait(false);
        if (outcome is not DecidedOutcome decided)
        {
            return ["live call:  no decision (the chain escalated)"];
        }

        List<string> lines = [$"live call:  {decided.Decision}  provider {ProviderNames.Name(decided.Model)}  (judge why {decided.Id.Value})"];
        if (decided.Model != ProviderName.Jev && await log.Find(decided.Id, cancellationToken).ConfigureAwait(false) is { IsSuccess: true } row)
        {
            lines.AddRange(JevSkipNotes.For(row.Value));
        }

        return lines;
    }
}
