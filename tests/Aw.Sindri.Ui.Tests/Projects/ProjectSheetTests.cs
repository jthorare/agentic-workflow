using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Projects;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Instance;
using Aw.Sindri.Ui.ViewModels.Projects;

namespace Aw.Sindri.Ui.Tests.Projects;

/// <summary>A project card's sheet (projects page spec §3.2): what it reads, the links it may open, and the actions each origin has.</summary>
[Lane(TestLane.Unit)]
public sealed class ProjectSheetTests
{
    private readonly ProjectSheetFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Opening_reads_the_card_by_its_unescaped_id()
    {
        var sheet = await _f.Opened(ProjectSheetFixture.Attached, Token);

        Assert.Equal((ProjectSheetFixture.Attached, FramePhase.Shown), (sheet.Id, sheet.Phase));
        Assert.Equal([ProjectSheetFixture.Attached], _f.Server.ProjectsRead);
    }

    [Fact]
    public async Task A_led_key_with_a_slash_travels_as_one_escaped_segment_and_is_read_whole()
    {
        _f.Serve(ProjectSheetFixture.Led, c => c["origin"] = "led");

        var sheet = await _f.Opened(ProjectSheetFixture.Led, Token);

        Assert.DoesNotContain('/', ProjectSheetFixture.Subject(ProjectSheetFixture.Led));
        Assert.Equal(ProjectSheetFixture.Led, sheet.Id);
        Assert.Equal([ProjectSheetFixture.Led], _f.Server.ProjectsRead);
    }

    [Fact]
    public async Task An_attached_card_shows_every_field_in_words()
    {
        var sheet = await _f.Opened(ProjectSheetFixture.Attached, Token);

        Assert.Equal(("Mobile app", "attached", "Paused · set by you", null), (sheet.Heading, sheet.OriginChip, sheet.StateText, sheet.HealthWord));
        Assert.Equal(("Lead: Sam Lee", null, "7 open issues", null), (sheet.LeadText, sheet.TargetText, sheet.IssuesText, sheet.PrsText));
        Assert.Equal(("Waiting on the vendor", null, null), (sheet.Notes, sheet.Description, sheet.FinishedText));
        Assert.NotNull(sheet.ActivityText);
    }

    [Fact]
    public async Task A_served_description_target_health_and_prs_are_shown()
    {
        _f.Serve(ProjectSheetFixture.Attached, c =>
        {
            c["description"] = "Rebuild the checkout.";
            c["target"] = "2026-01-31";
            c["health"] = "atRisk";
            c["openPrs"] = 3;
            c["prScope"] = "(updated in the last 30 days)";
            c["stateSetByOwner"] = false;
        });

        var sheet = await _f.Opened(ProjectSheetFixture.Attached, Token);

        Assert.Equal(("Rebuild the checkout.", "Target Jan 31", "at risk", ChipTone.Attn), (sheet.Description, sheet.TargetText, sheet.HealthWord, sheet.HealthTone));
        Assert.Equal(("3 open PRs (updated in the last 30 days)", "Paused"), (sheet.PrsText, sheet.StateText));
    }

    [Fact]
    public async Task A_null_description_shows_nothing()
    {
        var sheet = await _f.Opened(ProjectSheetFixture.Attached, Token);

        Assert.Null(sheet.Description);
    }

    [Fact]
    public async Task A_finished_card_says_when_it_finished()
    {
        _f.Serve(ProjectSheetFixture.Led, c => (c["origin"], c["state"], c["finishedAt"]) = ("led", "done", "2025-12-30T00:00:00Z"));

        var sheet = await _f.Opened(ProjectSheetFixture.Led, Token);

        Assert.Equal("Done Dec 30", sheet.FinishedText);
    }

    [Fact]
    public async Task The_card_lists_Open_in_Linear_then_the_owners_links()
    {
        var sheet = await _f.Opened(ProjectSheetFixture.Attached, Token);

        Assert.Equal([("Open in Linear ↗", true), ("https://example.com/mobile", true)], sheet.Links.Select(l => (l.Text, l.CanOpen)));
    }

