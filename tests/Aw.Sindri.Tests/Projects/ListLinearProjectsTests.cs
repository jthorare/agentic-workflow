using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Projects.Facts;
using Aw.Sindri.Core.Projects.Vocabulary;
using Aw.Sindri.Features.ListLinearProjects;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Projects;

/// <summary>ListLinearProjects (projects page spec §3.2, §6.2): the From Linear tab's rows are the stored catalog minus what the owner leads, has attached or is finished.</summary>
[Lane(TestLane.Unit)]
public sealed class ListLinearProjectsTests
{
    private const string Other = "linear:project/00000000-0000-4000-8000-0000000000a1";
    private const string Second = "linear:project/00000000-0000-4000-8000-0000000000a2";
    private const string Third = "linear:project/00000000-0000-4000-8000-0000000000a3";

    private readonly ProjectWriteHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ListLinearProjectsService Service() => new();

    private static LinearProjectFact Fact(string key, string title, string state = "started", string? url = "https://linear.app/acme/project/x", bool leadIsOwner = false, DateTimeOffset? closed = null) =>
        ProjectRows.Fact(key, url, leadIsOwner) with { Title = title, StateRaw = state, ClosedAt = closed, Lead = new ProjectPerson("linear:user/ana", "Ana Ruiz"), OpenIssues = 6 };

    [Fact]
    public async Task Without_a_caller_the_tab_is_refused()
    {
        var result = await Service().Handle(new ListLinearProjectsRequest(), Token);

        Expect.Equal(SindriErrors.AuthRefused.Code, result.Error.Code);
    }

    [Fact]
    public async Task The_owners_led_attached_and_finished_projects_are_left_out()
    {
        await _h.SeedAttached();
        _ = _h.Facts.With(ProjectRows.Snapshot(
            Fact(ProjectRows.LinearKey, "Attached one"),
            Fact(Other, "Led one", leadIsOwner: true),
            Fact(Second, "Done one", state: "completed"),
            Fact(Third, "Dropped one", state: "canceled", closed: KnownInstants.Epoch),
            Fact("linear:project/00000000-0000-4000-8000-0000000000a4", "Kept")));
        using var entered = _h.Human();

        var result = await Service().Handle(new ListLinearProjectsRequest(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Expect.Sequence(["Kept"], [.. result.Value.Projects.Select(static p => p.Name)]);
        Assert.Null(result.Value.Error);
        Expect.Equal(_h.Views.Progress.Current.AsOf, result.Value.AsOf);
        Expect.Sequence([ProjectRows.LinearKey], [.. _h.Facts.Asked.Single()]);
    }

    [Fact]
    public async Task A_project_with_a_close_date_is_finished_whatever_its_status()
    {
        _ = _h.Facts.With(ProjectRows.Snapshot(Fact(Other, "Closed", state: "started", closed: KnownInstants.Epoch), Fact(Second, "Open")));
        using var entered = _h.Human();

        var result = await Service().Handle(new ListLinearProjectsRequest(), Token);

        Expect.Sequence(["Open"], [.. result.Value.Projects.Select(static p => p.Name)]);
    }

    [Fact]
    public async Task Rows_are_ordered_by_name_ignoring_case()
    {
        _ = _h.Facts.With(ProjectRows.Snapshot(Fact(Other, "mobile push"), Fact(Second, "Billing"), Fact(Third, "api cleanup")));
        using var entered = _h.Human();

        var result = await Service().Handle(new ListLinearProjectsRequest(), Token);

        Expect.Sequence(["api cleanup", "Billing", "mobile push"], [.. result.Value.Projects.Select(static p => p.Name)]);
    }

    [Fact]
    public async Task A_row_carries_the_key_lead_state_issue_count_and_url()
    {
        _ = _h.Facts.With(ProjectRows.Snapshot(Fact(Other, "Mobile push settings", url: "https://linear.app/acme/project/mobile-push-a1")));
        using var entered = _h.Human();

        var result = await Service().Handle(new ListLinearProjectsRequest(), Token);

        var row = Assert.Single(result.Value.Projects);
        Expect.Equal(new LinearProjectChoice(Other, "Mobile push settings", "Ana Ruiz", ProjectState.InProgress, 6, "https://linear.app/acme/project/mobile-push-a1"), row);
    }

    [Theory]
    [InlineData("http://linear.app/acme/project/x")]
    [InlineData("https://evil.example/acme/project/x")]
    [InlineData(null)]
    public async Task A_url_a_result_may_not_carry_becomes_null(string? url)
    {
        _ = _h.Facts.With(ProjectRows.Snapshot(Fact(Other, "Mobile push settings", url: url)));
        using var entered = _h.Human();

        var result = await Service().Handle(new ListLinearProjectsRequest(), Token);

        Assert.Null(Assert.Single(result.Value.Projects).Url);
    }

    [Fact]
    public async Task A_project_without_a_lead_has_a_null_lead_and_an_unknown_status_has_no_state()
    {
        _ = _h.Facts.With(ProjectRows.Snapshot(Fact(Other, "Loose", state: "mystery") with { Lead = null }));
        using var entered = _h.Human();

        var result = await Service().Handle(new ListLinearProjectsRequest(), Token);

        var row = Assert.Single(result.Value.Projects);
        Assert.Null(row.Lead);
        Assert.Null(row.State);
    }

    [Fact]
    public async Task When_Context_fails_the_rows_are_empty_and_the_error_is_its_first_line()
    {
        _ = _h.Facts.Failing(SindriErrors.ProjectFactsUnavailable.With("Context is not answering\nsecond line"));
        using var entered = _h.Human();

        var result = await Service().Handle(new ListLinearProjectsRequest(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Assert.Empty(result.Value.Projects);
        Expect.Equal("Context is not answering", result.Value.Error);
    }
}
