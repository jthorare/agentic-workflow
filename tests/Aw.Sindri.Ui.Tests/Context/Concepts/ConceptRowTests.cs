using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context.Concepts;

namespace Aw.Sindri.Ui.Tests.Context.Concepts;

/// <summary>A concept is one line (#3677, decision 1): its name, its file count (the only unit) and when it last changed; "Suggested" reads only when pinned and unpinned concepts are mixed.</summary>
[Lane(TestLane.Unit)]
public sealed class ConceptRowTests
{
    private static readonly MapRebuildView _idle = new(MapRebuildPhases.Idle, Progress: null, RetryAt: null, Stale: false, Failed: null);

    private static ConceptLayerViewModel Layer(IReadOnlyList<ContextConcept> concepts, params PinView[] pins)
    {
        var layer = new ConceptLayerViewModel(_ => { });
        layer.WirePins(_ => { }, () => "api", TimeZoneInfo.Utc);
        layer.Show(AConcepts.Ok(concepts));
        layer.ShowPins(new PinsView(1, "api", "file-h", "v1", pins, _idle));
        return layer;
    }

    private static PinView Pin(string id, string name) => new(id, name, "def of " + name, [], [], FolderRules: 0, "profile", "approved", Change: null);

    [Fact]
    public void A_row_counts_files_only_and_says_when_it_last_changed()
    {
        var concept = AConcepts.Concept("concept:self-scheduling", "Self scheduling", features: 4, records: 3, decisions: 2, code: 40) with { LastChanged = new DateTimeOffset(2026, 11, 13, 9, 0, 0, TimeSpan.Zero) };
        var undated = AConcepts.Concept("concept:billing", "Billing", features: 2, records: 1, decisions: 1, code: 1);
        var layer = Layer([concept, undated]);

        var first = layer.Tiles[0];
        var second = layer.Tiles[1];

        Assert.Equal(("40 files", "changed 13 Nov", "Self scheduling, 40 files. Press Enter to open."), (first.FilesText, first.ChangedText, first.AccessibleName));
        Assert.Equal(("1 file", string.Empty), (second.FilesText, second.ChangedText));
        Assert.Equal("40 files", first.SizeText);
    }

    [Fact]
    public void Suggested_does_not_read_while_nothing_is_pinned()
    {
        var layer = Layer([AConcepts.Concept("concept:auth", "Auth"), AConcepts.Concept("concept:notify", "Notify")]);

        Assert.All(layer.Tiles, t => Assert.Equal(string.Empty, t.MarkText));
        Assert.All(layer.Tiles, t => Assert.True(t.ShowPinThis));
    }

    [Fact]
    public void Suggested_reads_on_the_unpinned_rows_once_some_are_pinned()
    {
        var layer = Layer([AConcepts.Concept("concept:auth", "Auth"), AConcepts.Concept("concept:billing", "Billing", source: "override")], Pin("billing", "Billing"));

        Assert.Equal(["Set by a person", "Suggested"], [.. layer.Tiles.Select(t => t.MarkText)]);
    }

    [Fact]
    public void Suggested_does_not_read_when_every_concept_is_pinned()
    {
        var layer = Layer([AConcepts.Concept("concept:billing", "Billing", source: "override")], Pin("billing", "Billing"));

        Assert.DoesNotContain("Suggested", layer.Tiles.Select(t => t.MarkText), StringComparer.Ordinal);
    }
}
