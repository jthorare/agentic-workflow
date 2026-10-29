using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Projects;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Projects;

namespace Aw.Sindri.Ui.Tests.Projects;

/// <summary>The Add project sheet's By hand tab (projects page spec §3.2): when Save is allowed, what it sends, and what a refusal or a bad date leaves behind.</summary>
[Lane(TestLane.Unit)]
public sealed class ProjectAddTests
{
    private readonly ProjectsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ProjectAddViewModel Add() => new(_f.Server, () => ProjectsFixture.Url);

    private ProjectAttach Sent() => Assert.IsType<ProjectAttach>(Assert.Single(_f.Server.ProjectWrites).Body);

    [Fact]
    public void The_sheet_opens_on_By_hand_and_the_tabs_switch()
    {
        var add = Add();

        Assert.Equal((true, false), (add.IsByHand, add.IsFromLinear));
        add.SelectFromLinear();
        Assert.Equal((false, true), (add.IsByHand, add.IsFromLinear));
        add.SelectByHand();
        Assert.Equal((true, false), (add.IsByHand, add.IsFromLinear));
    }

    [Fact]
    public void Save_waits_for_a_name()
    {
        var add = Add();
        Assert.False(add.CanSave);

        add.Name = "   ";
        Assert.False(add.CanSave);

        add.Name = "Kitchen";
        Assert.True(add.CanSave);
    }

    [Fact]
    public async Task Save_with_a_blank_name_sends_nothing()
    {
        var add = Add();

        await add.Save(Token);

        Assert.Empty(_f.Server.ProjectWrites);
    }

    [Fact]
    public async Task Save_sends_the_fields_and_leaves_out_the_empty_ones()
    {
        var add = Add();
        add.Name = "  Kitchen  ";

        await add.Save(Token);

        var sent = Sent();
        Assert.Equal(("/sindri/projects", null, "Kitchen", "planned", null, null, null), (_f.Server.ProjectWrites[0].Route, sent.Linear, sent.Name, sent.State, sent.Links, sent.Notes, sent.Target));
    }

    [Fact]
    public async Task Save_sends_the_picked_state_the_links_one_per_line_the_notes_and_the_target()
    {
        var add = Add();
        add.Name = "Kitchen";
        add.SelectedState = add.StateChoices.Single(s => string.Equals(s.Value, "paused", StringComparison.Ordinal));
        add.Links = "https://example.com/a\n\n  https://example.com/b  \n";
        add.Notes = "Waiting on the vendor";
        add.Target = " 2026-02-03 ";

        await add.Save(Token);

        var sent = Sent();
        Assert.Equal(("paused", "Waiting on the vendor", "2026-02-03"), (sent.State, sent.Notes, sent.Target));
        Assert.Equal(["https://example.com/a", "https://example.com/b"], sent.Links);
    }

    [Fact]
    public async Task Save_shows_Saving_and_is_disabled_while_the_write_is_in_flight()
    {
        var add = Add();
        add.Name = "Kitchen";
        _f.Server.CommandGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var saving = add.Save(Token);

        Assert.Equal((true, "Saving…", false), (add.IsSaving, add.SaveText, add.CanSave));
        _f.Server.CommandGate.SetResult();
        await saving;
        Assert.Equal((false, "Save", true), (add.IsSaving, add.SaveText, add.CanSave));
    }

    [Fact]
    public async Task A_refusal_shows_under_the_button_and_keeps_every_field()
    {
        _f.Server.ProjectWriteAnswer = RouteAnswer.Problem(HttpStatusCode.BadRequest, "SND-PROJECT-004", "links: more than 10 links");
        var add = Add();
        (add.Name, add.Links, add.Notes, add.Target) = ("Kitchen", "https://example.com/a", "n", "2026-02-03");
        var raised = new List<ProjectDoneEventArgs>();
        add.Added += (_, e) => raised.Add(e);

        await add.Save(Token);

        Assert.Contains("SND-PROJECT-004", add.SaveError, StringComparison.Ordinal);
        Assert.Equal(("Kitchen", "https://example.com/a", "n", "2026-02-03", "planned", false), (add.Name, add.Links, add.Notes, add.Target, add.SelectedState?.Value, add.IsSaving));
        Assert.Empty(raised);
    }

    [Theory]
    [InlineData("tomorrow")]
    [InlineData("2026-13-01")]
    [InlineData("03/02/2026")]
    public async Task A_target_that_is_not_a_date_is_refused_without_a_call(string target)
    {
        var add = Add();
        (add.Name, add.Target) = ("Kitchen", target);

        await add.Save(Token);

        Assert.Empty(_f.Server.ProjectWrites);
        Assert.Equal(("Target date must look like 2026-01-31.", "Kitchen", target), (add.SaveError, add.Name, add.Target));
    }

    [Fact]
    public async Task A_new_save_clears_the_last_error()
    {
        _f.Server.ProjectWriteAnswer = RouteAnswer.Problem(HttpStatusCode.BadRequest, "SND-PROJECT-004", "no");
        var add = Add();
        add.Name = "Kitchen";
        await add.Save(Token);
        Assert.NotEmpty(add.SaveError);

        _f.Server.ProjectWriteAnswer = null;
        await add.Save(Token);

        Assert.Empty(add.SaveError);
    }

    [Fact]
    public async Task Success_raises_Added_with_the_new_projects_id_and_the_sentence()
    {
        var add = Add();
        add.Name = "Kitchen";
        var raised = new List<ProjectDoneEventArgs>();
        add.Added += (_, e) => raised.Add(e);

        await add.Save(Token);

        var added = Assert.Single(raised);
        Assert.Equal((ProjectDone.Added, "prj_00000000000000000000000001", "Kitchen added", false), (added.Kind, added.Id, added.Said, added.WentToFinished));
    }

    [Fact]
    public async Task Reset_starts_the_sheet_over_on_By_hand_with_empty_fields()
    {
        var add = Add();
        _f.Server.ProjectWriteAnswer = RouteAnswer.Problem(HttpStatusCode.BadRequest, "SND-PROJECT-004", "no");
        (add.Name, add.Links, add.Notes, add.Target) = ("Kitchen", "https://example.com/a", "n", "2026-02-03");
        add.SelectedState = add.StateChoices[0];
        await add.Save(Token);
        add.SelectFromLinear();

        add.Reset();

        Assert.Equal((true, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, "planned"), (add.IsByHand, add.Name, add.Links, add.Notes, add.Target, add.SaveError, add.SelectedState?.Value));
    }

    [Fact]
    public void The_sheets_link_round_trips_and_has_no_subject()
    {
        Assert.True(DeepLink.TryParse("projects/add", out var link));

        Assert.Equal((DeepLinkKind.ProjectAdd, null, "projects/add"), (link.Kind, link.Subject, link.ToString()));
        Assert.Equal(DeepLink.ProjectAdd, link);
    }

    [Theory]
    [InlineData("projects/add/x")]
    [InlineData("projects/other")]
    [InlineData("projects/")]
    [InlineData("project/add")]
    public void Other_texts_under_projects_are_not_the_sheets_link(string text) =>
        Assert.False(DeepLink.TryParse(text, out var link) && link.Kind == DeepLinkKind.ProjectAdd);
}
