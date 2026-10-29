using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Projects;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Projects;

namespace Aw.Sindri.Ui.Tests.Projects;

/// <summary>The Add project sheet's From Linear tab (projects page spec §3.2): the list it reads once per opening, the search, and Attach.</summary>
[Lane(TestLane.Unit)]
public sealed class ProjectAddLinearTests
{
    private const string FirstKey = "linear:project/00000000-0000-4000-8000-0000000000a3";

    private readonly ProjectsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ProjectAddViewModel Add() => new(_f.Server, () => ProjectsFixture.Url);

    private async Task<ProjectAddViewModel> Opened()
    {
        var add = Add();
        add.Reset();
        await add.LinearLoad;
        return add;
    }

    private static string[] Names(ProjectAddViewModel add) => [.. add.LinearRows.Select(r => r.Name)];

    [Fact]
    public async Task The_tab_says_Loading_while_it_reads_and_then_shows_the_rows()
    {
        _f.Server.LinearGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var add = Add();

        add.Reset();

        Assert.Equal(("Loading…", 0, string.Empty), (add.LinearStatus, add.LinearRows.Count, add.LinearError));
        _f.Server.LinearGate.SetResult();
        await add.LinearLoad;
        Assert.Equal((string.Empty, string.Empty), (add.LinearStatus, add.LinearError));
        Assert.Equal(["api cleanup", "Billing exports", "Mobile push settings"], Names(add));
    }

    [Fact]
    public async Task A_row_says_the_lead_the_state_and_the_open_issues_and_leaves_out_what_is_null()
    {
        var add = await Opened();

        Assert.Equal(
            ["Lead: Ana Ruiz · Backlog · 2 open issues", "Planned · 0 open issues", "Lead: Ana Ruiz · In progress · 6 open issues"],
            add.LinearRows.Select(r => r.Detail),
            StringComparer.Ordinal);
        Assert.Equal("Attach api cleanup", add.LinearRows[0].AttachName);
    }

    [Fact]
    public void One_open_issue_is_singular() =>
        Assert.Equal("Lead: Sam · Paused · 1 open issue", Labels.ProjectLinearDetail("Sam", "paused", 1));

    [Fact]
    public async Task An_empty_list_says_so()
    {
        _f.Server.LinearAnswer = RouteAnswer.Api("projects-linear", "empty");

        var add = await Opened();

        Assert.Equal(("No other Linear projects in your teams.", 0), (add.LinearStatus, add.LinearRows.Count));
    }

    [Fact]
    public async Task The_servers_error_line_shows_in_place_of_the_rows()
    {
        _f.Server.LinearAnswer = RouteAnswer.Api("projects-linear", "context-down");

        var add = await Opened();

        Assert.Equal(("Context is not answering", 0, string.Empty), (add.LinearError, add.LinearRows.Count, add.LinearStatus));
    }

    [Fact]
    public async Task A_refused_call_shows_its_error()
    {
        _f.Server.LinearAnswer = RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-API-005", "views are rebuilding");

        var add = await Opened();

        Assert.Contains("SND-API-005", add.LinearError, StringComparison.Ordinal);
        Assert.Empty(add.LinearRows);
    }

    [Fact]
    public async Task An_untrusted_name_is_shown_as_plain_text()
    {
        _f.Server.LinearAnswer = RouteAnswer.Api("projects-linear", "untrusted-name");

        var add = await Opened();

        Assert.StartsWith("<untrusted", Assert.Single(add.LinearRows).Name, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Search_filters_by_name_ignoring_case_without_another_call()
    {
        var add = await Opened();

        add.LinearSearch = "BILLING";
        Assert.Equal(["Billing exports"], Names(add));
        add.LinearSearch = "zzz";
        Assert.Equal((0, "No project matches that search."), (add.LinearRows.Count, add.LinearStatus));
        add.LinearSearch = string.Empty;
        Assert.Equal((3, string.Empty, 1), (add.LinearRows.Count, add.LinearStatus, _f.Server.LinearCalls));
    }

    [Fact]
    public async Task The_list_loads_once_per_opening()
    {
        var add = await Opened();

        add.SelectByHand();
        add.SelectFromLinear();
        await add.LinearLoad;
        Assert.Equal(1, _f.Server.LinearCalls);

        add.Reset();
        await add.LinearLoad;
        Assert.Equal(2, _f.Server.LinearCalls);
    }

    [Fact]
    public async Task Reset_clears_the_search_and_the_rows_of_the_last_opening()
    {
        var add = await Opened();
        add.LinearSearch = "billing";

        _f.Server.LinearGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        add.Reset();

        Assert.Equal((string.Empty, 0, "Loading…"), (add.LinearSearch, add.LinearRows.Count, add.LinearStatus));
    }

    [Fact]
    public async Task Attach_sends_the_key_and_raises_Added_like_Save()
    {
        var add = await Opened();
        var raised = new List<ProjectDoneEventArgs>();
        add.Added += (_, e) => raised.Add(e);

        await add.Attach(add.LinearRows[0], Token);

        var sent = Assert.IsType<ProjectAttach>(Assert.Single(_f.Server.ProjectWrites).Body);
        Assert.Equal((FirstKey, null), (sent.Linear, sent.Name));
        var added = Assert.Single(raised);
        Assert.Equal((ProjectDone.Added, "api cleanup added", false), (added.Kind, added.Said, added.WentToFinished));
    }

    [Fact]
    public async Task Attach_shows_Attaching_on_its_row_and_disables_the_others_while_in_flight()
    {
        var add = await Opened();
        _f.Server.CommandGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var attaching = add.Attach(add.LinearRows[1], Token);

        Assert.Equal(["Attach", "Attaching…", "Attach"], add.LinearRows.Select(r => r.AttachText), StringComparer.Ordinal);
        Assert.Equal([false, false, false], add.LinearRows.Select(r => r.CanAttach));
        _f.Server.CommandGate.SetResult();
        await attaching;
        Assert.Equal([true, true, true], add.LinearRows.Select(r => r.CanAttach));
        Assert.Equal(["Attach", "Attach", "Attach"], add.LinearRows.Select(r => r.AttachText), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_refused_Attach_keeps_the_rows_and_shows_the_error_under_them()
    {
        var add = await Opened();
        _f.Server.ProjectWriteAnswer = RouteAnswer.Problem(HttpStatusCode.NotFound, "SND-PROJECT-404", "no such project");
        var raised = new List<ProjectDoneEventArgs>();
        add.Added += (_, e) => raised.Add(e);

        await add.Attach(add.LinearRows[0], Token);

        Assert.Contains("SND-PROJECT-404", add.LinearError, StringComparison.Ordinal);
        Assert.StartsWith("Couldn't add the project.", add.LinearError, StringComparison.Ordinal);
        Assert.Equal((3, true), (add.LinearRows.Count, add.LinearRows.All(r => r.CanAttach)));
        Assert.Empty(raised);
    }

    [Fact]
    public async Task Pressing_Attach_on_a_row_attaches_it()
    {
        var add = await Opened();
        add.Added += (_, _) => { };

        add.LinearRows[2].RequestAttach();
        await add.LinearAttach;

        Assert.Equal("linear:project/00000000-0000-4000-8000-0000000000a1", Assert.IsType<ProjectAttach>(Assert.Single(_f.Server.ProjectWrites).Body).Linear);
    }
}
