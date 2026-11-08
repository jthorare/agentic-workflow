using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Microsoft.Extensions.Time.Testing;
using StateGlyph = Aw.Sindri.Ui.Api.Workflow.StateGlyph;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>One row of the Workflows list (screens §4.2, U8 T1): every cell is a served field mapped to words; nothing is worked out from the recipe.</summary>
[Lane(TestLane.Unit)]
public sealed class InstanceRowViewModelTests
{
    private static InstanceRowViewModel Row(InstanceSummary row) => new(row, new FakeClipboard(), new FakeTimeProvider(A.Noon));

    private static IReadOnlyList<InstanceSummary> Open => FixtureInstances.List("open").Instances;

    private static InstanceSummary Where(Func<InstanceSummary, bool> match) => Open.First(match);

    private static string Mark(StateGlyph glyph) => glyph switch
    {
        StateGlyph.Done => Labels.MarkDone,
        StateGlyph.Active => Labels.MarkActive,
        StateGlyph.Pending => Labels.MarkPending,
        _ => throw new ArgumentOutOfRangeException(nameof(glyph), glyph, message: null),
    };

    [Fact]
    public void Subject_cell_is_the_served_label() => Assert.All(Open, served => Assert.Equal(served.Subject.Label, Row(served).Subject));

    [Fact]
    public void Status_is_the_served_chip_mapped_to_the_one_run_status() =>
        Assert.All(Open, served => Assert.Equal(RunStatus.From(served.Chip), Row(served).Status));

    [Fact]
    public void Inputs_show_check_or_cross_per_declared_input()
    {
        Assert.All(Open, served =>
        {
            var cells = Row(served).Inputs;
            Expect.Sequence(served.Inputs.Select(i => i.Label), cells.Select(c => c.Label));
            Expect.Sequence(served.Inputs.Select(i => i.Present ? Labels.MarkInputPresent : Labels.MarkInputMissing), cells.Select(c => c.Mark));
            Assert.All(cells.Zip(served.Inputs), p => Assert.Contains(p.Second.Present ? "present" : "missing", p.First.Name, StringComparison.Ordinal));
        });
        var missing = FixtureInstances.List("preview-missing-input").Instances[0];
        Assert.Contains(Row(missing).Inputs, c => !c.Present && c.Mark == Labels.MarkInputMissing);
    }

    [Fact]
    public void A_missing_required_input_blanks_its_stage_as_blocked_dash()
    {
        var served = FixtureInstances.List("preview-missing-input").Instances[0];
        Assert.Contains(served.Inputs, i => i is { Required: true, Present: false });

        var stages = Row(served).Stages;

        var blocked = Assert.Single(stages, s => s.IsBlocked);
        Assert.Equal((served.State, Labels.BlockedMark, true), (blocked.State, blocked.Glyph, blocked.IsCurrent));
        Assert.StartsWith(Labels.BlockedMark, blocked.Text, StringComparison.Ordinal);
        Assert.Contains(Labels.EngineBadge(served.EngineOwned!.Kind, served.EngineOwned.Reason), blocked.Text, StringComparison.Ordinal);
        Assert.All(Open, other => Assert.DoesNotContain(Row(other).Stages, s => s.IsBlocked));
    }

    [Fact]
    public void Stage_cell_shows_glyph_then_round_desk_child_or_progress()
    {
        Assert.All(Open.Where(r => r.EngineOwned is null), served =>
        {
            var stages = Row(served).Stages;
            Expect.Sequence(served.States.Select(s => s.State), stages.Select(s => s.State));
            Assert.All(served.States.Zip(stages), p =>
            {
                var (cell, stage) = p;
                Assert.StartsWith(Mark(cell.Glyph), stage.Text, StringComparison.Ordinal);
                Assert.Equal(cell.Round is { } r ? string.Create(CultureInfo.InvariantCulture, $"r{r.N}/{r.Max}") : string.Empty, Between(stage, "r"), StringComparer.Ordinal);
                Assert.Equal(cell.Desk, stage.Detail.Contains("waiting for you", StringComparison.Ordinal));
                Assert.Equal(cell.Child && cell.Children is null, stage.Detail.Contains("child", StringComparison.Ordinal));
                Assert.Equal(cell.Children is { } c ? string.Create(CultureInfo.InvariantCulture, $"{c.Done}/{c.Total}") : string.Empty, Between(stage, "n"), StringComparer.Ordinal);
            });
            _ = Assert.Single(stages, s => s.IsCurrent);
        });

        // The cells that carry each part exist in the fixture, so the loop above checks them.
        Assert.Contains(Open.SelectMany(r => r.States), s => s.Round is not null);
        Assert.Contains(Open.SelectMany(r => r.States), s => s.Desk);
        Assert.Contains(Open.SelectMany(r => r.States), s => s.Child && s.Children is null);
        Assert.Contains(Open.SelectMany(r => r.States), s => s.Children is not null);
    }

