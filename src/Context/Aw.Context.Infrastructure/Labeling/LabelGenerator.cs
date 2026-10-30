using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Models;
using Aw.Context.Core.Trust;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Labeling;

/// <summary>
/// The generate step of a labeling pass (spec §6.1 step 1, §6.2): clusters in packs sized by their real scrubbed text against the smallest prompt budget of the labeling route, asking for a number of candidate labels per cluster.
/// A model error after some packs answered keeps their labels and marks the pass incomplete; before any answer it is the error, or a note naming the fix when the model is simply not there.
/// </summary>
/// <param name="model">The model port.</param>
/// <param name="egress">Scrubs every path (and, with <paramref name="scrubLabels"/>, every label).</param>
/// <param name="budget">The prompt budget a pack is sized for.</param>
/// <param name="candidates">Labels asked for per cluster.</param>
/// <param name="scrubLabels">True when the labels go on to Jev, so a secret a model echoes into one is scrubbed too.</param>
internal sealed class LabelGenerator(IModelPort model, IEgressScrubber egress, PromptBudget budget, int candidates, bool scrubLabels)
{
    private const int AnswerTokensPerLabel = 24;
    private static readonly TimeSpan _callBudget = TimeSpan.FromMinutes(2);

    internal async ValueTask<Result<Generated>> Generate(IReadOnlyList<FileCluster> clusters, int maxTokens, CancellationToken cancellationToken)
    {
        var header = LabelPrompt.Header(candidates);
        var lines = clusters.OrderBy(static c => c.Id.Value, StringComparer.Ordinal).Select(c => (Cluster: c, Line: LabelPrompt.Line(c, egress)));
        var plan = LabelPacks.First(lines, static l => l.Line.Length, budget.Chars, budget.MaxItems, header.Length);
        var named = new List<(FileCluster, List<string>)>();
        var spent = 0;
        foreach (var pack in plan.Packs)
        {
            var prompt = header + string.Concat(pack.Select(static l => l.Line));
            var answerCap = (pack.Count * candidates * AnswerTokensPerLabel) + 16;
            if (spent + Tokens(prompt) + answerCap > maxTokens)
            {
                return ContextErrors.LabelingUnavailable.With(string.Create(CultureInfo.InvariantCulture,
                    $"the labeling pass would pass its {maxTokens} token cap after {spent} (fix: raise 'context.labeling.maxTokens' or label fewer clusters)"));
            }

            var request = new ModelRequest(ModelPurpose.Labeling, prompt, answerCap, _callBudget) { MaxPromptChars = budget.Chars };
            var answer = await model.Ask(request, cancellationToken).ConfigureAwait(false);
            if (answer.IsError)
            {
                return Failed(answer.Error, named, spent);
            }

            spent += answer.Value.InputTokens + answer.Value.OutputTokens;
            named.AddRange(LabelPrompt.Parse(answer.Value.Text, [.. pack.Select(static l => l.Cluster)], candidates, scrubLabels ? egress : null));
        }

        return new Generated(named, Complete: true, Unavailable: null, spent);
    }

    internal static int Tokens(string text) => (text.Length + 3) / 4;

    private static Result<Generated> Failed(Error error, List<(FileCluster, List<string>)> named, int spent)
    {
        // The packs already answered are kept (and paid for): the pass is incomplete, and the build says 'degraded: labels'.
        if (named.Count > 0 || spent > 0)
        {
            return new Generated(named, Complete: false, Unavailable: null, spent);
        }

        // No model answered at all: the build goes on with file-derived names and says how to set one up (SND-CONTEXT-042).
        return error.Code == ContextErrors.SourceUnavailable.Code || error.Code == ContextErrors.LabelingUnavailable.Code
            ? new Generated([], Complete: false, $"no model answered: {error.Message} (fix: {LabelPass.NoModelFix})", spent)
            : error;
    }
}
