using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Decisions;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Requests;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ListDecisions;

/// <summary>Maps a decisions-view row to the <see cref="DecisionView"/> the API serves (U3-API-3, -6, -7, -8): options and CLI lines come from DecisionKinds through CliForm.Fill, notices from EmptyText.</summary>
internal static class DecisionViews
{
    private static readonly ActionId _decide = ActionId.From("Decide");

    /// <summary>The view of <paramref name="row"/>.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The view, or SND-CLI-003 when a CLI line can't be built.</returns>
    public static Result<DecisionView> From(HumanDecisionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var form = CliForm.Parse("sindri decide <decisionId> <choice>").Bind(parsed => parsed.Fill("decisionId", row.Id));
        if (form.IsError)
        {
            return form.Error;
        }

        var options = new List<OptionView>();
        foreach (var option in DecisionKinds.Options(row.Kind))
        {
            var cli = form.Value.Fill("choice", option.Id);
            if (cli.IsError)
            {
                return cli.Error;
            }

            options.Add(new OptionView(option.Id, option.Label, option.RejectLike, cli.Value.Text));
        }

        var recommended = DecisionKinds.Recommended(row.Kind);
        var pending = row.State == DecisionState.Pending;
        var voided = row.State == DecisionState.Voided;
        var question = $"Accept this {RequestKindWords.Of(row.RequestKind)} request?\n{row.Text}";
        return new DecisionView(row.Id, row.Kind, ActionClass.Decision, DecisionLabels.For(row.Kind, row.RequestKind), row.Item, row.ItemVersion, row.Repo,
            question, options, recommended, row.State, row.FiledBy, row.FiledAt, row.DecidedBy, row.DecidedAt, row.Choice, row.Note,
            RecordedOnly: !voided, Notice: voided ? EmptyText.Voided() : EmptyText.RecordedOnly(),
            pending ? [_decide] : [], options.First(o => o.Id == recommended).Cli, row.Cleared);
    }
}
