using Aw.Kernel.Ids;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Projects;
using Aw.Sindri.Core.Projects.Vocabulary;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Tests.Projects;

namespace Aw.Sindri.Tests.Views;

/// <summary>The 'project-attached' and 'project-hidden' folds (projects page spec §5.2): pure rows from the four project events.</summary>
public sealed class ProjectsProjectionTests
{
    private readonly ViewsHarness _v = new();
    private readonly ProjectId _id = ProjectRows.HandMadeId;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private SliceHarness H => _v.Slice;

    private CommandId Command() => H.Ids.New<CommandId>();

    private Task Attach(ProjectState? state = null, string? linear = null, string? name = "Kitchen", IReadOnlyList<string>? links = null, string? notes = null, DateOnly? target = null) =>
        _v.Fold(EventTypes.ProjectAttached, new ProjectAttached(Command(), _id, linear, name, state, links ?? [], target, notes));

    private Task Change(long version, ProjectState? state = null, bool clearState = false, IReadOnlyList<string>? links = null, string? notes = null, bool clearNotes = false, DateOnly? target = null, bool clearTarget = false, string? name = null) =>
        _v.Fold(EventTypes.ProjectChanged, new ProjectChanged(Command(), _id, version, name, state, clearState, links, target, clearTarget, notes, clearNotes));

    private async Task<ProjectAttachedRow?> Row() => (await _v.Views.Get<ProjectAttachedRow>(ViewNames.ProjectAttached, _id.Value, Token)).Value;

