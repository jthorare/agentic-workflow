using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Features.LinearIssues;
using Aw.Sindri.Testing;
using static Aw.Sindri.Tests.LinearIssues.LinearIssueRows;

namespace Aw.Sindri.Tests.LinearIssues;

/// <summary>ListLinearIssues (Linear hub spec §3, Ph3 1a): the Work page's Linear list, its two groups, the filters, the paging and the links on the rows.</summary>
[Lane(TestLane.Unit)]
public sealed class ListLinearIssuesTests
{
    private const string Label = "00000000-0000-4000-8000-0000000000d1";
    private const string Project = "00000000-0000-4000-8000-0000000000b1";
    private const string Cycle = "00000000-0000-4000-8000-0000000000c1";

    private readonly FakeLinearIssueFacts _facts = new FakeLinearIssueFacts().WithSource(Connected(Viewer));
    private readonly FakeLinearItemLinks _links = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ListLinearIssuesService Service() => new(_facts, _links);

    private async Task<ListLinearIssuesResponse> Ask(ListLinearIssuesRequest request)
    {
        var result = await Service().Handle(request, Token);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        return result.Value;
    }

    private static string EngKey(int number) => "ENG-" + number.ToString(CultureInfo.InvariantCulture);

    private static string[] Keys(ListLinearIssuesResponse response) => [.. response.Rows.Select(static r => r.Key)];

    [Theory]
    [InlineData("0")]
    [InlineData("201")]
    [InlineData("x")]
    [InlineData("-1")]
    [InlineData("2.5")]
    public async Task A_limit_outside_1_to_200_is_refused(string limit)
    {
        var result = await Service().Handle(new ListLinearIssuesRequest(Limit: limit), Token);

        Expect.Equal(SindriErrors.InvalidQuery.Code, result.Error.Code);
        Assert.Empty(_facts.Asked);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("100")]
    [InlineData("150")]
    [InlineData("200")]
    public async Task A_limit_from_1_to_200_is_served(string limit)
    {
        _ = _facts.WithIssues(Row("ENG-1", Viewer));

        var response = await Ask(new ListLinearIssuesRequest(Limit: limit));

        Expect.Sequence(["ENG-1"], Keys(response));
    }

    [Theory]
    [InlineData("group", "all")]
    [InlineData("priority", "5")]
    [InlineData("priority", "x")]
    [InlineData("after", "zzz")]
    [InlineData("after", "t1.2")]
    public async Task A_value_outside_its_set_is_refused(string name, string value)
    {
        var request = name switch
        {
            "group" => new ListLinearIssuesRequest(Group: value),
            "priority" => new ListLinearIssuesRequest(Priority: value),
            _ => new ListLinearIssuesRequest(After: value),
        };

        var result = await Service().Handle(request, Token);

        Expect.Equal(SindriErrors.InvalidQuery.Code, result.Error.Code);
    }

    [Fact]
    public async Task The_page_holds_50_rows_when_no_limit_is_given()
    {
        _ = _facts.WithIssues([.. Enumerable.Range(1, 60).Select(n => Row(EngKey(n), Sam, updatedMinutesAgo: n))]);

        var response = await Ask(new ListLinearIssuesRequest(Group: "teams"));

        Expect.Equal(50, response.Rows.Count);
        Assert.NotNull(response.Next);
    }

    [Fact]
    public async Task Without_a_linear_source_the_answer_is_not_connected_and_empty()
    {
        _ = _facts.WithSource(new LinearSourceFacts(Connected: false, Viewer: null, new LinearFeedView("ok", LastSyncedAt: null, Code: null, Reread: null))).WithIssues(Row("ENG-1", Viewer));

        var response = await Ask(new ListLinearIssuesRequest());

        Assert.False(response.Connected);
        Assert.Empty(response.Rows);
        Assert.Null(response.Viewer);
        Expect.Equal(0, response.Total);
        Assert.Empty(_facts.Asked);
    }

