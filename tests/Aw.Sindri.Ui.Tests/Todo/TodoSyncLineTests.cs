using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Todo;

/// <summary>The Todo panel's sync line in every state (todo spec §3.1, §3.4): hidden with no note, quiet when synced, a warning with the reason otherwise, a countdown while a pick's Undo window runs.</summary>
[Lane(TestLane.Unit)]
public sealed class TodoSyncLineTests
{
    private readonly TodoFixture _f = new();   // the clock reads 2026-01-01T00:00:00Z

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<(string Quiet, string Live, bool Warning, bool Has)> Line(string noteJson)
    {
        _f.Server.TodoNoteAnswer = new RouteAnswer(HttpStatusCode.OK, noteJson);
        var panel = _f.Panel();
        await panel.Refresh(TodoFixture.Url, Token);
        return (panel.SyncQuiet, panel.SyncLive, panel.SyncIsWarning, panel.HasSync);
    }

    [Fact]
    public async Task No_note_set_draws_no_line() =>
        Assert.Equal((string.Empty, string.Empty, false, false), await Line(TodoNoteSetupSample.NoteIn("not set")));

    [Fact]
    public async Task A_syncing_note_says_when_it_last_synced_and_is_not_a_live_region_message() =>
        Assert.Equal(("Synced with Obsidian 09:58", string.Empty, false, true), await Line(TodoNoteSetupSample.NoteIn("syncing")));

    [Fact]
    public async Task A_note_waiting_for_its_import_is_a_warning() =>
        Assert.Equal((string.Empty, "Waiting for import", true, true), await Line(TodoNoteSetupSample.NoteIn("waiting for import", lastSyncAt: null)));

    [Fact]
    public async Task An_open_hold_says_how_many_todos_the_note_changed() =>
        Assert.Equal((string.Empty, "Paused: the note changed 14 todos", true, true), await Line(TodoNoteSetupSample.NoteIn("syncing", hold: 14)));

    [Fact]
    public async Task One_line_that_cannot_sync_is_named_with_its_reason() =>
        Assert.Equal((string.Empty, "Line 14 can't sync: it has a markdown link", true, true), await Line(TodoNoteSetupSample.NoteIn("syncing", cantSync: [(14, "it has a markdown link")])));

    [Fact]
    public async Task Two_lines_that_cannot_sync_are_counted() =>
        Assert.Equal((string.Empty, "2 lines can't sync", true, true), await Line(TodoNoteSetupSample.NoteIn("syncing", cantSync: [(14, "it has a markdown link"), (20, "it has two ids")])));

    [Fact]
    public async Task A_one_way_disk_says_so() =>
        Assert.Equal((string.Empty, "Obsidian → Sindri only: can't write to this disk", true, true), await Line(TodoNoteSetupSample.NoteIn("one way")));

    [Fact]
    public async Task A_note_that_cannot_be_read_points_at_doctor_and_never_shows_the_servers_text() =>
        Assert.Equal((string.Empty, "Obsidian note: can't read. Run `sindri doctor` to see why.", true, true), await Line(TodoNoteSetupSample.NoteIn("can't read", lastError: "Todo.md is missing")));

    [Fact]
    public async Task A_busy_note_says_it_is_trying_again() =>
        Assert.Equal((string.Empty, "Obsidian note busy, trying again", true, true), await Line(TodoNoteSetupSample.NoteIn("note busy")));

    [Fact]
    public async Task A_pick_in_its_window_counts_down_in_the_live_region_without_the_warning_colour() =>
        Assert.Equal((string.Empty, "Saving your pick to Obsidian in 6 s", false, true), await Line(TodoNoteSetupSample.NoteIn("syncing", pickUndoUntil: "2026-01-01T00:00:06Z")));

    [Fact]
    public async Task An_older_sindri_with_a_bare_404_hides_a_line_that_was_shown()
    {
        var panel = _f.Panel();
        _f.Server.TodoNoteAnswer = new RouteAnswer(HttpStatusCode.OK, TodoNoteSetupSample.NoteIn("syncing"));
        await panel.Refresh(TodoFixture.Url, Token);
        Assert.True(panel.HasSync);

        _f.Server.TodoNoteAnswer = new RouteAnswer(HttpStatusCode.NotFound, string.Empty);
        await panel.Refresh(TodoFixture.Url, Token);

        Assert.False(panel.HasSync);
    }

    [Fact]
    public async Task A_failed_read_leaves_the_last_line_in_place()
    {
        var panel = _f.Panel();
        _f.Server.TodoNoteAnswer = new RouteAnswer(HttpStatusCode.OK, TodoNoteSetupSample.NoteIn("syncing"));
        await panel.Refresh(TodoFixture.Url, Token);

        _f.Server.TodoNoteAnswer = RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-API-503", "Sindri is busy (fix: wait)");
        await panel.Refresh(TodoFixture.Url, Token);

        Assert.Equal("Synced with Obsidian 09:58", panel.SyncQuiet);
    }
}
