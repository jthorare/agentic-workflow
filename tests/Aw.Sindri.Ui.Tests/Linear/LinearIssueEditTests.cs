using Aw.Kernel.Testing;

namespace Aw.Sindri.Ui.Tests.Linear;

/// <summary>The Linear issue sheet's edit form (Linear hub spec §3, "Issue sheet"): what a change sends, the dot and the dirty line, and the pickers.</summary>
[Lane(TestLane.Unit)]
public sealed class LinearIssueEditTests
{
    private readonly LinearIssueEditFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Changing_only_priority_sends_set_and_opened_for_priority_and_nothing_else()
    {
        var sheet = await _f.Opened(Token);

        _f.Pick("Priority", "High");
        await sheet.Save(Token);

        Assert.Equal(("ENG-12", """{"priority":1}""", """{"priority":2}"""), (_f.Sent.Key, _f.Sent.Body.Opened.ToJsonString(), _f.Sent.Body.Set.ToJsonString()));
    }

    [Fact]
    public async Task Changing_state_sends_the_state_id_with_the_state_it_opened_on()
    {
        var sheet = await _f.Opened(Token);

        _f.Pick("State", "Done");
        await sheet.Save(Token);

        Assert.Equal(($$"""{"stateId":"{{LinearIssueEditFixture.Done}}"}""", """{"stateId":"00000000-0000-4000-8000-000000000052"}"""), (_f.Sent.Body.Set.ToJsonString(), _f.Sent.Body.Opened.ToJsonString()));
    }