    [Theory]
    [InlineData("Linear", "https://linear.app/acme/project/5", true)]
    [InlineData("Linear", "http://linear.app/acme/project/5", false)]
    [InlineData("Linear", "https://linear.app.evil.example/x", false)]
    [InlineData("Linear", "https://evil.example/linear.app", false)]
    [InlineData("Linear", "https://linear.app@evil.example/x", false)]
    [InlineData("GitHub", "https://github.com/acme/web-app", true)]
    [InlineData("GitHub", "https://gist.github.com/x", false)]
    [InlineData("GitHub", "https://notgithub.com/x", false)]
    [InlineData("Docs", "https://example.com/docs", true)]
    [InlineData("Docs", "http://example.com/docs", false)]
    [InlineData("Docs", "javascript:alert(1)", false)]
    [InlineData("Docs", "file:///etc/passwd", false)]
    [InlineData("Docs", "not a url", false)]
    public async Task A_link_is_clickable_only_when_https_and_on_its_labelled_host(string label, string url, bool clickable)
    {
        _f.Serve(ProjectSheetFixture.Attached, c => c["links"] = new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject { ["label"] = label, ["url"] = url }));

        var sheet = await _f.Opened(ProjectSheetFixture.Attached, Token);
        await Assert.Single(sheet.Links).Open(Token);

