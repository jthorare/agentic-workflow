using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Linear;
using Aw.Kernel;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Context.Tests.Linear;

/// <summary>
/// The two stored reads the Linear hub's Plan and Cycle pages and the tracker source use (spec 2026-11-04 §6.1, §7): one issue by its human key, and issue counts by state type. Same audience and tombstone rules as the other issue reads.
/// </summary>
[Trait("Lane", "integration")]
public sealed class LinearPlanReadsTests : IAsyncLifetime
{
    private static readonly DateTimeOffset _windowStart = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private LinearHubWorld _world = null!;
    private string _ada = "";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _world = await LinearHubWorld.Start(seed => seed
            .Team("ENG")
            .Member("Ada Fixture").Member("Bo Fixture")
            .Project("Launch")
            .Cycle("C1", "ENG", _windowStart, _windowStart.AddDays(14))
            .Issue("ENG", "One", new FakeLinearIssueSeed { State = "Todo", Assignee = "Ada Fixture", Project = "Launch", Cycle = "C1", Description = "Details of one" })
            .Issue("ENG", "Two", new FakeLinearIssueSeed { State = "In Progress", Assignee = "Bo Fixture", Project = "Launch", Cycle = "C1" })
            .Issue("ENG", "Three", new FakeLinearIssueSeed { State = "Done", Assignee = "Ada Fixture", Project = "Launch" })
            .Issue("ENG", "Four", new FakeLinearIssueSeed { State = "Canceled", Assignee = "Bo Fixture", Cycle = "C1" })
            .Issue("ENG", "Five", new FakeLinearIssueSeed { State = "Backlog", Assignee = "Ada Fixture" })
            .Issue("ENG", "Six", new FakeLinearIssueSeed { State = "Todo", Assignee = "Bo Fixture", Project = "Launch" })
            .Comment("ENG-1", "a comment"));
        _ada = _world.Linear.MemberId("Ada Fixture");
        _ = await _world.Tick();
    }

    public ValueTask DisposeAsync() => _world.DisposeAsync();

    private string Id(string identifier) => _world.Linear.Issues.Single(i => i.Identifier == identifier).Id;

    private ContextScope Session(string repo) => _world.Scope with { Caller = new ContextCaller(ContextCallerKind.Session, ContextRepoName.From(repo), ContextClient.Mcp) };

    private async Task<T> Facade<T>(Func<IContextIssues, ValueTask<Result<T>>> call)
    {
        await using var scope = _world.Provider.CreateAsyncScope();
        var result = await call(scope.ServiceProvider.GetRequiredService<IContextEngine>());
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : "");
        return result.Value;
    }

    private Task<LinearIssueDetail?> ByKey(string key, ContextScope? scope = null) =>
        Facade(i => i.IssueGet(LinearIssueKey.Parse(key).Value, scope ?? _world.Scope, Ct));

    private Task<LinearIssueCounts> Counts(LinearIssueFilter? filter = null, ContextScope? scope = null) =>
        Facade(i => i.IssueCounts(filter ?? new LinearIssueFilter(), scope ?? _world.Scope, Ct));

    private async Task<int> ListedWith(LinearIssueFilter filter)
    {
        var total = 0;
        LinearIssueCursor? after = null;
        do
        {
            var page = await Facade(i => i.IssueList(filter with { Limit = 2 }, after, _world.Scope, Ct));
            total += page.Issues.Count;
            after = page.Next;
        }
        while (after is not null);
        return total;
    }

    [Fact]
    public async Task An_issue_reads_by_its_key_to_the_same_row_as_by_its_record_key_with_its_comments()
    {
        var byKey = await ByKey("ENG-1");
        var byRecord = await Facade(i => i.IssueGet(RecordKey.From("linear:issue/" + Id("ENG-1")), _world.Scope, Ct));
        var comments = await Facade(i => i.IssueComments(RecordKey.From("linear:issue/" + Id("ENG-1")), _world.Scope, Ct));

        Assert.NotNull(byKey);
        Assert.Equal("Details of one", byKey.Issue.Description);
        Assert.Equal(byRecord, byKey.Issue);
        Assert.False(byKey.Removed);
        Assert.Null(byKey.Parent);
        Assert.Equal(["a comment"], byKey.Comments.Select(static c => c.Body), StringComparer.Ordinal);
        Assert.Equal(comments, byKey.Comments);
    }

    [Fact]
    public async Task A_lower_case_key_reads_the_same_issue()
    {
        var issue = await ByKey("eng-2");

        Assert.Equal("ENG-2", issue?.Issue.Fields.Identifier);
    }

    [Fact]
    public async Task A_key_never_stored_reads_as_null()
    {
        Assert.Null(await ByKey("ENG-99"));
        Assert.Null(await ByKey("OPS-1"));
    }

    [Fact]
    public async Task A_tombstoned_issue_reads_by_key_as_its_last_copy_marked_removed_while_the_record_key_read_still_hides_it()
    {
        var recordKey = RecordKey.From("linear:issue/" + Id("ENG-2"));
        _world.Sql($"UPDATE source_record SET removed_at = '2026-01-02T08:00:00.0000000Z' WHERE key = '{recordKey.Value}'");

        var gone = await ByKey("ENG-2");

        Assert.NotNull(gone);
        Assert.True(gone.Removed);
        Assert.Equal("Two", gone.Issue.Title);
        Assert.False((await ByKey("ENG-1"))!.Removed);
        Assert.Null(await Facade(i => i.IssueGet(recordKey, _world.Scope, Ct)));
    }

    [Fact]
    public async Task A_child_issue_names_its_stored_parent_and_a_parent_that_is_not_stored_reads_as_null()
    {
        _world.Sql($"UPDATE linear_issue SET parent_id = '{Id("ENG-1")}' WHERE key = 'linear:issue/{Id("ENG-2")}'");
        _world.Sql($"UPDATE linear_issue SET parent_id = 'not-stored' WHERE key = 'linear:issue/{Id("ENG-3")}'");

        var child = await ByKey("ENG-2");
        var orphan = await ByKey("ENG-3");

        Assert.Equal("ENG-1", child?.Parent?.Key.ToString());
        Assert.Equal("One", child?.Parent?.Title);
        Assert.NotNull(orphan);
        Assert.Null(orphan.Parent);
    }

    [Fact]
    public async Task A_session_of_a_repo_the_profile_does_not_name_reads_null_by_key_and_one_of_its_repos_reads_the_issue()
    {
        Assert.Null(await ByKey("ENG-1", Session("unlisted")));
        Assert.NotNull(await ByKey("ENG-1", Session("split-panes")));
    }

    [Fact]
    public async Task Counts_group_the_issues_by_state_type_and_total_them_with_the_canceled_ones()
    {
        var counts = await Counts();

        Assert.Equal(new Dictionary<string, int>(StringComparer.Ordinal) { ["unstarted"] = 2, ["started"] = 1, ["completed"] = 1, ["canceled"] = 1, ["backlog"] = 1 }, counts.ByStateType.ToDictionary(static p => p.Key, static p => p.Value, StringComparer.Ordinal));
        Assert.Equal(6, counts.Total);
    }

    [Fact]
    public async Task Counts_narrow_by_project_cycle_assignee_state_type_and_team_like_a_list()
    {
        Assert.Equal(4, (await Counts(new LinearIssueFilter(Project: ExternalId.From(_world.Linear.ProjectId("Launch"))))).Total);
        var cycle = await Counts(new LinearIssueFilter(Cycle: ExternalId.From(_world.Linear.CycleId("C1"))));
        Assert.Equal(3, cycle.Total);
        Assert.Equal(1, cycle.ByStateType["canceled"]);
        Assert.Equal(3, (await Counts(new LinearIssueFilter(Assignee: ExternalId.From(_ada)))).Total);
        var started = await Counts(new LinearIssueFilter(StateTypes: ["started", "completed"]));
        Assert.Equal(2, started.Total);
        Assert.False(started.ByStateType.ContainsKey("unstarted"));
        Assert.Equal(6, (await Counts(new LinearIssueFilter(Team: "ENG"))).Total);
        Assert.Equal(0, (await Counts(new LinearIssueFilter(Team: "OPS"))).Total);
    }

    [Fact]
    public async Task Counts_ignore_the_limit()
    {
        var counts = await Counts(new LinearIssueFilter(Limit: 1));

        Assert.Equal(6, counts.Total);
    }

    [Fact]
    public async Task A_session_outside_the_audience_counts_nothing_and_one_inside_counts_everything()
    {
        var stranger = await Counts(scope: Session("unlisted"));
        var own = await Counts(scope: Session("split-panes"));

        Assert.Equal(0, stranger.Total);
        Assert.Empty(stranger.ByStateType);
        Assert.Equal(6, own.Total);
    }

    [Fact]
    public async Task A_removed_issue_is_not_counted()
    {
        _world.Sql($"UPDATE source_record SET removed_at = '2026-01-02T08:00:00.0000000Z' WHERE key = 'linear:issue/{Id("ENG-3")}'");

        var counts = await Counts();

        Assert.Equal(5, counts.Total);
        Assert.False(counts.ByStateType.ContainsKey("completed"));
    }

    [Fact]
    public async Task An_issue_with_no_stored_state_type_counts_under_unknown()
    {
        _world.Sql($"UPDATE linear_issue SET state_type = NULL WHERE key = 'linear:issue/{Id("ENG-5")}'");

        var counts = await Counts();

        Assert.Equal(1, counts.ByStateType["unknown"]);
        Assert.False(counts.ByStateType.ContainsKey("backlog"));
        Assert.Equal(6, counts.Total);
    }

    [Fact]
    public async Task Each_count_equals_the_rows_a_list_with_that_state_type_pages_through_to_the_end()
    {
        var filters = new LinearIssueFilter[]
        {
            new(),
            new(Team: "ENG"),
            new(Project: ExternalId.From(_world.Linear.ProjectId("Launch"))),
            new(Cycle: ExternalId.From(_world.Linear.CycleId("C1"))),
            new(Assignee: ExternalId.From(_ada)),
            new(Project: ExternalId.From(_world.Linear.ProjectId("Launch")), Assignee: ExternalId.From(_ada)),
        };
        foreach (var filter in filters)
        {
            var counts = await Counts(filter);

            Assert.NotEmpty(counts.ByStateType);
            foreach (var (type, count) in counts.ByStateType)
            {
                Assert.Equal(count, await ListedWith(filter with { StateTypes = [type] }));
            }

            Assert.Equal(counts.Total, await ListedWith(filter));
        }
    }
}