    [Fact]
    public async Task While_the_viewer_is_unknown_mine_is_empty_but_connected()
    {
        _ = _facts.WithSource(Connected(viewer: null)).WithIssues(Row("ENG-1", Viewer));

        var response = await Ask(new ListLinearIssuesRequest(Group: "mine"));

        Assert.True(response.Connected);
        Assert.Null(response.Viewer);
        Assert.Empty(response.Rows);
        Expect.Equal(0, response.Total);
    }

    [Fact]
    public async Task While_the_viewer_is_unknown_teams_lists_every_open_issue()
    {
        _ = _facts.WithSource(Connected(viewer: null)).WithIssues(Row("ENG-1", Viewer), Row("ENG-2", Sam, updatedMinutesAgo: 20));

        var response = await Ask(new ListLinearIssuesRequest(Group: "teams"));

        Expect.Sequence(["ENG-1", "ENG-2"], Keys(response));
    }

    [Theory]
    [InlineData("label")]
    [InlineData("project")]
    [InlineData("cycle")]
    [InlineData("state")]
    [InlineData("team")]
    [InlineData("assignee")]
    public async Task An_id_nothing_has_gives_empty_rows_not_an_error(string filter)
    {
        _ = _facts.WithIssues(Row("ENG-1", Viewer, labelId: Label, projectId: Project, cycleId: Cycle));
        const string Unknown = "00000000-0000-4000-8000-0000000000ff";
        var request = filter switch
        {
            "label" => new ListLinearIssuesRequest(Label: Unknown),
            "project" => new ListLinearIssuesRequest(Project: Unknown),
            "cycle" => new ListLinearIssuesRequest(Cycle: Unknown),
            "state" => new ListLinearIssuesRequest(State: Unknown),
            "team" => new ListLinearIssuesRequest(Team: "NOPE"),
            _ => new ListLinearIssuesRequest(Assignee: Unknown),
        };

        var response = await Ask(request);

        Assert.Empty(response.Rows);
        Expect.Equal(0, response.Total);
        Assert.Null(response.Next);
    }

    [Fact]
    public async Task Mine_is_ordered_by_priority_with_none_last_then_the_newest_update()
    {
        _ = _facts.WithIssues(
            Row("ENG-1", Viewer, priority: 0, updatedMinutesAgo: 1),
            Row("ENG-2", Viewer, priority: 4, updatedMinutesAgo: 5),
            Row("ENG-3", Viewer, priority: 1, updatedMinutesAgo: 30),
            Row("ENG-4", Viewer, priority: 1, updatedMinutesAgo: 20),
            Row("ENG-5", Viewer, priority: 2, updatedMinutesAgo: 2));

        var response = await Ask(new ListLinearIssuesRequest(Group: "mine"));

        Expect.Sequence(["ENG-4", "ENG-3", "ENG-5", "ENG-2", "ENG-1"], Keys(response));
        Expect.Equal(5, response.Total);
        Assert.Null(response.Next);
    }

    [Fact]
    public async Task Mine_is_only_the_viewers_open_issues()
    {
        _ = _facts.WithIssues(
            Row("ENG-1", Viewer),
            Row("ENG-2", Sam),
            Row("ENG-3", Viewer, stateType: "completed"),
            Row("ENG-4", Viewer, stateType: "canceled"),
            Row("ENG-5", Viewer, stateType: "triage"),
            Row("OPS-6", Viewer, stateType: "backlog"));

        var response = await Ask(new ListLinearIssuesRequest());

        Expect.Sequence(["ENG-1", "ENG-5", "OPS-6"], [.. Keys(response).Order(StringComparer.Ordinal)]);
        Expect.Equal(ViewerId, Expect.Only(_facts.Asked).Filter.Assignee!);
        Expect.Sequence(["triage", "backlog", "unstarted", "started"], Expect.Only(_facts.Asked).Filter.StateTypes!);
    }

    [Fact]
    public async Task Mine_keeps_a_state_the_owner_names_even_when_it_is_closed()
    {
        const string Done = "00000000-0000-4000-8000-000000000055";
        _ = _facts.WithIssues(Row("ENG-1", Viewer), Row("ENG-2", Viewer, stateType: "completed", stateId: Done));

        var response = await Ask(new ListLinearIssuesRequest(State: Done));

        Expect.Sequence(["ENG-2"], Keys(response));
        Assert.Null(Expect.Only(_facts.Asked).Filter.StateTypes);
    }