        Assert.Equal(clickable, Assert.Single(sheet.Links).CanOpen);
        string[] opened = clickable ? [new Uri(url).AbsoluteUri] : [];
        Assert.Equal(opened, _f.Launcher.Opened);
    }

    [Fact]
    public async Task Open_in_Linear_opens_the_link_through_the_launcher()
    {
        var sheet = await _f.Opened(ProjectSheetFixture.Attached, Token);

        await sheet.Links[0].Open(Token);

        Assert.Equal(["https://linear.app/acme/project/5"], _f.Launcher.Opened);
    }

    [Fact]
    public async Task A_repo_with_a_GitHub_name_links_its_pulls_and_one_without_says_it_is_not_synced()
    {
        _f.Serve(ProjectSheetFixture.Attached, c => c["repos"] = new System.Text.Json.Nodes.JsonArray("acme/web-app", "api"));

        var sheet = await _f.Opened(ProjectSheetFixture.Attached, Token);
        await sheet.Repos[0].Open(Token);

        Assert.Equal([("acme/web-app PRs ↗", true), ("api: PRs not synced for this repo", false)], sheet.Repos.Select(r => (r.Text, r.CanOpen)));
        Assert.Equal(["https://github.com/acme/web-app/pulls"], _f.Launcher.Opened);
    }

    [Theory]
    [InlineData("led", false, false, false, false, false, true, false)]
    [InlineData("attached", false, true, true, true, true, false, false)]
    [InlineData("handMade", false, true, true, false, true, false, false)]
    [InlineData("led", true, false, false, false, false, false, true)]
    public async Task Each_origin_has_its_own_actions(string origin, bool hidden, bool edit, bool setState, bool clearState, bool remove, bool hide, bool unhide)
    {
        _f.Serve(ProjectSheetFixture.Attached, c => c["origin"] = origin, one => one["hidden"] = hidden);

        var sheet = await _f.Opened(ProjectSheetFixture.Attached, Token);

        Assert.Equal((edit, setState, clearState, remove, hide, unhide), (sheet.CanEdit, sheet.CanSetState, sheet.CanClearState, sheet.CanRemove, sheet.CanHide, sheet.CanUnhide));
        Assert.Equal(origin == "handMade", sheet.CanEditName);
    }

    [Fact]
    public async Task A_state_the_owner_did_not_set_cannot_be_cleared()
    {
        _f.Serve(ProjectSheetFixture.Attached, c => c["stateSetByOwner"] = false);

        var sheet = await _f.Opened(ProjectSheetFixture.Attached, Token);

        Assert.True(sheet.CanSetState);
        Assert.False(sheet.CanClearState);
    }

    [Fact]
    public async Task Editing_an_attached_card_sends_its_links_and_target_with_the_version_it_read()
    {
        var sheet = await _f.Opened(ProjectSheetFixture.Attached, Token);
        sheet.BeginEdit();
        (sheet.EditTarget, sheet.EditLinks) = ("2026-02-01", "https://a.example/x\nhttps://b.example/y\n");

        await sheet.Save(Token);

        var (route, body) = Assert.Single(_f.Server.ProjectWrites);
        var change = Assert.IsType<ProjectChange>(body);
        Assert.Equal((ProjectRoutes.Change(ProjectSheetFixture.Attached), 1L, null, "2026-02-01"), (route, change.Version, change.Name, change.Target));
        Assert.Equal(["https://a.example/x", "https://b.example/y"], change.Links);
        Assert.False(sheet.IsEditing);
    }

    [Fact]
    public async Task Editing_a_hand_made_card_sends_its_name_too()
    {
        _f.Serve(ProjectSheetFixture.Attached, c => c["origin"] = "handMade");
        var sheet = await _f.Opened(ProjectSheetFixture.Attached, Token);
        sheet.BeginEdit();
        sheet.EditName = "Kitchen redo";

        await sheet.Save(Token);

        Assert.Equal("Kitchen redo", Assert.IsType<ProjectChange>(Assert.Single(_f.Server.ProjectWrites).Body).Name);
    }

    [Fact]
    public async Task The_form_opens_with_the_cards_values_and_an_untouched_one_sends_no_field()
    {
        _f.Serve(ProjectSheetFixture.Attached, c => c["target"] = "2026-01-31");
        var sheet = await _f.Opened(ProjectSheetFixture.Attached, Token);

        sheet.BeginEdit();
        await sheet.Save(Token);

        Assert.Equal(("2026-01-31", "https://example.com/mobile"), (sheet.EditTarget, sheet.EditLinks));
        var change = Assert.IsType<ProjectChange>(Assert.Single(_f.Server.ProjectWrites).Body);
        Assert.Equal((null, null, false), (change.Target, change.Name, change.ClearTarget));
    }

    [Fact]
    public async Task Emptying_the_target_clears_it()
    {
        _f.Serve(ProjectSheetFixture.Attached, c => c["target"] = "2026-01-31");
        var sheet = await _f.Opened(ProjectSheetFixture.Attached, Token);
        sheet.BeginEdit();
        sheet.EditTarget = string.Empty;

        await sheet.Save(Token);

        var change = Assert.IsType<ProjectChange>(Assert.Single(_f.Server.ProjectWrites).Body);
        Assert.Equal((true, null), (change.ClearTarget, change.Target));
    }

    [Fact]
    public async Task Cancel_leaves_the_form_and_writes_nothing()
    {
        var sheet = await _f.Opened(ProjectSheetFixture.Attached, Token);
        sheet.BeginEdit();
        sheet.EditTarget = "2026-02-01";

        sheet.CancelEdit();

        Assert.Equal((false, 0), (sheet.IsEditing, _f.Server.ProjectWrites.Count));
    }

    [Fact]
    public async Task Set_state_sends_the_picked_state_and_the_version()
    {
        var sheet = await _f.Opened(ProjectSheetFixture.Attached, Token);
        sheet.SelectedState = sheet.StateChoices.Single(s => string.Equals(s.Value, "inProgress", StringComparison.Ordinal));

        await sheet.SetState(Token);

        var (route, body) = Assert.Single(_f.Server.ProjectWrites);
        Assert.Equal((ProjectRoutes.Change(ProjectSheetFixture.Attached), new ProjectChange(Version: 1, State: "inProgress")), (route, body));
    }

    [Fact]
    public async Task Use_Linears_state_clears_the_owners()
    {
        var sheet = await _f.Opened(ProjectSheetFixture.Attached, Token);

        await sheet.ClearState(Token);

        Assert.Equal(new ProjectChange(Version: 1, ClearState: true), Assert.Single(_f.Server.ProjectWrites).Body);
    }

    [Fact]
    public async Task A_finished_save_is_announced_and_the_card_is_read_again()
    {
        var sheet = await _f.Opened(ProjectSheetFixture.Attached, Token);
        sheet.BeginEdit();

        await sheet.Save(Token);

        Assert.Equal(("Mobile app saved", 2), (sheet.Announcement, _f.Server.ProjectsRead.Count));
    }

    [Fact]
    public async Task Saving_shows_Saving_and_disables_until_the_answer()
    {
        _f.Server.CommandGate = new TaskCompletionSource();
        var sheet = await _f.Opened(ProjectSheetFixture.Attached, Token);
        sheet.BeginEdit();

        var saving = sheet.Save(Token);

        Assert.Equal((true, "Saving…"), (sheet.IsSaving, sheet.SaveText));
        await sheet.Save(Token);   // a second press while one is in flight does nothing
        _ = Assert.Single(_f.Server.ProjectWrites);
        _f.Server.CommandGate.SetResult();
        await saving;
        Assert.Equal((false, "Save"), (sheet.IsSaving, sheet.SaveText));
    }

    [Fact]
    public async Task A_refused_write_shows_under_the_button_and_keeps_the_fields()
    {
        _f.Server.ProjectWriteAnswer = RouteAnswer.Problem(HttpStatusCode.BadRequest, "SND-PROJECT-004", "links: more than 10 links");
        var sheet = await _f.Opened(ProjectSheetFixture.Attached, Token);
        sheet.BeginEdit();
        sheet.EditTarget = "2026-02-01";

        await sheet.Save(Token);

        Assert.Contains("SND-PROJECT-004", sheet.SaveError, StringComparison.Ordinal);
        Assert.Equal((true, false, "2026-02-01", string.Empty), (sheet.IsEditing, sheet.IsConflict, sheet.EditTarget, sheet.Announcement));
    }

    [Fact]
    public async Task A_stale_version_says_changed_elsewhere_and_Reload_reads_the_card_again()
    {
        _f.Server.ProjectWriteAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-PROJECT-409", "This project changed since you read it. Read it again and retry.");
        var sheet = await _f.Opened(ProjectSheetFixture.Attached, Token);
        sheet.BeginEdit();
        sheet.EditTarget = "2026-02-01";

        await sheet.Save(Token);

        Assert.Equal((true, "Changed elsewhere since you opened it."), (sheet.IsConflict, sheet.SaveError));
        await sheet.Reload(Token);
        Assert.Equal((false, string.Empty, false, 2), (sheet.IsConflict, sheet.SaveError, sheet.IsEditing, _f.Server.ProjectsRead.Count));
    }

    [Fact]
    public async Task A_refresh_while_editing_keeps_the_fields_and_the_version_the_form_opened_on()
    {
        var sheet = await _f.Opened(ProjectSheetFixture.Attached, Token);
        sheet.BeginEdit();
        sheet.EditTarget = "2026-02-01";
        _f.Serve(ProjectSheetFixture.Attached, c => (c["version"], c["name"]) = (2, "Mobile app 2"));

        await sheet.Refresh(ProjectsFixture.Url, Token);
        await sheet.Save(Token);

        Assert.Equal(("2026-02-01", 1L), (sheet.EditTarget, Assert.IsType<ProjectChange>(Assert.Single(_f.Server.ProjectWrites).Body).Version));
    }

    [Fact]
    public async Task Remove_asks_inline_and_focus_goes_to_Keep()
    {
        var sheet = await _f.Opened(ProjectSheetFixture.Attached, Token);
        var focus = 0;
        sheet.KeepFocusRequested += (_, _) => focus++;

        sheet.AskRemove();

        Assert.Equal((true, "Remove Mobile app from your page?"), (sheet.IsConfirmingRemove, sheet.RemoveQuestion));
        Assert.Equal(1, focus);
        Assert.Empty(_f.Server.ProjectWrites);
    }

    [Fact]
    public async Task Keep_closes_the_question_and_writes_nothing()
    {
        var sheet = await _f.Opened(ProjectSheetFixture.Attached, Token);
        sheet.AskRemove();

        sheet.Keep();

        Assert.Equal((false, 0), (sheet.IsConfirmingRemove, _f.Server.ProjectWrites.Count));
    }

    [Fact]
    public async Task Remove_detaches_with_the_version_and_says_it_is_done()
    {
        var sheet = await _f.Opened(ProjectSheetFixture.Attached, Token);
        var done = new List<ProjectDoneEventArgs>();
        sheet.Done += (_, e) => done.Add(e);
        sheet.AskRemove();

        await sheet.Remove(Token);

        var (route, body) = Assert.Single(_f.Server.ProjectWrites);
        Assert.Equal((ProjectRoutes.Detach(ProjectSheetFixture.Attached), new ProjectDetach(Version: 1)), (route, body));
        var e = Assert.Single(done);
        Assert.Equal((ProjectDone.Removed, ProjectSheetFixture.Attached, "Mobile app removed"), (e.Kind, e.Id, e.Said));
    }

    [Fact]
    public async Task A_refused_remove_keeps_the_sheet_and_shows_why()
    {
        _f.Server.ProjectWriteAnswer = RouteAnswer.Problem(HttpStatusCode.NotFound, "SND-PROJECT-404", "No project.");
        var sheet = await _f.Opened(ProjectSheetFixture.Attached, Token);
        var done = new List<ProjectDoneEventArgs>();
        sheet.Done += (_, e) => done.Add(e);
        sheet.AskRemove();

        await sheet.Remove(Token);

        Assert.Empty(done);
        Assert.Contains("SND-PROJECT-404", sheet.SaveError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Hide_sends_the_led_key_and_the_version_and_says_it_is_done()
    {
        _f.Serve(ProjectSheetFixture.Led, c => (c["origin"], c["name"], c["version"]) = ("led", "Checkout rewrite", 0));
        var sheet = await _f.Opened(ProjectSheetFixture.Led, Token);
        var done = new List<ProjectDoneEventArgs>();
        sheet.Done += (_, e) => done.Add(e);

        await sheet.Hide(Token);

        Assert.Equal((ProjectRoutes.Hide, new ProjectHide(ProjectSheetFixture.Led, Hidden: true, Version: 0)), Assert.Single(_f.Server.ProjectWrites));
        var e = Assert.Single(done);
        Assert.Equal((ProjectDone.Hidden, ProjectSheetFixture.Led, "Checkout rewrite hidden"), (e.Kind, e.Id, e.Said));
    }

    [Theory]
    [InlineData("inProgress", false)]
    [InlineData("done", true)]
    [InlineData("canceled", true)]
    public async Task Unhide_sends_the_hide_rows_version_and_says_where_the_card_went(string state, bool finished)
    {
        _f.Serve(ProjectSheetFixture.Led, c => (c["origin"], c["state"], c["version"]) = ("led", state, 1), one => one["hidden"] = true);
        var sheet = await _f.Opened(ProjectSheetFixture.Led, Token);
        var done = new List<ProjectDoneEventArgs>();
        sheet.Done += (_, e) => done.Add(e);

        await sheet.Unhide(Token);

        Assert.Equal((ProjectRoutes.Hide, new ProjectHide(ProjectSheetFixture.Led, Hidden: false, Version: 1)), Assert.Single(_f.Server.ProjectWrites));
        var e = Assert.Single(done);
        Assert.Equal((ProjectDone.Unhidden, finished), (e.Kind, e.WentToFinished));
    }

    [Fact]
    public async Task An_unknown_project_is_the_whole_sheet()
    {
        _f.Server.ProjectAnswers[ProjectSheetFixture.Attached] = RouteAnswer.Problem(HttpStatusCode.NotFound, "SND-PROJECT-404", "No project.");

        var sheet = await _f.Opened(ProjectSheetFixture.Attached, Token);

        Assert.Equal((FramePhase.NotFound, true, "Sindri has no such project."), (sheet.Phase, sheet.IsProblem, sheet.ProblemText));
    }

    [Fact]
    public void Showing_a_subject_names_the_project_before_any_read()
    {
        _f.Sheet.Show(ProjectSheetFixture.Subject(ProjectSheetFixture.Led));

        Assert.Equal((ProjectSheetFixture.Led, FramePhase.Loading), (_f.Sheet.Id, _f.Sheet.Phase));
    }
}
