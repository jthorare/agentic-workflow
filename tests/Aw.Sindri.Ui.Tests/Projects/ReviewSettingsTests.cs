using System.Net;
using System.Text;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Reviews;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Projects;

namespace Aw.Sindri.Ui.Tests.Projects;

/// <summary>The project sheet's Review settings (review page spec §7, Ph9): what it shows, what a save sends, and how the server's refusals land.</summary>
[Lane(TestLane.Unit)]
public sealed class ReviewSettingsTests
{
    private readonly ProjectSheetFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ReviewSettingsViewModel> Opened(string scenario = "default-only")
    {
        _f.Server.ReviewSettingsAnswer = RouteAnswer.Api("review-settings", scenario);
        return (await _f.Opened(ProjectSheetFixture.Attached, Token)).ReviewSettings;
    }

    private ReviewSettingsSave OnlySave() => Assert.Single(_f.Server.ReviewSettingsSaves);

    [Fact]
    public async Task The_sheet_reads_the_settings_of_the_project_it_opened()
    {
        var settings = await Opened();

        Assert.Equal([ProjectSheetFixture.Attached], _f.Server.ReviewSettingsReads);
        Assert.True(settings.IsShown);
    }

    [Fact]
    public async Task An_unreadable_answer_leaves_the_section_hidden()
    {
        _f.Server.ReviewSettingsAnswer = RouteAnswer.Problem(HttpStatusCode.BadRequest, "SND-REVIEW-002", "no such project");

        var sheet = await _f.Opened(ProjectSheetFixture.Attached, Token);

        Assert.False(sheet.ReviewSettings.IsShown);
        Assert.True(sheet.IsShown);
    }

    [Fact]
    public async Task The_entry_is_named_and_each_key_says_where_it_comes_from()
    {
        var settings = await Opened("extends-sindri");

        Assert.Equal(Labels.ReviewSettingsEntryLine("sindri"), settings.EntryLine);
        Assert.Equal(
            [Labels.ReviewSettingsKeyLine(Labels.ReviewSettingsPanels, Labels.ReviewSettingsFromEntry), Labels.ReviewSettingsKeyLine(Labels.ReviewSettingsChecks, Labels.ReviewSettingsFromDefault)],
            [settings.PanelsKey.HeaderText, settings.ChecksKey.HeaderText],
            StringComparer.Ordinal);
    }

    [Fact]
    public async Task With_no_entry_every_key_is_from_default()
    {
        var settings = await Opened();

        Assert.Equal(Labels.ReviewSettingsEntryLine("default"), settings.EntryLine);
        ReviewSettingsKey[] keys = [settings.PanelsKey, settings.GroupsKey, settings.CollapseKey, settings.RiskyKey, settings.ChecksKey];
        Assert.All(keys, k => Assert.Equal(Labels.ReviewSettingsFromDefault, k.FromText));
        Assert.True(settings.ShowGroupsByFolder);
    }

    [Fact]
    public async Task Projects_sharing_the_entry_are_named_and_none_says_nothing()
    {
        var settings = await Opened("extends-sindri");
        Assert.Equal(string.Empty, settings.SharedText);

        _f.Server.ReviewSettingsAnswer = RouteAnswer.Api("review-settings", "shared-entry");
        await _f.Sheet.Refresh(ProjectsFixture.Url, Token);

        Assert.Equal(Labels.ReviewSettingsSharedWith("prj_0000000000000000000000000K"), settings.SharedText);
    }

    [Fact]
    public async Task Shown_panels_come_first_in_their_order_and_the_rest_are_hidden()
    {
        var settings = await Opened("extends-sindri");

        Assert.Equal(["requiredChecks", "docMap", "ci", "risk", "conversation", "testPlan", "evidence"], settings.PanelRows.Select(p => p.Id), StringComparer.Ordinal);
        Assert.Equal([true, true, true, true, true, false, false], settings.PanelRows.Select(p => p.Visible));
    }

