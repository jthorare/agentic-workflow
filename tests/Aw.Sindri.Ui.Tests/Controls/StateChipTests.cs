using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels.Controls;

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
        ["DIDN'T WORK OUT"] = ChipTone.Attn,
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

    // Every chip of the table, as the API serves it: a word and its tone.
    private static IEnumerable<(string Where, StateChip Chip, string State, bool Terminal)> ServedChips() =>
        _spec.Select(entry => ($"table {entry.Key}", new StateChip(entry.Key, entry.Value), string.Empty, true));

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

    [Fact]
    public void Served_chip_wins_when_present()
    {
        var served = new StateChip("PARKED", ChipTone.Attn);
        var drawn = new StateChip("RUNNING", ChipTone.Ok);

        Assert.Equal(new ChipView("PARKED", ChipTone.Attn), ChipView.Resolve(served, drawn));
        Assert.Equal(new ChipView("RUNNING", ChipTone.Ok), ChipView.Resolve(served: null, drawn));
        Assert.Null(ChipView.Resolve(served: null, fallback: null));
    }
}