    [Fact, Trait("Lane", "unit")]
    public async Task Attached_makes_a_row_at_version_1_keyed_by_the_project_id()
    {
        await Attach(ProjectState.Planned, links: ["https://example.com/plan"], notes: "one line", target: new DateOnly(2026, 11, 14));

        var row = (await Row())!;

        Assert.Equal((_id, null, "Kitchen", ProjectState.Planned, "one line", new DateOnly(2026, 11, 14), 1L), (row.Id, row.Linear, row.Name, row.State, row.Notes, row.Target, row.Version));
        Assert.Equal(["https://example.com/plan"], row.Links);
        Assert.Equal((H.Clock.GetUtcNow(), H.Clock.GetUtcNow(), null, 1L), (row.AttachedAt, row.EditedAt, row.FinishedAt, row.Sequence));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_linear_project_attaches_with_its_key_and_no_name()
    {
        await Attach(linear: ProjectRows.LinearKey, name: null);

        var row = (await Row())!;

        Assert.Equal((ProjectRows.LinearKey, null), (row.Linear, row.Name));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Attaching_straight_into_done_sets_finished_at_to_that_event()
    {
        await Attach(ProjectState.Done);

        Assert.Equal(H.Clock.GetUtcNow(), (await Row())!.FinishedAt);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Changed_sets_only_the_asked_fields_and_the_version()
    {
        await Attach(ProjectState.Planned, links: ["https://example.com/a"], notes: "first", target: new DateOnly(2026, 11, 14));
        var attachedAt = H.Clock.GetUtcNow();
        H.Clock.Advance(TimeSpan.FromMinutes(5));

        await Change(version: 2, notes: "second");

        var row = (await Row())!;
        Assert.Equal((2L, "second", ProjectState.Planned, new DateOnly(2026, 11, 14), "Kitchen"), (row.Version, row.Notes, row.State, row.Target, row.Name));
        Assert.Equal(["https://example.com/a"], row.Links);
        Assert.Equal((attachedAt, H.Clock.GetUtcNow(), 2L), (row.AttachedAt, row.EditedAt, row.Sequence));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Changed_links_replace_the_whole_list_and_a_name_replaces_the_name()
    {
        await Attach(links: ["https://example.com/a", "https://example.com/b"]);

        await Change(version: 2, links: ["https://example.com/c"], name: "Bathroom");

        var row = (await Row())!;
        Assert.Equal(["https://example.com/c"], row.Links);
        Assert.Equal("Bathroom", row.Name);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_clear_flags_drop_state_notes_and_target()
    {
        await Attach(ProjectState.Paused, notes: "n", target: new DateOnly(2026, 11, 14));

        await Change(version: 2, clearState: true, clearNotes: true, clearTarget: true);

        var row = (await Row())!;
        Assert.Equal((null, null, null), (row.State, row.Notes, row.Target));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Finished_at_is_the_time_of_the_event_that_set_done_or_canceled()
    {
        await Attach(ProjectState.InProgress);
        H.Clock.Advance(TimeSpan.FromHours(1));
        var doneAt = H.Clock.GetUtcNow();
        await Change(version: 2, state: ProjectState.Done);
        Assert.Equal(doneAt, (await Row())!.FinishedAt);

        H.Clock.Advance(TimeSpan.FromHours(1));
        await Change(version: 3, notes: "still done");
        Assert.Equal(doneAt, (await Row())!.FinishedAt);

        H.Clock.Advance(TimeSpan.FromHours(1));
        var canceledAt = H.Clock.GetUtcNow();
        await Change(version: 4, state: ProjectState.Canceled);
        Assert.Equal(canceledAt, (await Row())!.FinishedAt);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Finished_at_clears_when_the_state_goes_back_or_is_cleared()
    {
        await Attach(ProjectState.InProgress);
        await Change(version: 2, state: ProjectState.Done);
        await Change(version: 3, state: ProjectState.Paused);
        Assert.Null((await Row())!.FinishedAt);

        await Change(version: 4, state: ProjectState.Canceled);
        _ = Assert.NotNull((await Row())!.FinishedAt);
        await Change(version: 5, clearState: true);
        Assert.Null((await Row())!.FinishedAt);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Versions_follow_the_events_so_the_next_command_sees_the_latest()
    {
        await Attach();
        await Change(version: 2, notes: "a");
        await Change(version: 3, notes: "b");

        Assert.Equal(3L, (await Row())!.Version);
    }

    private ProjectChanged Bare(long version) => new(Command(), _id, version, Name: null, State: null, ClearState: false, Links: null, Target: null, ClearTarget: false);

    // Events written before the field was renamed carry "note" and "clearNote": the payload is rewritten to that shape after it is serialized (owner q158)
    private Task FoldOld<TPayload>(string type, TPayload payload, params (string Now, string Then)[] edits) =>
        _v.Fold(type, payload, ItemId.From("ITM-" + new string('1', 32)), json =>
        {
            foreach (var (now, then) in edits)
            {
                Assert.Contains(now, json, StringComparison.Ordinal);
                json = json.Replace(now, then, StringComparison.Ordinal);
            }

            return json;
        });

    [Fact, Trait("Lane", "unit")]
    public async Task An_old_attached_event_with_note_shows_up_as_the_rows_notes()
    {
        await FoldOld(EventTypes.ProjectAttached, new ProjectAttached(Command(), _id, Linear: null, "Kitchen", ProjectState.Planned, [], Target: null), ("\"notes\":null", "\"note\":\"order the tile\""));

        Assert.Equal("order the tile", (await Row())!.Notes);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_old_changed_event_with_note_sets_the_notes_and_one_with_clearNote_drops_them()
    {
        await Attach(notes: "first");

        await FoldOld(EventTypes.ProjectChanged, Bare(2), ("\"notes\":null", "\"note\":\"second\""));
        Assert.Equal("second", (await Row())!.Notes);

        await FoldOld(EventTypes.ProjectChanged, Bare(3), ("\"clearNotes\":false", "\"clearNote\":true"));
        Assert.Null((await Row())!.Notes);
    }

    [Fact, Trait("Lane", "unit")]
    public void New_events_write_notes_and_never_the_old_note_field()
    {
        var recorded = Recorded.Of(H.Events, EventTypes.ProjectAttached, new ProjectAttached(Command(), _id, Linear: null, "Kitchen", State: null, [], Target: null, "line one\nline two"), 1);

        Assert.Contains("\"notes\":\"line one\\nline two\"", recorded.Envelope.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain("\"note\"", recorded.Envelope.Payload, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Detached_deletes_the_row_so_the_linear_key_can_be_attached_again()
    {
        await Attach(linear: ProjectRows.LinearKey, name: null);

        await _v.Fold(EventTypes.ProjectDetached, new ProjectDetached(Command(), _id, Version: 2));

        Assert.Null(await Row());
        await Attach(linear: ProjectRows.LinearKey, name: null);
        Assert.Equal(1L, (await Row())!.Version);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_change_or_detach_for_a_project_the_view_does_not_hold_is_skipped()
    {
        await Change(version: 2, notes: "orphan");
        await _v.Fold(EventTypes.ProjectDetached, new ProjectDetached(Command(), _id, Version: 3));

        Assert.Null(await Row());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Hide_and_unhide_keep_one_row_per_linear_key_at_the_event_version()
    {
        await _v.Fold(EventTypes.ProjectHidden, new ProjectHidden(Command(), ProjectRows.LinearKey, Hidden: true, Version: 1));
        var hidden = (await _v.Views.Get<ProjectHiddenRow>(ViewNames.ProjectHidden, ProjectRows.LinearKey, Token)).Value!;
        Assert.Equal((ProjectRows.LinearKey, true, 1L, 1L), (hidden.Linear, hidden.Hidden, hidden.Version, hidden.Sequence));

        await _v.Fold(EventTypes.ProjectHidden, new ProjectHidden(Command(), ProjectRows.LinearKey, Hidden: false, Version: 2));
        var unhidden = (await _v.Views.Get<ProjectHiddenRow>(ViewNames.ProjectHidden, ProjectRows.LinearKey, Token)).Value!;
        Assert.Equal((false, 2L, 2L), (unhidden.Hidden, unhidden.Version, unhidden.Sequence));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_projection_folds_exactly_the_four_project_events_into_its_two_views()
    {
        var projection = new ProjectsProjection(H.Events);

        Assert.All([EventTypes.ProjectAttached, EventTypes.ProjectChanged, EventTypes.ProjectDetached, EventTypes.ProjectHidden], type => Assert.True(projection.Handles(type)));
        Assert.False(projection.Handles(EventTypes.ItemCreated));
        Assert.Equal([ViewNames.ProjectAttached, ViewNames.ProjectHidden], projection.Views);
    }
}