    // The round ("r1/3") or the progress ("4/9") token of a cell's detail; empty when it has none.
    private static string Between(StageCellViewModel stage, string kind) =>
        stage.Detail.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(t => kind == "r" ? t.StartsWith('r') && t.Contains('/', StringComparison.Ordinal) : char.IsDigit(t[0]) && t.Contains('/', StringComparison.Ordinal)) ?? string.Empty;

    [Fact]
    public void Engine_badge_replaces_the_glyph()
    {
        var served = Where(r => r.EngineOwned is { Kind: EngineOwnedKind.Escalated });
        var covered = served.States.Single(s => s.Badge is not null);

        var stage = Row(served).Stages.Single(s => s.State == covered.State);

        Assert.Equal(Labels.EngineBadge(covered.Badge!.Kind, covered.Badge.Reason), stage.Glyph);
        Assert.StartsWith(stage.Glyph, stage.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(Mark(covered.Glyph), stage.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Taken_over_row_shows_WITH_YOU_and_the_badge_on_the_covered_state()
    {
        var served = Where(r => r.EngineOwned is { Kind: EngineOwnedKind.TakenOver });
        var covered = served.States.Single(s => s.Badge is not null);
        var row = Row(served);

        var stage = row.Stages.Single(s => s.State == covered.State);

        Assert.Equal(served.Chip.Word, row.Chip.Word);
        Assert.Equal(served.State, covered.State);
        Assert.Contains(covered.Badge!.Reason, stage.Glyph, StringComparison.Ordinal);
        Assert.DoesNotContain(Mark(covered.Glyph), stage.Text, StringComparison.Ordinal);
        _ = Assert.Single(row.Stages, s => s.Glyph.Contains(covered.Badge.Reason, StringComparison.Ordinal));
    }

    [Fact]
    public void Now_is_the_served_now_verbatim() => Assert.All(Open, served => Assert.Equal(served.Now, Row(served).Now));

    [Fact]
    public void Chip_is_the_served_chip() => Assert.All(Open, served => Assert.Equal(served.Chip, Row(served).Chip));

    [Fact]
    public void The_name_carries_the_workflow_the_subject_the_stage_and_who_is_on_it_but_no_address()
    {
        Assert.All(Open, served =>
        {
            var row = Row(served);
            Assert.Equal(string.Join(", ", new[] { row.Subject, row.DisplayName, served.Chip.Word, served.State, row.Who, row.MissingInputsText }.Where(p => p.Length > 0)), row.AccessibleName);
            Assert.StartsWith(row.Subject, row.AccessibleName, StringComparison.Ordinal);
            Assert.DoesNotContain(served.Address, row.AccessibleName, StringComparison.Ordinal);
            Assert.Equal($"Open {row.Subject}, {row.DisplayName}", row.OpenName);
            Assert.DoesNotContain(served.Address, row.OpenName, StringComparison.Ordinal);
            Assert.DoesNotContain(served.Address, row.Compact, StringComparison.Ordinal);
            Assert.Equal($"{row.DisplayName} · {served.State}", row.Compact);
        });
    }

    [Fact]
    public void The_current_cell_says_current_and_the_others_do_not()
    {
        Assert.All(Open, served =>
        {
            var row = Row(served);
            var current = row.Stages.Single(s => s.State == served.State);
            Assert.EndsWith(", " + Labels.StageCurrent, current.Name, StringComparison.Ordinal);
            Assert.All(row.Stages.Where(s => s.State != served.State), s => Assert.DoesNotContain(Labels.StageCurrent, s.Name, StringComparison.Ordinal));
        });
    }

    [Fact]
    public void The_display_name_is_the_served_one_and_otherwise_the_recipe_id_made_readable()
    {
        var served = Open[0];

        var plain = Row(served with { Recipe = served.Recipe with { Id = "workflow.bug-fix", DisplayName = null } });
        var named = Row(served with { Recipe = served.Recipe with { Id = "workflow.bug-fix", DisplayName = "Find, fix and prove a bug" } });

        Assert.Equal(("Bug Fix", "Find, fix and prove a bug"), (plain.DisplayName, named.DisplayName));
        Assert.Equal("workflow.bug-fix", named.Recipe);   // the id stays available for Details and the filter
    }

    [Fact]
    public void Who_is_on_it_is_said_in_plain_words()
    {
        var who = Open.Select(served => (Served: served, Row(served).Who)).ToList();

        Assert.All(who.Where(w => w.Served.EngineOwned is { Kind: EngineOwnedKind.TakenOver }), w => Assert.Equal("You took it over", w.Who));
        Assert.All(who.Where(w => w.Served.EngineOwned is { Kind: EngineOwnedKind.Escalated or EngineOwnedKind.Parked }), w => Assert.Equal("Paused: needs a decision", w.Who));
        Assert.All(who.Where(w => w.Served.EngineOwned is null && (w.Served.Answer is not null || InstanceRules.CompletesAtDesk(w.Served))), w => Assert.Equal("Waiting for you", w.Who));
        Assert.All(who.Where(w => w.Served.EngineOwned is null && w.Served.Answer is null && !InstanceRules.CompletesAtDesk(w.Served)), w => Assert.Equal("In progress", w.Who));
        Assert.Contains(who, w => w.Who == "Paused: needs a decision");
    }

    [Fact]
    public void Details_are_closed_until_the_row_opens_them()
    {
        var row = Row(Open[0]);
        Assert.False(row.IsDetailsOpen);

        row.ToggleDetails();
        Assert.True(row.IsDetailsOpen);

        row.ToggleDetails();
        Assert.False(row.IsDetailsOpen);
    }

    private static InstanceSummary Finished(string word, ChipTone tone)
    {
        var served = Open[0];
        return served with
        {
            EngineOwned = null,
            Answer = null,
            Chip = new StateChip(word, tone),
            States = [.. served.States.Select(s => string.Equals(s.State, served.State, StringComparison.Ordinal) ? s with { Terminal = true } : s)],
        };
    }

    [Fact]
    public void A_finished_run_names_nobody_and_the_chip_word_is_in_its_name()
    {
        var failed = Row(Finished("FAILED", ChipTone.Fail));

        Assert.Equal(string.Empty, failed.Who);
        Assert.Contains("FAILED", failed.AccessibleName, StringComparison.Ordinal);
        Assert.DoesNotContain("In progress", failed.AccessibleName, StringComparison.Ordinal);
        Assert.False(failed.AccessibleName.EndsWith(", ", StringComparison.Ordinal));
    }

    [Fact]
    public void The_name_counts_the_missing_inputs_when_there_are_any()
    {
        var served = FixtureInstances.List("preview-missing-input").Instances[0];
        var row = Row(served);

        Assert.True(row.HasMissingInputs);
        Assert.EndsWith(", " + row.MissingInputsText, row.AccessibleName, StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_inputs_are_counted_up_front_and_only_when_any_is_missing()
    {
        var served = FixtureInstances.List("preview-missing-input").Instances[0];
        var missing = served.Inputs.Count(i => !i.Present);
        Assert.True(missing > 0);

        Assert.Equal(missing == 1 ? "1 input missing" : string.Create(CultureInfo.InvariantCulture, $"{missing} inputs missing"), Row(served).MissingInputsText);
        Assert.True(Row(served).HasMissingInputs);

        var none = Row(served with { Inputs = [.. served.Inputs.Select(i => i with { Present = true })] });
        Assert.Equal((string.Empty, false), (none.MissingInputsText, none.HasMissingInputs));
    }
}