    [Fact]
    public async Task Mine_filters_priority_and_label_in_sindri()
    {
        _ = _facts.WithIssues(Row("ENG-1", Viewer, priority: 1, labelId: Label), Row("ENG-2", Viewer, priority: 1), Row("ENG-3", Viewer, priority: 3, labelId: Label));

        Expect.Sequence(["ENG-1", "ENG-2"], Keys(await Ask(new ListLinearIssuesRequest(Priority: "1"))).Order(StringComparer.Ordinal));
        Expect.Sequence(["ENG-1", "ENG-3"], Keys(await Ask(new ListLinearIssuesRequest(Label: Label))).Order(StringComparer.Ordinal));
        Expect.Sequence(["ENG-1"], Keys(await Ask(new ListLinearIssuesRequest(Label: Label, Priority: "1"))));
    }

    [Fact]
    public async Task Mine_pages_by_offset_and_totals_every_match()
    {
        _ = _facts.WithIssues([.. Enumerable.Range(1, 5).Select(n => Row(EngKey(n), Viewer, priority: n))]);

        var first = await Ask(new ListLinearIssuesRequest(Limit: "2"));
        var second = await Ask(new ListLinearIssuesRequest(Limit: "2", After: first.Next));
        var third = await Ask(new ListLinearIssuesRequest(Limit: "2", After: second.Next));

        Expect.Sequence(["ENG-1", "ENG-2"], Keys(first));
        Expect.Sequence(["ENG-3", "ENG-4"], Keys(second));
        Expect.Sequence(["ENG-5"], Keys(third));
        Assert.Null(third.Next);
        Expect.Equal(5, first.Total);
        Expect.Equal(5, third.Total);
    }

    [Fact]
    public async Task Mine_is_empty_when_the_assignee_filter_names_someone_else()
    {
        _ = _facts.WithIssues(Row("ENG-1", Viewer), Row("ENG-2", Sam));

        var response = await Ask(new ListLinearIssuesRequest(Group: "mine", Assignee: SamId));

        Assert.Empty(response.Rows);
    }

    [Fact]
    public async Task Teams_leaves_out_the_viewers_own_issues_and_keeps_the_stores_order()
    {
        _ = _facts.WithIssues(
            Row("ENG-1", Viewer, updatedMinutesAgo: 1),
            Row("ENG-2", Sam, updatedMinutesAgo: 5),
            Row("ENG-3", assignee: null, updatedMinutesAgo: 3),
            Row("OPS-4", Sam, updatedMinutesAgo: 9),
            Row("ENG-5", Sam, stateType: "completed", updatedMinutesAgo: 2));

        var response = await Ask(new ListLinearIssuesRequest(Group: "teams"));

        Expect.Equal("teams", response.Group);
        Expect.Sequence(["ENG-3", "ENG-2", "OPS-4"], Keys(response));
        Expect.Equal(3, response.Total);
        Assert.Null(response.Next);
    }

    [Fact]
    public async Task Teams_passes_team_project_cycle_and_the_open_state_types_to_the_store()
    {
        _ = _facts.WithIssues(Row("ENG-1", Sam, projectId: Project, cycleId: Cycle), Row("ENG-2", Sam));

        var response = await Ask(new ListLinearIssuesRequest(Group: "teams", Team: "ENG", Project: Project, Cycle: Cycle));

        Expect.Sequence(["ENG-1"], Keys(response));
        var filter = Expect.Only(_facts.Asked).Filter;
        Expect.Equal("ENG", filter.Team!);
        Expect.Equal(Project, filter.Project!);
        Expect.Equal(Cycle, filter.Cycle!);
        Expect.Sequence(["triage", "backlog", "unstarted", "started"], filter.StateTypes!);
    }

