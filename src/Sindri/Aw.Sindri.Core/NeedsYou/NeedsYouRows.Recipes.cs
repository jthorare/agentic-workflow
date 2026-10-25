using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.OpenTargets;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.ApproveAll;

namespace Aw.Sindri.Core.NeedsYou;

public static partial class NeedsYouRows
{
    private static readonly ActionId _approveAllRecipes = ActionId.From(SetupActions.ApproveAllRecipes);

    /// <summary>
    /// The rows of the approve-all plan (setup-once spec §5.3, §7.3), read-time and not folded from events: 'setup:recipes' while the hold has workflows to approve, and a
    /// 'recipe:&lt;id&gt;' row for each previously approved workflow that is approved on its own. A workflow in the hold has no row of its own: the hold covers it.
    /// </summary>
    /// <param name="plan">The plan now.</param>
    /// <param name="since">When the list was read (the plan has no history of its own).</param>
    /// <param name="scrubber">Scrubs the text.</param>
    /// <returns>The rows, none when nothing waits; an error when a CLI line can't be built.</returns>
    public static Result<IReadOnlyList<NeedsYouRow>> ForRecipes(ShippedRecipePlan plan, DateTimeOffset since, ITextScrubber scrubber)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(scrubber);
        List<NeedsYouRow> rows = [];
        if (plan.Recipes.Count > 0)
        {
            var hold = HoldRow(plan.Recipes.Count, since);
            if (hold.IsError)
            {
                return hold.Error;
            }

            rows.Add(hold.Value);
        }

        foreach (var own in plan.OwnApproval)
        {
            var row = OwnRow(own, since, scrubber);
            if (row.IsError)
            {
                return row.Error;
            }

            rows.Add(row.Value);
        }

        return Results.Success<IReadOnlyList<NeedsYouRow>>(rows);
    }

    private static Result<NeedsYouRow> HoldRow(int count, DateTimeOffset since)
    {
        var cli = CliForm.Parse("sindri recipe approve-all");
        if (cli.IsError)
        {
            return cli.Error;
        }

        var words = count.ToString(CultureInfo.InvariantCulture);
        return new NeedsYouRow(
            NeedsYouRowId.From("setup:recipes"),
            NeedsYouKind.Setup,
            "setup · workflows",
            new NeedsYouSubject(SubjectKind.Setup, SubjectId.From("recipes")),
            Item: null,
            $"Approve all new workflows ({words})",
            count == 1 ? "1 workflow waits for your approval." : $"{words} workflows wait for your approval.",
            Repo: null,
            Urgency.Pulse,
            since,
            RecordedOnly: false,
            Notice: null,
            AnswerKind: null,
            _approveAllRecipes,
            [_approveAllRecipes],
            cli.Value.Text,
            new NeedsYouOpen("setup", "Open setup"));
    }

    private static Result<NeedsYouRow> OwnRow(OwnApprovalRow own, DateTimeOffset since, ITextScrubber scrubber)
    {
        // the waiting version's own id@hash: the CLI takes <file|id@hash>, never a bare id (#983)
        var cli = own.Hash is { } hash ? Cli("sindri recipe approve <recipe>@<hash>", "recipe", own.Id).Bind(form => form.Fill("hash", hash)) : Cli("sindri recipe approve <recipe>", "recipe", own.Id);
        if (cli.IsError)
        {
            return cli.Error;
        }

        return new NeedsYouRow(
            NeedsYouRowId.From("recipe:" + own.Id.Value),
            NeedsYouKind.Recipe,
            "workflow · changed",
            new NeedsYouSubject(SubjectKind.Recipe, SubjectId.From(own.Id.Value)),
            Item: null,
            SafeText.Clean(scrubber, "Approve a changed workflow: " + own.DisplayName, TitleMax),
            SafeText.Clean(scrubber, own.Sentence, ReasonMax),
            Repo: null,
            Urgency.Pulse,
            since,
            RecordedOnly: false,
            Notice: null,
            AnswerKind: null,
            _approveRecipe,
            [_approveRecipe],
            cli.Value.Text,
            new NeedsYouOpen("recipe/" + own.Id.Value, "Review the workflow change"));
    }
}
