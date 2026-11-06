using System.Text.Json.Nodes;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Features.LinearWriteThrough;
using Aw.Context.Testing.Linear;
using Aw.Context.Testing.Records;
using Aw.Kernel;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Context.Testing.Records.LinearSideSamples;

namespace Aw.Context.Tests.Linear;

/// <summary>
/// The Linear hub's reads on the context facade (<see cref="IContextIssues"/>, Linear hub spec 2026-11-04 §4.3) over the real store and the stateful fake Linear: lists, one issue, comments, the pickers and the strip, with the record's
/// audience applied to the side tables and tombstones hidden.
/// </summary>
[Trait("Lane", "integration")]
public sealed class LinearIssueReadsTests : IAsyncLifetime
{
    private static readonly DateTimeOffset _windowStart = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private LinearHubWorld _world = null!;
    private string _ada = "";
    private string _bo = "";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _world = await LinearHubWorld.Start(seed => seed
            .Team("ENG")
            .Member("Ada Fixture").Member("Bo Fixture")
            .Project("Launch")
            .Cycle("C1", "ENG", _windowStart, _windowStart.AddDays(14))
            .Issue("ENG", "First issue", new FakeLinearIssueSeed { State = "Todo", Assignee = "Ada Fixture", Project = "Launch", Cycle = "C1", Description = "Details of the first issue" })
            .Issue("ENG", "Second issue", new FakeLinearIssueSeed { State = "In Progress", Assignee = "Bo Fixture" })
            .Issue("ENG", "Third issue", new FakeLinearIssueSeed { State = "Done", Assignee = "Ada Fixture" })
            .Comment("ENG-1", "older comment"));
        _ada = _world.Linear.MemberId("Ada Fixture");
        _bo = _world.Linear.MemberId("Bo Fixture");
        _ = await _world.Tick();
    }

    public ValueTask DisposeAsync() => _world.DisposeAsync();

    private string Id(string identifier) => _world.Linear.Issues.Single(i => i.Identifier == identifier).Id;

    private static RecordKey Key(string id) => RecordKey.From("linear:issue/" + id);

    private async Task<T> Facade<T>(Func<IContextIssues, ValueTask<Result<T>>> call)
    {
        await using var scope = _world.Provider.CreateAsyncScope();
        var result = await call(scope.ServiceProvider.GetRequiredService<IContextEngine>());
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : "");
        return result.Value;
    }

    private async Task<Error> Refused<T>(Func<IContextIssues, ValueTask<Result<T>>> call)
    {
        await using var scope = _world.Provider.CreateAsyncScope();
        var result = await call(scope.ServiceProvider.GetRequiredService<IContextEngine>());
        Assert.True(result.IsError);
        return result.Error;
    }

    private Task<LinearIssuePage> List(LinearIssueFilter? filter = null, LinearIssueCursor? after = null, ContextScope? scope = null) =>
        Facade(i => i.IssueList(filter ?? new LinearIssueFilter(), after, scope ?? _world.Scope, Ct));

    private static IEnumerable<string> Identifiers(LinearIssuePage page) => page.Issues.Select(static i => i.Fields.Identifier);

    private ContextScope Session(string repo) => _world.Scope with { Caller = new ContextCaller(ContextCallerKind.Session, ContextRepoName.From(repo), ContextClient.Mcp) };

    [Fact]
    public async Task A_list_is_every_issue_newest_first()
    {
        var page = await List();

        Assert.Equal(["ENG-3", "ENG-2", "ENG-1"], Identifiers(page), StringComparer.Ordinal);
        Assert.Null(page.Next);
        var first = page.Issues[^1];
        Assert.Equal("First issue", first.Title);
        Assert.Equal("Todo", first.StateRaw);
        Assert.Equal("ENG", first.TeamKey);
        Assert.Equal("unstarted", first.Fields.StateType);
        Assert.Equal(_ada, first.Fields.AssigneeId?.Value);
    }

    [Fact]
    public async Task A_list_narrows_by_assignee_state_type_project_cycle_and_team()
    {
        Assert.Equal(["ENG-3", "ENG-1"], Identifiers(await List(new LinearIssueFilter(Assignee: ExternalId.From(_ada)))), StringComparer.Ordinal);
        Assert.Equal(["ENG-2"], Identifiers(await List(new LinearIssueFilter(StateTypes: ["started"]))), StringComparer.Ordinal);
        Assert.Equal(["ENG-3", "ENG-1"], Identifiers(await List(new LinearIssueFilter(StateTypes: ["completed", "unstarted"]))), StringComparer.Ordinal);
        Assert.Equal(["ENG-1"], Identifiers(await List(new LinearIssueFilter(Project: ExternalId.From(_world.Linear.ProjectId("Launch"))))), StringComparer.Ordinal);
        Assert.Equal(["ENG-1"], Identifiers(await List(new LinearIssueFilter(Cycle: ExternalId.From(_world.Linear.CycleId("C1"))))), StringComparer.Ordinal);
        Assert.Equal(3, (await List(new LinearIssueFilter(Team: "ENG"))).Issues.Count);
        Assert.Empty((await List(new LinearIssueFilter(Team: "OPS"))).Issues);
    }

    [Fact]
    public async Task A_list_pages_with_a_cursor_and_ends_with_none()
    {
        var first = await List(new LinearIssueFilter(Limit: 2));
        var second = await List(new LinearIssueFilter(Limit: 2), first.Next);

        Assert.Equal(["ENG-3", "ENG-2"], Identifiers(first), StringComparer.Ordinal);
        Assert.NotNull(first.Next);
        Assert.Equal(["ENG-1"], Identifiers(second), StringComparer.Ordinal);
        Assert.Null(second.Next);
    }

    [Fact]
    public async Task One_issue_reads_by_its_key_and_an_unknown_key_reads_as_null()
    {
        var issue = await Facade(i => i.IssueGet(Key(Id("ENG-2")), _world.Scope, Ct));
        var none = await Facade(i => i.IssueGet(RecordKey.From("linear:issue/nope"), _world.Scope, Ct));

        var first = await Facade(i => i.IssueGet(Key(Id("ENG-1")), _world.Scope, Ct));
        Assert.Equal("Details of the first issue", first?.Description);
        Assert.Null(issue?.Description);
        Assert.Equal("Second issue", issue?.Title);
        Assert.Equal("started", issue?.Fields.StateType);
        Assert.Null(none);
    }

    [Fact]
    public async Task Comments_read_newest_first()
    {
        await _world.Mutate(
            "mutation M($input: CommentCreateInput!) { commentCreate(input: $input) { success } }",
            new JsonObject { ["input"] = new JsonObject { ["issueId"] = Id("ENG-1"), ["body"] = "newer comment" } });
        _world.Clock.Advance(TimeSpan.FromMinutes(1));
        _ = await _world.Tick();

        var comments = await Facade(i => i.IssueComments(Key(Id("ENG-1")), _world.Scope, Ct));

        Assert.Equal(["newer comment", "older comment"], comments.Select(static c => c.Body), StringComparer.Ordinal);
        Assert.All(comments, static c => Assert.False(c.Sindri));
    }

    [Fact]
    public async Task A_tombstoned_issue_is_hidden_from_every_read()
    {
        var key = Key(Id("ENG-2")).Value;
        _world.Sql($"UPDATE source_record SET removed_at = '2026-01-02T08:00:00.0000000Z' WHERE key = '{key}'");
        _world.Sql($"INSERT INTO linear_issue_change (issue_key, field, \"old\", \"new\", at, via) VALUES ('{key}', 'assignee', NULL, '{_ada}', '2026-01-02T08:30:00.0000000Z', 'feed')");

        var list = await List();
        var one = await Facade(i => i.IssueGet(Key(Id("ENG-2")), _world.Scope, Ct));
        var comments = await Facade(i => i.IssueComments(Key(Id("ENG-2")), _world.Scope, Ct));
        var since = await Facade(i => i.IssueSince(_windowStart, ExternalId.From(_ada), _world.Scope, Ct));

        Assert.Equal(["ENG-3", "ENG-1"], Identifiers(list), StringComparer.Ordinal);
        Assert.Null(one);
        Assert.Empty(comments);
        Assert.Empty(since.Changes);
    }

    [Fact]
    public async Task A_session_of_a_repo_the_profile_does_not_name_sees_nothing_and_one_of_its_repos_sees_the_issues()
    {
        var stranger = await List(scope: Session("unlisted"));
        var own = await List(scope: Session("split-panes"));
        var one = await Facade(i => i.IssueGet(Key(Id("ENG-1")), Session("unlisted"), Ct));
        var comments = await Facade(i => i.IssueComments(Key(Id("ENG-1")), Session("unlisted"), Ct));

        Assert.Empty(stranger.Issues);
        Assert.Equal(3, own.Issues.Count);
        Assert.Null(one);
        Assert.Empty(comments);
    }

    [Fact]
    public async Task An_issue_of_a_team_the_caller_may_not_read_is_hidden_with_its_side_rows()
    {
        // a record of another team's audience, stored by hand with its fields and a comment
        var other = RecordSamples.Record("linear:issue/other-team", "v1", RecordSamples.Linear) with { Audience = AudienceKey.From("linear:OPS") };
        var rows = Rows(Fields(Ada, Todo), LinearWrittenFields.None, Comment("oc1", "secret plans"));
        var store = _world.Provider.GetRequiredService<IRecordStore>();
        _ = await store.Commit(RecordSamples.Batch(RecordSamples.Linear, 99, RecordSamples.Write(other, "words") with { Side = rows }) with { Position = null }, Ct);

        var list = await List();
        var one = await Facade(i => i.IssueGet(other.Key, _world.Scope, Ct));
        var comments = await Facade(i => i.IssueComments(other.Key, _world.Scope, Ct));

        Assert.Equal(3, list.Issues.Count);
        Assert.Null(one);
        Assert.Empty(comments);
        Assert.Equal(1, _world.Count("SELECT count(*) FROM linear_comment WHERE issue_key = 'linear:issue/other-team'"));
    }

    [Fact]
    public async Task The_pickers_of_a_team_read_for_the_owner_and_not_for_a_session()
    {
        var lookups = await Facade(i => i.IssueLookups("ENG", _world.Scope, Ct));
        var unknown = await Facade(i => i.IssueLookups("OPS", _world.Scope, Ct));
        var refused = await Refused(i => i.IssueLookups("ENG", Session("split-panes"), Ct));

        Assert.Equal("ENG", lookups?.TeamKey);
        Assert.Equal(["Backlog", "Todo", "In Progress", "Done", "Canceled"], lookups?.States.Select(static s => s.Name), StringComparer.Ordinal);
        Assert.Contains(lookups?.Members ?? [], static m => string.Equals(m.Name, "Ada Fixture", StringComparison.Ordinal));
        Assert.Null(unknown);
        Assert.Equal(ContextErrors.SessionsDisabled.Code, refused.Code);
    }

    [Fact]
    public async Task The_strip_is_closed_to_a_session()
    {
        var refused = await Refused(i => i.IssueSince(_windowStart, ExternalId.From(_ada), Session("split-panes"), Ct));

        Assert.Equal(ContextErrors.SessionsDisabled.Code, refused.Code);
    }

    [Fact]
    public async Task Since_lists_assignments_to_the_viewer_and_state_moves_on_their_issues_a_sync_saw_and_not_the_rest()
    {
        var marker = _world.Clock.GetUtcNow();
        _world.Clock.Advance(TimeSpan.FromHours(1));
        await _world.Update(Id("ENG-1"), new JsonObject { ["stateId"] = _world.Linear.StateId("ENG", "In Progress") });   // a teammate moves Ada's issue
        await _world.Update(Id("ENG-2"), new JsonObject { ["stateId"] = _world.Linear.StateId("ENG", "Done") });   // and Bo's, which is not the viewer's
        await _world.Update(Id("ENG-3"), new JsonObject { ["assigneeId"] = _bo });   // Ada's issue goes to Bo
        await _world.Update(Id("ENG-2"), new JsonObject { ["assigneeId"] = _ada });   // Bo's goes to Ada
        _ = await _world.Tick();

        var since = await Facade(i => i.IssueSince(marker, ExternalId.From(_ada), _world.Scope, Ct));

        var summary = since.Changes.Select(static c => (c.Identifier, c.Field, c.Via)).OrderBy(static c => c.Identifier, StringComparer.Ordinal).ThenBy(static c => c.Field, StringComparer.Ordinal).ToList();
        Assert.Equal(
            [("ENG-1", "state", "feed"), ("ENG-2", "assignee", "feed"), ("ENG-2", "state", "feed")],
            summary);
        Assert.Equal(_ada, since.Changes.Single(static c => c.Field == "assignee").New?.Value);
    }

    [Fact]
    public async Task A_teammates_state_move_absorbed_by_an_owner_priority_save_shows_in_since()
    {
        var marker = _world.Clock.GetUtcNow();
        _world.Clock.Advance(TimeSpan.FromHours(1));
        await _world.Update(Id("ENG-1"), new JsonObject { ["stateId"] = _world.Linear.StateId("ENG", "In Progress") });   // a teammate moves it
        await _world.Update(Id("ENG-1"), new JsonObject { ["priority"] = 1 });   // the owner saves a priority before any sync looked
        await using (var scope = _world.Provider.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IService<LinearWriteThroughRequest, LinearWriteThroughResponse>>();
            var stored = await service.Handle(new LinearWriteThroughRequest(_world.Scope, ExternalId.From(Id("ENG-1")), LinearWrittenFields.None), Ct);
            Assert.True(stored.IsSuccess, stored.IsError ? stored.Error.Message : "");
        }

        var since = await Facade(i => i.IssueSince(marker, ExternalId.From(_ada), _world.Scope, Ct));

        var change = Assert.Single(since.Changes);
        Assert.Equal(("ENG-1", "state", "feed"), (change.Identifier, change.Field, change.Via));
    }

    [Fact]
    public async Task An_owners_own_state_move_is_not_in_since()
    {
        var marker = _world.Clock.GetUtcNow();
        _world.Clock.Advance(TimeSpan.FromHours(1));
        await _world.Update(Id("ENG-1"), new JsonObject { ["stateId"] = _world.Linear.StateId("ENG", "Done") });
        await using (var scope = _world.Provider.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IService<LinearWriteThroughRequest, LinearWriteThroughResponse>>();
            _ = await service.Handle(new LinearWriteThroughRequest(_world.Scope, ExternalId.From(Id("ENG-1")), LinearWrittenFields.State), Ct);
        }

        var since = await Facade(i => i.IssueSince(marker, ExternalId.From(_ada), _world.Scope, Ct));

        Assert.Empty(since.Changes);
        Assert.Equal(1, _world.Count($"SELECT count(*) FROM linear_issue_change WHERE field = 'state' AND via = 'write-through' AND issue_key = '{Key(Id("ENG-1")).Value}'"));
    }

    [Fact]
    public async Task A_new_issue_a_teammate_assigns_to_the_viewer_shows_in_since_after_the_first_sync()
    {
        var marker = _world.Clock.GetUtcNow();
        _world.Clock.Advance(TimeSpan.FromHours(1));
        await _world.Mutate(
            "mutation M($input: IssueCreateInput!) { issueCreate(input: $input) { success } }",
            new JsonObject { ["input"] = new JsonObject { ["title"] = "Harbor ferry booking", ["teamId"] = _world.Linear.TeamId("ENG"), ["assigneeId"] = _ada } });
        _ = await _world.Tick();

        var since = await Facade(i => i.IssueSince(marker, ExternalId.From(_ada), _world.Scope, Ct));

        var change = Assert.Single(since.Changes);
        Assert.Equal(("ENG-4", "assignee", "feed", null, _ada), (change.Identifier, change.Field, change.Via, change.Old?.Value, change.New?.Value));
    }

    [Fact]
    public async Task The_first_sync_does_not_put_the_viewers_existing_issues_in_since()
    {
        var since = await Facade(i => i.IssueSince(_windowStart, ExternalId.From(_ada), _world.Scope, Ct));   // InitializeAsync's tick stored ENG-1 and ENG-3, both Ada's

        Assert.Empty(since.Changes);
    }

    [Fact]
    public async Task An_owners_own_assignment_to_themselves_is_not_in_since()
    {
        var marker = _world.Clock.GetUtcNow();
        _world.Clock.Advance(TimeSpan.FromHours(1));
        await _world.Update(Id("ENG-2"), new JsonObject { ["assigneeId"] = _ada });
        await using (var scope = _world.Provider.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IService<LinearWriteThroughRequest, LinearWriteThroughResponse>>();
            _ = await service.Handle(new LinearWriteThroughRequest(_world.Scope, ExternalId.From(Id("ENG-2")), LinearWrittenFields.Assignee), Ct);
        }

        var since = await Facade(i => i.IssueSince(marker, ExternalId.From(_ada), _world.Scope, Ct));

        Assert.Empty(since.Changes);
        Assert.Equal(1, _world.Count($"SELECT count(*) FROM linear_issue_change WHERE field = 'assignee' AND via = 'write-through' AND issue_key = '{Key(Id("ENG-2")).Value}'"));
    }

    [Fact]
    public async Task Since_derives_mentions_from_comments_after_the_marker_by_someone_else()
    {
        var marker = FakeNow();   // a comment's time is Linear's (here the fake's), so the marker is read off the same clock
        await PostComment("ENG-2", "ping @Ada Fixture please look");
        await PostComment("ENG-2", "no one is mentioned here");
        await PostComment("ENG-3", "email ada@example.org and say @Ada Fixturex");
        _ = await _world.Tick();
        var key = Key(Id("ENG-2")).Value;
        _world.Sql($"INSERT INTO linear_comment (id, issue_key, author_id, created_at, body, sindri) VALUES ('mine', '{key}', '{_ada}', '2026-01-01T12:30:00.0000000Z', 'I said @Ada Fixture myself', 0)");
        _world.Sql($"INSERT INTO linear_comment (id, issue_key, author_id, created_at, body, sindri) VALUES ('ours', '{key}', NULL, '2026-01-01T12:31:00.0000000Z', 'Sindri run (automatic) @Ada Fixture', 1)");
        _world.Sql($"INSERT INTO linear_comment (id, issue_key, author_id, created_at, body, sindri) VALUES ('old', '{key}', '{_bo}', '2026-01-01T00:00:01.0000000Z', 'old @Ada Fixture', 0)");

        var since = await Facade(i => i.IssueSince(marker, ExternalId.From(_ada), _world.Scope, Ct));

        var mention = Assert.Single(since.Mentions);
        Assert.Equal("ENG-2", mention.Identifier);
        Assert.Equal("ping @Ada Fixture please look", mention.Comment.Body);
    }

    [Fact]
    public async Task A_mention_is_found_under_more_than_a_thousand_newer_comments_with_none()
    {
        var marker = FakeNow();
        await PostComment("ENG-2", "ping @Ada Fixture please look");
        _ = await _world.Tick();
        var key = Key(Id("ENG-2")).Value;
        _world.Sql(
            "WITH RECURSIVE n(i) AS (SELECT 1 UNION ALL SELECT i + 1 FROM n WHERE i < 1001) " +
            $"INSERT INTO linear_comment (id, issue_key, author_id, created_at, body, sindri) SELECT 'noise-' || i, '{key}', '{_bo}', '2099-01-01T00:00:00.0000000Z', 'ferry timetable update ' || i, 0 FROM n");

        var since = await Facade(i => i.IssueSince(marker, ExternalId.From(_ada), _world.Scope, Ct));

        var mention = Assert.Single(since.Mentions);
        Assert.Equal("ping @Ada Fixture please look", mention.Comment.Body);
    }

    [Fact]
    public async Task A_viewer_the_pickers_do_not_know_has_no_mentions()
    {
        await PostComment("ENG-2", "ping @Ada Fixture please look");
        _ = await _world.Tick();

        var since = await Facade(i => i.IssueSince(_windowStart, ExternalId.From("someone-else"), _world.Scope, Ct));

        Assert.Empty(since.Mentions);
    }

    private DateTimeOffset FakeNow() => _world.Linear.Issues.Select(static i => i.UpdatedAt).Concat(_world.Linear.Comments.Select(static c => c.UpdatedAt))
        .Max(static t => DateTimeOffset.Parse(t, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal));

    private Task PostComment(string identifier, string body) =>
        _world.Mutate(
            "mutation M($input: CommentCreateInput!) { commentCreate(input: $input) { success } }",
            new JsonObject { ["input"] = new JsonObject { ["issueId"] = Id(identifier), ["body"] = body } });
}
