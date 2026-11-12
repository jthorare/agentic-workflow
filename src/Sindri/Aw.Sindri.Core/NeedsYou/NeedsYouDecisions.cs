using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Decisions;
using Aw.Sindri.Core.Requests;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.NeedsYou;

/// <summary>Builds the <see cref="NeedsYouDecision"/> of a decisions-view row: the one place the question, the options and their command lines are made, so the needs-you list and the decision list say the same words (issue #2789).</summary>
public static class NeedsYouDecisions
{
    /// <summary>The question of <paramref name="row"/>, as the decision list words it.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The question.</returns>
    public static string Question(HumanDecisionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return $"Accept this {RequestKindWords.Of(row.RequestKind)} request?\n{row.Text}";
    }

    /// <summary>The decision of <paramref name="row"/>.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The decision, or SND-CLI-003 when a command line can't be built.</returns>
    public static Result<NeedsYouDecision> From(HumanDecisionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var form = CliForm.Parse("sindri decide <decisionId> <choice>").Bind(parsed => parsed.Fill("decisionId", row.Id));
        if (form.IsError)
        {
            return form.Error;
        }

        var options = new List<NeedsYouDecisionOption>();
        foreach (var option in DecisionKinds.Options(row.Kind))
        {
            var cli = form.Value.Fill("choice", option.Id);
            if (cli.IsError)
            {
                return cli.Error;
            }

            options.Add(new NeedsYouDecisionOption(option.Id, option.Label, cli.Value.Text, option.RejectLike));
        }

        return new NeedsYouDecision(row.Id, Question(row), row.FiledBy, DecisionKinds.Recommended(row.Kind), options);
    }
}