    [Fact]
    public async Task Moving_and_hiding_a_panel_changes_the_panels_the_save_sends()
    {
        var settings = await Opened("extends-sindri");

        settings.MovePanel(settings.PanelRows[1], -1);
        settings.PanelRows.First(p => string.Equals(p.Id, "risk", StringComparison.Ordinal)).Visible = false;
        await settings.Save(Token);

        Assert.Equal(["docMap", "requiredChecks", "ci", "conversation"], OnlySave().Entry.Panels, StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_panel_cannot_move_off_either_end()
    {
        var settings = await Opened("extends-sindri");
        var first = settings.PanelRows[0];

        settings.MovePanel(first, -1);
        settings.MovePanel(settings.PanelRows[^1], 1);

        Assert.Same(first, settings.PanelRows[0]);
        Assert.False(settings.HasChanges);
    }

    [Fact]
    public async Task Save_and_approve_sends_approve_true_with_the_project_repos_and_entry_name()
    {
        var settings = await Opened();

        await settings.Save(Token);

        var save = OnlySave();
        Assert.Equal((ProjectSheetFixture.Attached, "default", true), (save.Project, save.Settings, save.Approve));
        Assert.Equal(["jthorare/agentic-workflow"], save.Repos, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Keys_left_at_their_inherited_value_are_not_written_into_the_entry()
    {
        var settings = await Opened();

        await settings.Save(Token);

        Assert.Equal(new ReviewSettingsEntry(), OnlySave().Entry);
    }

    [Fact]
    public async Task Changing_one_inherited_key_writes_only_that_key()
    {
        var settings = await Opened();
        settings.CollapseText = "a/**\n b/**";

        await settings.Save(Token);

        var entry = OnlySave().Entry;
        Assert.Equal(["a/**", "b/**"], entry.Collapse, StringComparer.Ordinal);
        Assert.Null(entry.Extends);
        Assert.Null(entry.Panels);
        Assert.Null(entry.Groups);
        Assert.Null(entry.Risky);
        Assert.Null(entry.Checks);
    }

    [Fact]
    public async Task Keys_the_entry_already_sets_are_sent_as_they_are_and_the_others_are_not()
    {
        var settings = await Opened("extends-sindri");

        await settings.Save(Token);

        var entry = OnlySave().Entry;
        Assert.Equal(("default", 5, 4, 3, 2, null), (entry.Extends, entry.Panels!.Count, entry.Groups!.Count, entry.Collapse!.Count, entry.Risky!.Count, entry.Checks?.Count));
    }

    [Fact]
    public async Task Reset_to_default_leaves_a_key_the_entry_set_out_of_the_save()
    {
        var settings = await Opened("extends-sindri");
        settings.CollapseText = "changed/**";

        settings.Reset(settings.CollapseKey);
        await settings.Save(Token);

        var entry = OnlySave().Entry;
        Assert.True(settings.CollapseKey.IsReset);
        Assert.Null(entry.Collapse);
        Assert.NotNull(entry.Panels);
    }

    [Fact]
    public async Task Reset_on_an_inherited_key_puts_back_the_value_that_was_read()
    {
        var settings = await Opened();
        var read = settings.CollapseText;
        settings.CollapseText = "changed/**";

        settings.Reset(settings.CollapseKey);
        await settings.Save(Token);

        Assert.Equal(read, settings.CollapseText);
        Assert.Null(OnlySave().Entry.Collapse);
    }

    [Fact]
    public async Task Added_groups_risky_paths_and_checks_are_sent_and_blank_rows_are_not()
    {
        var settings = await Opened();
        settings.AddGroup();
        settings.GroupRows[0].Name = "Docs";
        settings.GroupRows[0].GlobsText = "docs/**\n**/*.md";
        settings.AddGroup();
        settings.AddRisk();
        settings.RiskRows[0].GlobsText = ".github/workflows/**";
        settings.RiskRows[0].Why = "CI definition";
        settings.AddRisk();
        settings.AddCheck();
        settings.CheckRows[0].Id = "ran-it";
        settings.CheckRows[0].Title = "I ran it myself";
        settings.CheckRows[0].NeedText = "manual";

        await settings.Save(Token);

        var entry = OnlySave().Entry;
        Assert.Equal("Docs", entry.Groups!.Single().Name);
        Assert.Equal(["docs/**", "**/*.md"], entry.Groups!.Single().Globs, StringComparer.Ordinal);
        Assert.Equal(("CI definition", 1), (entry.Risky!.Single().Why, entry.Risky!.Count));
        Assert.Equal(("ran-it", "{\"manual\":true}"), (entry.Checks!.Single().Id, entry.Checks!.Single().Need!.Value.GetRawText()));
    }

    [Fact]
    public async Task Removing_a_row_renumbers_the_ones_after_it()
    {
        var settings = await Opened("extends-sindri");

        settings.RemoveGroup(settings.GroupRows[0]);

        Assert.Equal([1, 2, 3], settings.GroupRows.Select(g => g.Index));
        Assert.Equal(["Features", "Tests", "Lockfiles"], settings.GroupRows.Select(g => g.Name), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_check_left_as_it_was_keeps_the_need_the_api_wrote()
    {
        var settings = await Opened("shared-entry");
        settings.CheckRows[0].Title = "Renamed";

        await settings.Save(Token);

        Assert.Equal(["{\"ci\":\"ui-tests\"}", "{\"manual\":true}"], OnlySave().Entry.Checks!.Select(c => c.Need!.Value.GetRawText()), StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("ci: ui-tests", "{\"ci\":\"ui-tests\"}")]
    [InlineData("changedWith: **/packages.lock.json, a/**", "{\"changedWith\":[\"**/packages.lock.json\",\"a/**\"]}")]
    [InlineData("covers", "{\"covers\":true}")]
    [InlineData("body: ^reviewed by ideas$", "{\"body\":\"^reviewed by ideas$\"}")]
    public void A_need_is_written_in_a_line_and_read_back(string text, string json)
    {
        Assert.Equal(json, ReviewSettingsNeed.Parse(text)!.Value.GetRawText());
        Assert.Equal(text, ReviewSettingsNeed.Text(ReviewSettingsNeed.Parse(text)), StringComparer.Ordinal);
    }

    [Fact]
    public void A_ci_need_with_an_app_keeps_the_app_through_text_and_back()
    {
        var need = ReviewSettingsNeed.Parse("ci: ui-tests (app: github-actions)");

        Assert.Equal("""{"ci":"ui-tests","app":"github-actions"}""", need!.Value.GetRawText());
        Assert.Equal("ci: ui-tests (app: github-actions)", ReviewSettingsNeed.Text(need));
    }

    [Fact]
    public async Task A_check_with_ci_and_app_is_sent_back_with_both_and_a_command_check_unchanged()
    {
        var settings = await Opened("shared-entry");
        settings.AddCheck();
        settings.CheckRows[2].Id = "withapp";
        settings.CheckRows[2].NeedText = "ci: build (app: gha)";

        await settings.Save(Token);

        Assert.Equal("""{"ci":"build","app":"gha"}""", OnlySave().Entry.Checks![2].Need!.Value.GetRawText());
    }

    [Fact]
    public async Task A_command_check_shows_read_only_and_goes_back_as_it_was()
    {
        var settings = await Opened("shared-entry");
        var command = ReviewSettingsNeed.Parse("manual")!.Value;
        Assert.False(ReviewSettingsNeed.IsCommand(command));
        using var document = System.Text.Json.JsonDocument.Parse("""{"command":["make","test"]}""");
        var row = new SettingsCheckRow(3, "cmd", "Runs make", string.Empty, ReviewSettingsNeed.Text(document.RootElement), document.RootElement.Clone());

        Assert.Equal(("command: make, test", true), (row.NeedText, row.NeedReadOnly));
        Assert.True(settings.IsShown);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ci")]
    [InlineData("shell: make")]
    [InlineData("manual: maybe")]
    public void A_need_that_is_none_of_the_five_is_not_read(string text) => Assert.Null(ReviewSettingsNeed.Parse(text));

    [Fact]
    public async Task A_need_that_cannot_be_read_stops_the_save_and_says_so_under_the_checks()
    {
        var settings = await Opened();
        settings.AddCheck();
        settings.CheckRows[0].Id = "bad";
        settings.CheckRows[0].NeedText = "make test";

        await settings.Save(Token);

        Assert.Empty(_f.Server.ReviewSettingsSaves);
        Assert.Equal(Labels.ReviewSettingsNeedBad("bad"), settings.ChecksKey.Error);
    }

    [Fact]
    public async Task An_approved_save_says_so_and_reads_the_settings_again()
    {
        var settings = await Opened();
        settings.CollapseText = "a/**";

        await settings.Save(Token);

        Assert.Equal((Labels.ReviewSettingsSavedApproved, string.Empty, false), (settings.Announcement, settings.Note, settings.HasChanges));
        Assert.Equal(2, _f.Server.ReviewSettingsReads.Count);
    }

    [Fact]
    public async Task A_save_that_waits_for_other_profile_changes_says_to_approve_in_Settings()
    {
        _f.Server.ReviewSettingsSaveAnswer = RouteAnswer.Api("review-settings-save", "pending-others");
        var settings = await Opened();

        await settings.Save(Token);

        Assert.Equal((Labels.ReviewSettingsPending(2), string.Empty), (settings.Note, settings.Announcement));
        Assert.Contains(Labels.ReviewsLoginPending, settings.Note, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("bad-panels", "panels")]
    [InlineData("bad-group-globs", "groups")]
    [InlineData("bad-extends", "extends")]
    [InlineData("bad-repo", "repos")]
    [InlineData("bad-settings-name", "settings")]
    public async Task A_400_puts_its_detail_under_the_field_it_names_and_nowhere_else(string scenario, string field)
    {
        _f.Server.ReviewSettingsSaveAnswer = RouteAnswer.Problem("review-settings-save", scenario);
        var settings = await Opened("extends-sindri");
        var detail = GoldenFiles.Node("review-settings-save", scenario)["detail"]!.GetValue<string>();

        await settings.Save(Token);

        var shown = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["panels"] = settings.PanelsKey.Error,
            ["groups"] = settings.GroupsKey.Error,
            ["extends"] = settings.ExtendsError,
            ["repos"] = settings.ReposError,
            ["settings"] = settings.NameError,
        };
        Assert.Equal(detail, shown[field]);
        Assert.All(shown.Where(s => !string.Equals(s.Key, field, StringComparison.Ordinal)), s => Assert.Equal(string.Empty, s.Value));
        Assert.Equal(string.Empty, settings.SaveError);
        Assert.True(settings.CanChange);
    }

    [Fact]
    public async Task The_save_sends_the_hash_the_settings_were_read_at()
    {
        var settings = await Opened();

        await settings.Save(Token);

        Assert.Equal("sha256:4f1c0b7e9a52d3", OnlySave().IfHash);
    }

    [Fact]
    public async Task A_409_says_the_profile_changed_with_no_field_and_Reload_drops_the_edits()
    {
        _f.Server.ReviewSettingsSaveAnswer = RouteAnswer.Problem("review-settings-save", "stale-hash");
        var settings = await Opened();
        var read = settings.CollapseText;
        settings.CollapseText = "mine/**";

        await settings.Save(Token);

        Assert.Equal((true, string.Empty, true, "mine/**"), (settings.IsStale, settings.SaveError, settings.CanChange, settings.CollapseText));
        Assert.Equal(string.Empty, settings.CollapseKey.Error);

        await settings.Discard(Token);

        Assert.Equal((false, read, false), (settings.IsStale, settings.CollapseText, settings.HasChanges));
        Assert.Equal(2, _f.Server.ReviewSettingsReads.Count);
    }

    [Fact]
    public async Task An_unknown_project_is_said_under_the_buttons()
    {
        _f.Server.ReviewSettingsSaveAnswer = RouteAnswer.Problem("review-settings-save", "unknown-project");
        var settings = await Opened();

        await settings.Save(Token);

        Assert.Contains("is not on the Projects page", settings.SaveError, StringComparison.Ordinal);
        Assert.Contains("SND-REVIEW-002", settings.SaveError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_403_shows_the_refusal_and_the_settings_turn_read_only()
    {
        _f.Server.ReviewSettingsSaveAnswer = RouteAnswer.Problem("review-settings-save", "session-refused");
        var settings = await Opened("session");
        settings.CollapseText = "a/**";

        await settings.Save(Token);
        await settings.Save(Token);
        settings.AddGroup();

        Assert.Contains("SND-AUTH-002", settings.SaveError, StringComparison.Ordinal);
        Assert.Equal((false, true, 1), (settings.CanChange, settings.IsReadOnly, _f.Server.ReviewSettingsSaves.Count));
        Assert.Equal(4, settings.GroupRows.Count);
    }

    [Fact]
    public async Task A_refresh_of_the_sheet_does_not_overwrite_what_is_being_edited()
    {
        var settings = await Opened();
        settings.CollapseText = "mine/**";

        await _f.Sheet.Refresh(ProjectsFixture.Url, Token);

        Assert.Equal("mine/**", settings.CollapseText);
        _ = Assert.Single(_f.Server.ReviewSettingsReads);
    }

    [Fact]
    public async Task Discard_drops_the_edits_and_reads_again()
    {
        var settings = await Opened();
        var read = settings.CollapseText;
        settings.CollapseText = "mine/**";

        await settings.Discard(Token);

        Assert.Equal((read, false, 2), (settings.CollapseText, settings.HasChanges, _f.Server.ReviewSettingsReads.Count));
    }

    [Fact]
    public async Task Another_project_forgets_the_first_ones_settings()
    {
        var settings = await Opened("extends-sindri");
        _f.Serve(ProjectSheetFixture.Led, c => c["origin"] = "led");

        _f.Sheet.Show(ProjectSheetFixture.Subject(ProjectSheetFixture.Led));

        Assert.False(settings.IsShown);
    }

    /// <summary>The wire body of the save: only what the owner changed, null keys left out, camelCase names.</summary>
    [Fact]
    public async Task The_request_body_holds_only_the_keys_that_are_sent()
    {
        var settings = await Opened();
        settings.CollapseText = "a/**";

        await settings.Save(Token);

        var body = Encoding.UTF8.GetString(AwJson.Serialize(OnlySave(), SindriApiClient.Json));
        Assert.Equal(
            """{"project":"prj_0000000000000000000000000H","repos":["jthorare/agentic-workflow"],"settings":"default","entry":{"collapse":["a/**"]},"approve":true,"ifHash":"sha256:4f1c0b7e9a52d3"}""",
            body);
    }

    [Fact]
    public async Task An_entry_with_groups_and_checks_writes_groups_and_need_as_the_api_reads_them()
    {
        var settings = await Opened("shared-entry");

        await settings.Save(Token);

        var body = Encoding.UTF8.GetString(AwJson.Serialize(OnlySave().Entry, SindriApiClient.Json));
        Assert.Contains("""{"name":"Lockfiles","globs":["**/packages.lock.json"]}""", body, StringComparison.Ordinal);
        Assert.Contains("""{"id":"owner-ran-it","title":"I ran it myself","need":{"manual":true}}""", body, StringComparison.Ordinal);
    }
}
