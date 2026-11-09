using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Todo;

/// <summary>The Obsidian todo note card while the server has no note import route: nothing on the card imports (todo spec §4.5; the flag is TodoNoteImport.Served).</summary>
[Lane(TestLane.Unit)]
public sealed class TodoNoteCardImportFlagTests
{
    private const string Line = "Import comes with note sync. Notes with open tasks can't sync yet.";

    private readonly TodoNoteCardFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Not_served_a_note_with_open_tasks_hides_Import_and_the_import_counts_and_shows_the_line_and_Cancel()
    {
        var card = await _f.Checked(TodoNoteSetupSample.Check(open: 45, cantImport: [4], willImport: 40, cap: 20), Token, importServed: false);

        Assert.Equal((false, false, false, true), (card.ShowImport, card.ShowStart, card.ShowStartOneWay, card.ShowCancel));
        Assert.Equal((Line, true), (card.ImportLaterText, card.ShowImportLater));
        Assert.Equal((false, false, false), (card.HasCounts, card.HasCantImport, card.HasOverCap));
    }

    [Fact]
    public async Task Not_served_a_note_with_open_tasks_on_a_disk_that_cannot_swap_hides_Start_one_way_too()
    {
        var card = await _f.Checked(TodoNoteSetupSample.Check(open: 12, canSwap: false), Token, importServed: false);

        Assert.Equal((false, false, false, true), (card.ShowImport, card.ShowStart, card.ShowStartOneWay, card.ShowCancel));
        Assert.Equal((Line, false), (card.ImportLaterText, card.HasOneWay));
    }

    [Fact]
    public async Task Not_served_a_new_note_still_offers_Start_syncing_and_no_line()
    {
        var card = await _f.Checked(TodoNoteSetupSample.NewNote, Token, importServed: false);

        Assert.Equal((true, false, false, true, false), (card.ShowStart, card.ShowImport, card.ShowStartOneWay, card.ShowCancel, card.ShowImportLater));
        Assert.Equal(string.Empty, card.ImportLaterText);
    }

    [Fact]
    public async Task Not_served_a_note_with_no_open_tasks_still_offers_Start_syncing_and_no_line()
    {
        var card = await _f.Checked(TodoNoteSetupSample.Check(open: 0), Token, importServed: false);

        Assert.Equal((true, false, true, false), (card.ShowStart, card.ShowImport, card.ShowCancel, card.ShowImportLater));
        Assert.Equal("Todo.md has no open tasks.", card.ResultText);
    }

    [Fact]
    public async Task Not_served_a_one_way_disk_with_a_new_note_still_offers_Start_syncing_one_way()
    {
        var card = await _f.Checked(TodoNoteSetupSample.Check(exists: false, open: 0, canSwap: false), Token, importServed: false);

        Assert.Equal((true, false), (card.ShowStartOneWay, card.ShowImportLater));
    }

    [Fact]
    public async Task Served_a_note_with_open_tasks_offers_Import_with_its_counts_and_no_line()
    {
        var card = await _f.Checked(TodoNoteSetupSample.Check(open: 42, sindriOpen: 6), Token, importServed: true);

        Assert.Equal((true, false, false), (card.ShowImport, card.ShowImportLater, card.ShowStartOneWay));
        Assert.Equal("Import adds ids to 42 lines in Todo.md and adds your 6 Sindri todos after them", card.CountsText);
    }
}