    [Fact]
    public async Task Not_changing_the_state_never_sends_a_state_id()
    {
        var sheet = await _f.Opened(Token);

        _f.Pick("Assignee", "Sam Lee");
        _f.Pick("Priority", "Low");
        _f.Pick("Cycle", "Cycle 15");
        await sheet.Save(Token);

        Assert.False(_f.Sent.Body.Set.ContainsKey("stateId") || _f.Sent.Body.Opened.ContainsKey("stateId"));
        Assert.Equal(["priority", "assigneeId", "cycleId"], _f.Sent.Body.Set.Select(m => m.Key), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Save_is_disabled_until_something_changes_and_again_when_it_is_changed_back()
    {
        var sheet = await _f.Opened(Token);
        Assert.Equal((false, false, string.Empty), (sheet.CanSave, sheet.HasChanges, sheet.DirtyLine));

        _f.Pick("Priority", "High");
        Assert.True(sheet.CanSave);

        _f.Pick("Priority", "Urgent");
        Assert.Equal((false, false), (sheet.CanSave, sheet.HasChanges));
        await sheet.Save(Token);
        Assert.Empty(_f.Server.LinearChanges);
    }

    [Fact]
    public async Task Two_changes_mark_their_fields_and_the_dirty_line_names_them_in_grid_order()
    {
        var sheet = await _f.Opened(Token);

        _f.Pick("Priority", "High");
        _f.Pick("State", "Done");

        Assert.Equal("2 changes: State, Priority", sheet.DirtyLine);
        Assert.Equal([true, true, false, false, false], sheet.EditFields.Take(5).Select(f => f.IsChanged));
        Assert.Equal("State: Done, changed", _f.Field("State").Name);
        Assert.Equal("Assignee: Joi Example", _f.Field("Assignee").Name);
        _f.Pick("State", "In Progress");
        Assert.Equal("1 change: Priority", sheet.DirtyLine);
    }

    [Fact]
    public async Task The_grid_runs_state_priority_assignee_cycle_labels_project_with_a_clear_entry_first_in_each_picker()
    {
        _ = await _f.Opened(Token);

        Assert.Equal(["State", "Priority", "Assignee", "Cycle", "Labels", "Project"], _f.Sheet.EditFields.Select(f => f.Label), StringComparer.Ordinal);
        Assert.Equal(["No priority", "Urgent", "High", "Medium", "Low"], _f.Field("Priority").Options.Select(o => o.Name), StringComparer.Ordinal);
        Assert.Equal("Unassigned", _f.Field("Assignee").Options[0].Name);
        Assert.Equal("No cycle", _f.Field("Cycle").Options[0].Name);
        Assert.Equal(["Backlog", "Todo", "In Progress", "In Review", "Done"], _f.Field("State").Options.Select(o => o.Name), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Choosing_unassigned_and_no_cycle_sends_null()
    {
        var sheet = await _f.Opened(Token);

        _f.Pick("Assignee", "Unassigned");
        _f.Pick("Cycle", "No cycle");
        await sheet.Save(Token);

        Assert.Equal("""{"assigneeId":null,"cycleId":null}""", _f.Sent.Body.Set.ToJsonString());
        Assert.Equal("""{"assigneeId":"00000000-0000-4000-8000-0000000000a1","cycleId":"00000000-0000-4000-8000-0000000000c1"}""", _f.Sent.Body.Opened.ToJsonString());
    }

    [Fact]
    public async Task Unticking_a_label_sends_the_full_set_and_unticking_all_sends_an_empty_set()
    {
        var sheet = await _f.Opened(Token);

        _f.Tick("api", on: false);
        await sheet.Save(Token);
        Assert.Equal("""{"labelIds":["00000000-0000-4000-8000-0000000000d1"]}""", _f.Sent.Body.Set.ToJsonString());

        _f.Tick("api", on: false);
        _f.Tick("bug", on: false);
        await sheet.Save(Token);
        Assert.Equal("""{"labelIds":[]}""", _f.Server.LinearChanges[^1].Body.Set.ToJsonString());
    }

    [Fact]
    public async Task The_title_and_the_description_are_edited_as_text_and_sent_when_changed()
    {
        var sheet = await _f.Opened(Token);

        sheet.TitleField.Text = "Retry budget";
        sheet.DescriptionField.Text = "Cap it.";
        await sheet.Save(Token);

        Assert.Equal("""{"title":"Retry budget","description":"Cap it."}""", _f.Sent.Body.Set.ToJsonString());
    }

    [Fact]
    public async Task The_dirty_line_counts_the_title_and_the_description()
    {
        var sheet = await _f.Opened(Token);

        sheet.TitleField.Text = "Retry budget";
        sheet.DescriptionField.Text = "Cap it.";

        Assert.Equal("2 changes: Title, Description", sheet.DirtyLine);
    }

    [Fact]
    public async Task The_project_is_shown_and_never_edited()
    {
        _ = await _f.Opened(Token);

        var project = _f.Field("Project");
        Assert.Equal((false, "Sync worker", false), (project.IsEditable, project.Value, project.IsChanged));
    }

    [Fact]
    public async Task The_pickers_read_the_teams_lookups_once_per_opened_issue()
    {
        var sheet = await _f.Opened(Token);

        await sheet.Refresh(LinearIssueEditFixture.Url, Token);
        await sheet.Refresh(LinearIssueEditFixture.Url, Token);

        Assert.Equal(["ENG"], _f.Server.LinearLookupsRead);
    }

    [Fact]
    public async Task Lookups_that_have_not_synced_disable_the_pickers_with_a_line_and_leave_priority_and_text_open()
    {
        _f.Server.LinearLookupsAnswer = Testing.RouteAnswer.Api("linear-lookups", "not-synced");

        _ = await _f.Opened(Token);

        Assert.Equal((false, "ENG's states haven't synced yet"), (_f.Field("State").IsEnabled, _f.Field("State").UnavailableText));
        Assert.Equal((false, false), (_f.Field("Assignee").IsEnabled, _f.Field("Labels").IsEnabled));
        Assert.Equal((true, string.Empty), (_f.Field("Priority").IsEnabled, _f.Field("Priority").UnavailableText));
        Assert.True(_f.Sheet.TitleField.IsEnabled);
    }
}
