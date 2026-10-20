using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Controls;
using Served = Aw.Sindri.Ui.Api.Workflow.StateChip;

namespace Aw.Sindri.Ui.Tests.Controls;

/// <summary>The one chip table (fixture.md "State chips"): the API serves word and tone, the app maps tone to a class and shows the word as served.</summary>
[Lane(TestLane.Unit)]
public sealed class StateChipTests
{
    // fixture.md's table, as the screens spec draws it: attn (the one attention colour), fail, ok, neutral. ABSENT (a dependency
    // not found on this host, --ink-subtle) is the System page's chip: it is never served, so it is not in this table, and the
    // chip control has no subtle look yet. It lands with the System page (U12), together with its test.
    private static readonly Dictionary<string, ChipTone> _spec = new(StringComparer.Ordinal)
    {
        ["PARKED"] = ChipTone.Attn,
        ["ESCALATED"] = ChipTone.Attn,
        ["DECISION"] = ChipTone.Attn,
        ["PROFILE"] = ChipTone.Attn,
        ["CHANGED"] = ChipTone.Attn,
        ["ORPHANED"] = ChipTone.Attn,
        ["DEGRADED"] = ChipTone.Attn,
        ["DIDN'T WORK OUT"] = ChipTone.Neutral,
        ["FAILED"] = ChipTone.Fail,
        ["RUNNING"] = ChipTone.Ok,
        ["OK"] = ChipTone.Ok,
        ["WITH YOU"] = ChipTone.Neutral,
        ["QUEUED"] = ChipTone.Neutral,
        ["DONE"] = ChipTone.Neutral,
        ["CANCELLED"] = ChipTone.Neutral,
        ["DRAFT"] = ChipTone.Neutral,
        ["APPROVED"] = ChipTone.Neutral,
        ["DETERMINISTIC"] = ChipTone.Neutral,
        ["MODEL"] = ChipTone.Neutral,
        ["CHILD"] = ChipTone.Neutral,
        ["POLLING"] = ChipTone.Neutral,
    };

    // Every chip the API serves: on the list rows, on the instance and on its child rows, found in the goldens on disk.
    private static IEnumerable<(string Where, Served Chip, string State, bool Terminal)> ServedChips()
    {
        foreach (var row in FixtureInstances.Rows)
        {
            yield return ($"list {row.Address}", row.Chip, row.State, row.States.Single(s => s.State == row.State).Terminal);
        }

        foreach (var (scenario, detail) in FixtureInstances.Details)
        {
            foreach (var child in detail.Children.SelectMany(FixtureInstances.ChildRows))
            {
                yield return ($"{scenario} {child.Address}", child.Chip, child.State, child.Glyph == StateGlyph.Done);
            }
        }
    }

    [Fact]
    public void Every_fixture_word_maps_to_its_class()
    {
        var chips = ServedChips().ToList();
        Assert.Contains(chips, c => c.Chip.Tone == ChipTone.Attn);
        Assert.Contains(chips, c => c.Chip.Tone == ChipTone.Ok);
        Assert.Contains(chips, c => c.Chip.Tone == ChipTone.Neutral);

        foreach (var (where, chip, _, _) in chips)
        {
            Assert.True(_spec.TryGetValue(chip.Word, out var expected), $"{where}: {chip.Word} is not in the fixture's chip table");
            Assert.True(chip.Tone == expected, $"{where}: {chip.Word} is served {chip.Tone}, the table says {expected}");
        }

        string?[] classes = ["attn", "fail", "ok", null];
        Assert.Equal(classes, new[] { ChipTone.Attn, ChipTone.Fail, ChipTone.Ok, ChipTone.Neutral }.Select(t => new ChipView("X", t).ToneClass), StringComparer.Ordinal);
    }

    [Fact]
    public void The_word_is_shown_as_served()
    {
        foreach (var (where, chip, _, _) in ServedChips())
        {
            var view = ChipView.Resolve(chip);

            Assert.True(view is { } v && v.Word == chip.Word && v.Tone == chip.Tone, where);
        }
    }

    // A recipe's state is a stage: plain text beside the chip, never inside it. The chip says how the instance is doing.
    [Fact]
    public void Stage_names_are_never_chips()
    {
        foreach (var (where, chip, state, terminal) in ServedChips().Where(c => !c.Terminal))
        {
            Assert.False(string.Equals(chip.Word, state, StringComparison.OrdinalIgnoreCase), $"{where}: the stage {state} is served as a chip");
        }

        var stages = FixtureInstances.Rows.SelectMany(r => r.States).Where(s => !s.Terminal).Select(s => s.State).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.NotEmpty(stages);
        Assert.DoesNotContain(ServedChips().Where(c => !c.Terminal), c => stages.Contains(c.Chip.Word));
    }

    [Fact]
    public void Served_chip_wins_when_present()
    {
        var served = new Served("PARKED", ChipTone.Attn);
        var drawn = new Served("RUNNING", ChipTone.Ok);

        Assert.Equal(new ChipView("PARKED", ChipTone.Attn), ChipView.Resolve(served, drawn));
        Assert.Equal(new ChipView("RUNNING", ChipTone.Ok), ChipView.Resolve(served: null, drawn));
        Assert.Null(ChipView.Resolve(served: null, fallback: null));
    }
}
