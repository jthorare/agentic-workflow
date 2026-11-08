using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Testing.Views;

namespace Aw.Sindri.Tests.Workflow.Views;

/// <summary>Invariants 4 and 5: the desk runtime's run tokens, and a state with no agent runtime served as a desk task.</summary>
public sealed partial class WorkflowEngineInvariantsTests
{
    [GeneratedRegex(@"--run (?<entry>\d+)\.(?<run>\d+)", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RunOption { get; }

    [Fact, Trait("Lane", "unit")]
    public void A_desk_run_token_is_the_entry_of_its_state_then_the_run_number_and_every_text_names_the_same_one() => Walk(Tokens);

    private static List<string> TokensOf(View view)
    {
        var tokens = new List<string>();
        if (view.Detail?.Current.Desk is { } desk)
        {
            tokens.Add(desk.Run.Value);
        }

        foreach (var status in view.Statuses)
        {
            var texts = status.Commands.Select(c => c.Cli).Append(status.Text);
            tokens.AddRange(texts.SelectMany(t => RunOption.Matches(t)).Select(m => M($"{m.Groups["entry"].Value}.{m.Groups["run"].Value}")));
        }

        return [.. tokens.Distinct(StringComparer.Ordinal)];
    }

    private static List<string> Tokens(View view, RecipeShape shape)
    {
        if (StateOf(view, shape).Order is not { } order)
        {
            return [];
        }

        var looped = view.Cells.Any(c => c.Round is { N: > 1 });
        var tokens = TokensOf(view);
        var failures = tokens
            .Where(t => t != RunToken.For(order, 1).Value && !(looped && int.Parse(t[..t.IndexOf('.', StringComparison.Ordinal)], CultureInfo.InvariantCulture) > order))
            .Select(t => M($"run token {t}: the engine opens '{view.State}' as {RunToken.For(order, 1).Value} (StateEntry; WF 5.3)"))
            .ToList();
        if (tokens.Count > 1)
        {
            failures.Add(M($"different run tokens: {string.Join(", ", tokens)}"));
        }

        return failures;
    }

    [Fact, Trait("Lane", "unit")]
    public void A_state_with_no_agent_runtime_is_served_as_a_desk_task_with_a_complete_command_and_a_decision_chip() => Walk(DeskTasks);

    private static List<string> DeskTasks(View view, RecipeShape shape)
    {
        var state = StateOf(view, shape);
        var failures = new List<string>();
        if (CellOf(view).Desk != state.Desk)
        {
            failures.Add("the cell's desk flag differs from the declaration");
        }

        failures.AddRange(ChildDeskRows(view));
        if (state.RunsAtDesk && view.EngineOwned is null)
        {
            failures.AddRange(DeskView(view));
        }

        return failures;
    }

    private static List<string> ChildDeskRows(View view)
    {
        if (view.Detail is not { } detail)
        {
            return [];
        }

        return [.. detail.Children.SelectMany(group =>
        {
            var child = FixtureRecipes.All.Single(s => s.Id == group.Recipe.Value);
            return RowsOf(group)
                .Where(r => r.Glyph == StateGlyph.Active && r.EngineOwned is null && child.State(r.State).RunsAtDesk && (r.Chip.Word != "DECISION" || r.Agent is not null))
                .Select(r => M($"child {r.Address.Value} at '{r.State}' is a desk task: chip {r.Chip.Word}, agent '{r.Agent}'"));
        })];
    }

    private static List<string> DeskView(View view)
    {
        var failures = new List<string>();
        if (view.Chip is { } chip && chip.Word != "DECISION")
        {
            failures.Add(M($"chip {chip.Word}, a desk task is DECISION"));
        }

        if (view.Detail is { } detail && detail.Current.Desk is null)
        {
            failures.Add("no DeskForm");
        }

        failures.AddRange(ShippedBlockOutputs(view));
        foreach (var status in view.Statuses)
        {
            if (status.Header.Detail != "desk task")
            {
                failures.Add(M($"status detail '{status.Header.Detail}', not 'desk task'"));
            }

            if (status.Commands.Count == 0 || status.Commands[0].Verb != StatusVerb.Complete)
            {
                failures.Add(M($"status commands start with '{(status.Commands.Count == 0 ? "nothing" : StatusVerbNames.Of(status.Commands[0].Verb))}', not complete"));
            }
        }

        return failures;
    }

    // A state whose Block is in the shipped catalog reports what the descriptor requires on its first outcome: the DeskForm's outputs and
    // the complete command's --output flags both (SCR5-1; the engine refuses a report with a required output missing).
    private static List<string> ShippedBlockOutputs(View view)
    {
        if (view.Detail?.Current is not { Step: { } step, Desk: { } desk } current || _catalog.Find(BlockId.From(step.Ref)) is not { } block || block.Outcomes.Count == 0)
        {
            return [];
        }

        var required = block.Outputs.Where(o => StepOutputs.CallerWritesOn(o, block.Outcomes[0])).Select(o => o.Fact.Value).ToList();
        var failures = new List<string>();
        if (!required.SequenceEqual(desk.Outputs.Where(o => o.Required).Select(o => o.Key), StringComparer.Ordinal))
        {
            failures.Add(M($"DeskForm outputs [{string.Join(", ", desk.Outputs.Select(o => o.Key))}], the shipped {step.Ref} requires [{string.Join(", ", required)}] on '{block.Outcomes[0].Value}'"));
        }

        foreach (var cli in current.Status.Commands.Where(c => c.Verb == StatusVerb.Complete).Select(c => c.Cli))
        {
            failures.AddRange(required.Where(key => !cli.Contains($"--output {key}=", StringComparison.Ordinal)).Select(key => M($"the complete command lacks --output {key}=<file>: {cli}")));
        }

        return failures;
    }
}