    [Fact]
    public async Task Teams_reads_on_until_the_page_is_full_and_its_next_names_where_it_stopped()
    {
        // why: the store's page holds 2, and every other issue is the viewer's, so one store page never fills a page of 3
        _ = _facts.WithPageSize(2).WithIssues([.. Enumerable.Range(1, 12).Select(n => Row(EngKey(n), n % 2 == 0 ? Viewer : Sam, updatedMinutesAgo: n))]);

        var first = await Ask(new ListLinearIssuesRequest(Group: "teams", Limit: "3"));
        var second = await Ask(new ListLinearIssuesRequest(Group: "teams", Limit: "3", After: first.Next));

        Expect.Sequence(["ENG-1", "ENG-3", "ENG-5"], Keys(first));
        Assert.NotNull(first.Next);
        Expect.Sequence(["ENG-7", "ENG-9", "ENG-11"], Keys(second));
        Assert.Null(first.Total);
        Expect.Equal(7, second.Total);
    }

    [Fact]
    public async Task Teams_total_on_the_last_page_counts_the_rows_before_it()
    {
        _ = _facts.WithIssues([.. Enumerable.Range(1, 7).Select(n => Row(EngKey(n), Sam, updatedMinutesAgo: n))]);

        var first = await Ask(new ListLinearIssuesRequest(Group: "teams", Limit: "5"));
        var second = await Ask(new ListLinearIssuesRequest(Group: "teams", Limit: "5", After: first.Next));

        Expect.Sequence(["ENG-6", "ENG-7"], Keys(second));
        Assert.Null(second.Next);
        Expect.Equal(7, second.Total);
    }

    [Fact]
    public async Task Teams_filters_state_priority_and_label_in_sindri()
    {
        _ = _facts.WithIssues(Row("ENG-1", Sam, priority: 1, labelId: Label, updatedMinutesAgo: 1), Row("ENG-2", Sam, priority: 3, updatedMinutesAgo: 2), Row("ENG-3", Sam, priority: 1, updatedMinutesAgo: 3));

        Expect.Sequence(["ENG-1", "ENG-3"], Keys(await Ask(new ListLinearIssuesRequest(Group: "teams", Priority: "1"))));
        Expect.Sequence(["ENG-1"], Keys(await Ask(new ListLinearIssuesRequest(Group: "teams", Label: Label))));
    }

    [Fact]
    public async Task A_row_carries_the_item_and_pull_request_linked_to_its_record()
    {
        var linked = Row("ENG-1", Viewer);
        _ = _facts.WithIssues(linked, Row("ENG-2", Viewer, updatedMinutesAgo: 20));
        _ = _links.With(linked.Record, new LinearItemLinkView("ITM-1", "running"), new LinearPrView(2071, "https://github.com/acme/api/pull/2071", "waiting"));

        var response = await Ask(new ListLinearIssuesRequest());

        Expect.Equal(new LinearItemLinkView("ITM-1", "running"), response.Rows[0].Link!);
        Expect.Equal(2071, response.Rows[0].Pr!.Number);
        Assert.Null(response.Rows[1].Link);
        Assert.Null(response.Rows[1].Pr);
    }

    [Fact]
    public async Task A_link_read_that_fails_leaves_the_rows_without_links()
    {
        _ = _facts.WithIssues(Row("ENG-1", Viewer));
        _ = _links.Failing(SindriErrors.InvalidQuery.With("views are down"));

        var response = await Ask(new ListLinearIssuesRequest());

        Expect.Sequence(["ENG-1"], Keys(response));
        Assert.Null(response.Rows[0].Link);
    }

    [Fact]
    public async Task The_viewer_and_the_feed_are_the_sources_own()
    {
        var feed = new LinearFeedView("rereading", KnownInstants.Epoch, Code: null, new LinearRereadView(412, 1000));
        _ = _facts.WithSource(Connected(Viewer, feed));

        var response = await Ask(new ListLinearIssuesRequest());

        Expect.Equal(Viewer, response.Viewer!);
        Expect.Equal(feed, response.Feed);
        Assert.True(response.Connected);
    }

    [Fact]
    public async Task A_failing_store_is_the_answers_error()
    {
        var error = SindriErrors.InvalidQuery.With("context is down");
        _ = _facts.Failing(error);

        var result = await Service().Handle(new ListLinearIssuesRequest(), Token);

        Expect.Equal(error.Code, result.Error.Code);
    }
}
