using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Aw.Context.Testing.Fakes;
using Aw.Context.Testing.Linear;
using Aw.Kernel.Testing;

namespace Aw.Context.Testing.Tests;

/// <summary>The stateful fake Linear: writes change what reads return, and the switches make it misbehave (spec D15).</summary>
[Lane(TestLane.Integration)]
public sealed class FakeLinearTests
{
    private const string Create = "mutation M($input: IssueCreateInput!) { issueCreate(input: $input) { success issue { id identifier updatedAt } } }";
    private const string Update = "mutation M($id: String!, $input: IssueUpdateInput!) { issueUpdate(id: $id, input: $input) { success issue { id title updatedAt } } }";
    private const string Comment = "mutation M($input: CommentCreateInput!) { commentCreate(input: $input) { success comment { id body } } }";
    private const string Link = "mutation M($issueId: String!, $url: String!, $title: String) { attachmentLinkURL(issueId: $issueId, url: $url, title: $title) { success attachment { id url title } } }";
    private const string Archive = "mutation M($id: String!) { issueArchive(id: $id) { success entity { id } } }";
    private const string List = "query Q($includeArchived: Boolean) { issues(first: 50, orderBy: updatedAt, includeArchived: $includeArchived) { nodes { id identifier title updatedAt estimate team { key } } pageInfo { hasNextPage } } }";
    private const string One = "query Q($id: String!) { issue(id: $id) { id identifier title estimate updatedAt team { key } comments { nodes { id body } } attachments { nodes { id url title } } } }";

    [Fact]
    public async Task A_created_issue_shows_in_the_list_and_by_id()
    {
        await using var linear = await FakeLinear.Start(seed => seed.Team("ENG"));

        var created = await Gql(linear, Create, NewIssue(linear, "Fix the login timeout"));
        var id = (string)created["data"]!["issueCreate"]!["issue"]!["id"]!;
        var list = await Gql(linear, List, []);
        var one = await Gql(linear, One, new JsonObject { ["id"] = "ENG-1" });

        Assert.True((bool)created["data"]!["issueCreate"]!["success"]!);
        Assert.Equal("ENG-1", (string?)created["data"]!["issueCreate"]!["issue"]!["identifier"]);
        Assert.Equal(["ENG-1"], Identifiers(list));
        Assert.Equal(id, (string?)one["data"]!["issue"]!["id"]);
        Assert.Equal("Fix the login timeout", linear.Issues.Single().Title);
    }

    [Fact]
    public async Task An_update_changes_what_reads_return_and_moves_updated_at()
    {
        await using var linear = await FakeLinear.Start(seed => seed.Team("ENG").Issue("ENG", "Old title"));
        var before = linear.Issues.Single();

        var updated = await Gql(linear, Update, new JsonObject { ["id"] = before.Id, ["input"] = new JsonObject { ["title"] = "New title", ["priority"] = 2 } });
        var one = await Gql(linear, One, new JsonObject { ["id"] = before.Id });

        Assert.True((bool)updated["data"]!["issueUpdate"]!["success"]!);
        Assert.Equal("New title", (string?)one["data"]!["issue"]!["title"]);
        Assert.Equal(2, linear.Issues.Single().Priority);
        Assert.True(string.CompareOrdinal(linear.Issues.Single().UpdatedAt, before.UpdatedAt) > 0);
    }

    [Fact]
    public async Task A_repeated_client_id_is_refused_and_leaves_one_issue()
    {
        await using var linear = await FakeLinear.Start();
        var clientId = "11111111-1111-4111-8111-111111111111";

        var first = await Gql(linear, Create, NewIssue(linear, "Once", clientId));
        var second = await Gql(linear, Create, NewIssue(linear, "Twice", clientId));

        Assert.Equal(clientId, (string?)first["data"]!["issueCreate"]!["issue"]!["id"]);
        Assert.Null(second["data"]!["issueCreate"]);
        Assert.Equal("Argument Validation Error", (string?)second["errors"]![0]!["message"]);
        Assert.Equal("INVALID_INPUT", (string?)second["errors"]![0]!["extensions"]!["code"]);
        Assert.Equal("id", (string?)second["errors"]![0]!["extensions"]!["validationErrors"]![0]!["property"]);
        Assert.Equal("Once", linear.Issues.Single().Title);
    }

    [Fact]
    public async Task A_comment_and_an_attachment_show_on_the_issue_and_move_its_updated_at()
    {
        await using var linear = await FakeLinear.Start(seed => seed.Team("ENG").Issue("ENG", "Needs a note"));
        var issue = linear.Issues.Single();

        _ = await Gql(linear, Comment, new JsonObject { ["input"] = new JsonObject { ["issueId"] = issue.Id, ["body"] = "Looking into it", ["id"] = "22222222-2222-4222-8222-222222222222" } });
        var afterComment = linear.Issues.Single().UpdatedAt;
        _ = await Gql(linear, Link, new JsonObject { ["issueId"] = issue.Id, ["url"] = "https://git.fake/pull/7", ["title"] = "PR 7" });
        _ = await Gql(linear, Link, new JsonObject { ["issueId"] = issue.Id, ["url"] = "https://git.fake/pull/7", ["title"] = "PR 7 (renamed)" });
        var one = await Gql(linear, One, new JsonObject { ["id"] = issue.Identifier });

        Assert.Equal("Looking into it", (string?)one["data"]!["issue"]!["comments"]!["nodes"]![0]!["body"]);
        Assert.Equal("22222222-2222-4222-8222-222222222222", linear.Comments.Single().Id);
        Assert.Equal("PR 7 (renamed)", linear.Attachments.Single().Title);
        _ = Assert.Single(one["data"]!["issue"]!["attachments"]!["nodes"]!.AsArray());
        Assert.True(string.CompareOrdinal(afterComment, issue.UpdatedAt) > 0);
        Assert.True(string.CompareOrdinal(linear.Issues.Single().UpdatedAt, afterComment) > 0);
    }

    [Fact]
    public async Task An_archived_issue_leaves_the_default_list_but_stays_readable()
    {
        await using var linear = await FakeLinear.Start(seed => seed.Team("ENG").Issue("ENG", "Keep").Issue("ENG", "Drop"));
        var drop = linear.Issues.Single(i => i.Title == "Drop");

        var archived = await Gql(linear, Archive, new JsonObject { ["id"] = drop.Id });
        var visible = await Gql(linear, List, []);
        var all = await Gql(linear, List, new JsonObject { ["includeArchived"] = true });
        var one = await Gql(linear, One, new JsonObject { ["id"] = drop.Id });

        Assert.True((bool)archived["data"]!["issueArchive"]!["success"]!);
        Assert.Equal(["ENG-1"], Identifiers(visible));
        Assert.Equal(2, Identifiers(all).Count);
        Assert.Equal("Drop", (string?)one["data"]!["issue"]!["title"]);
        Assert.NotNull(linear.Issues.Single(i => i.Title == "Drop").ArchivedAt);
    }

    [Fact]
    public async Task Introspection_lists_id_on_the_create_inputs_and_the_mutations()
    {
        await using var linear = await FakeLinear.Start();
        const string Types = "{ issue: __type(name: \"IssueCreateInput\") { inputFields { name type { name kind ofType { name kind } } } } " +
            "comment: __type(name: \"CommentCreateInput\") { inputFields { name } } update: __type(name: \"IssueUpdateInput\") { inputFields { name } } " +
            "__schema { mutationType { fields { name } } } }";

        var body = await Gql(linear, Types, variables: null);
        var data = body["data"]!;
        var id = data["issue"]!["inputFields"]!.AsArray().Single(f => (string?)f!["name"] == "id")!;
        var title = data["issue"]!["inputFields"]!.AsArray().Single(f => (string?)f!["name"] == "title")!;

        Assert.Equal("String", (string?)id["type"]!["name"]);
        Assert.Equal("NON_NULL", (string?)title["type"]!["kind"]);
        Assert.Contains(data["comment"]!["inputFields"]!.AsArray(), f => (string?)f!["name"] == "id");
        Assert.DoesNotContain(data["update"]!["inputFields"]!.AsArray(), f => (string?)f!["name"] == "id");
        Assert.Contains(data["__schema"]!["mutationType"]!["fields"]!.AsArray(), f => (string?)f!["name"] == "issueCreate");
    }

    [Fact]
    public async Task A_mutation_may_carry_its_input_inline()
    {
        await using var linear = await FakeLinear.Start();
        var query = $"mutation {{ issueCreate(input: {{ teamId: \"{linear.TeamId("ENG")}\", title: \"Inline\", labelIds: [] }}) {{ success issue {{ identifier }} }} }}";

        var body = await Gql(linear, query, variables: null);

        Assert.Equal("ENG-1", (string?)body["data"]!["issueCreate"]!["issue"]!["identifier"]);
    }

    [Fact]
    public async Task A_wrong_token_is_a_401_and_the_request_log_holds_bodies_only()
    {
        await using var linear = await FakeLinear.Start();

        var (status, _) = await Http.Post(linear.Url, "not-the-token", """{"query":"{ viewer { id } }"}""");
        var (ok, _) = await Http.Post(linear.Url, "Bearer " + linear.Token, """{"query":"{ viewer { id name } }"}""");

        Assert.Equal(HttpStatusCode.Unauthorized, status);
        Assert.Equal(HttpStatusCode.OK, ok);
        Assert.Equal(2, linear.RequestBodies.Count);
        Assert.DoesNotContain(linear.RequestBodies, b => b.Contains(linear.Token, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Every_answer_carries_the_rate_limit_headers()
    {
        await using var linear = await FakeLinear.Start();
        using var client = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, linear.Url) { Content = new StringContent("""{"query":"{ viewer { id } }"}""", Encoding.UTF8, "application/json") };
        _ = request.Headers.TryAddWithoutValidation("Authorization", linear.Token);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("5000", response.Headers.GetValues("x-ratelimit-requests-limit").Single());
        Assert.Equal("4999", response.Headers.GetValues("x-ratelimit-requests-remaining").Single());
        Assert.True(response.Headers.Contains("x-ratelimit-requests-reset"));
        Assert.True(response.Headers.Contains("x-complexity"));
    }

    [Fact]
    public async Task AnswerWithNewerField_shows_a_teammates_edit_on_the_next_read_only()
    {
        await using var linear = await FakeLinear.Start();
        _ = await Gql(linear, Create, NewIssue(linear, "Shared"));
        var written = linear.Issues.Single();
        linear.Switches.AnswerWithNewerField = true;

        var first = await Gql(linear, One, new JsonObject { ["id"] = written.Id });
        var second = await Gql(linear, One, new JsonObject { ["id"] = written.Id });

        Assert.Equal(1, (int?)first["data"]!["issue"]!["estimate"]);
        Assert.NotEqual(written.UpdatedAt, (string?)first["data"]!["issue"]!["updatedAt"], StringComparer.Ordinal);
        Assert.Equal(1, (int?)second["data"]!["issue"]!["estimate"]);
        Assert.False(linear.Switches.AnswerWithNewerField);
    }

    [Theory]
    [InlineData(LinearRateLimit.Http429, HttpStatusCode.TooManyRequests)]
    [InlineData(LinearRateLimit.GraphQl, HttpStatusCode.BadRequest)]
    public async Task RateLimit_refuses_every_request_until_it_is_switched_off(LinearRateLimit mode, HttpStatusCode status)
    {
        await using var linear = await FakeLinear.Start();
        linear.Switches.RateLimit = mode;

        var (refused, body) = await Http.Post(linear.Url, linear.Token, """{"query":"{ viewer { id } }"}""");
        linear.Switches.RateLimit = LinearRateLimit.None;
        var (ok, _) = await Http.Post(linear.Url, linear.Token, """{"query":"{ viewer { id } }"}""");

        Assert.Equal(status, refused);
        Assert.Equal("RATELIMITED", (string?)body!["errors"]![0]!["extensions"]!["code"]);
        Assert.Equal(HttpStatusCode.OK, ok);
    }

    [Fact]
    public async Task RevokeKey_makes_the_token_a_401_from_then_on()
    {
        await using var linear = await FakeLinear.Start();
        var (before, _) = await Http.Post(linear.Url, linear.Token, """{"query":"{ viewer { id } }"}""");
        linear.Switches.RevokeKey = true;

        var (after, _) = await Http.Post(linear.Url, linear.Token, """{"query":"{ viewer { id } }"}""");

        Assert.Equal(HttpStatusCode.OK, before);
        Assert.Equal(HttpStatusCode.Unauthorized, after);
    }

    [Fact]
    public async Task RepeatClientId_refuses_a_fresh_client_id_and_creates_nothing()
    {
        await using var linear = await FakeLinear.Start();
        linear.Switches.RepeatClientId = true;

        var body = await Gql(linear, Create, NewIssue(linear, "Fresh id", "33333333-3333-4333-8333-333333333333"));

        Assert.Null(body["data"]!["issueCreate"]);
        Assert.Equal("INVALID_INPUT", (string?)body["errors"]![0]!["extensions"]!["code"]);
        Assert.Empty(linear.Issues);
    }

    [Fact]
    public async Task NoAnswer_applies_the_next_mutation_and_drops_the_connection()
    {
        await using var linear = await FakeLinear.Start();
        linear.Switches.NoAnswer = true;

        _ = await Assert.ThrowsAnyAsync<HttpRequestException>(() => Gql(linear, Create, NewIssue(linear, "Lost reply")));
        var next = await Gql(linear, List, []);

        Assert.Equal("Lost reply", linear.Issues.Single().Title);
        Assert.Equal(["ENG-1"], Identifiers(next));
        Assert.False(linear.Switches.NoAnswer);
    }

    [Fact]
    public async Task A_comment_is_read_by_id_with_its_issue_and_a_missing_one_is_entity_not_found()
    {
        await using var linear = await FakeLinear.Start(seed => seed.Team("ENG").Issue("ENG", "Needs a note").Comment("ENG-1", "first"));
        var comment = linear.Comments.Single();
        const string Read = "query Q($id: String!) { comment(id: $id) { id body issue { id } } }";

        var found = await Gql(linear, Read, new JsonObject { ["id"] = comment.Id });
        var missing = await Gql(linear, Read, new JsonObject { ["id"] = "99999999-9999-4999-8999-999999999999" });

        Assert.Equal("first", (string?)found["data"]!["comment"]!["body"]);
        Assert.Equal(linear.Issues.Single().Id, (string?)found["data"]!["comment"]!["issue"]!["id"]);
        Assert.Null(missing["data"]!["comment"]);
        Assert.StartsWith("Entity not found", (string?)missing["errors"]![0]!["message"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task DropBeforeApplying_drops_the_next_mutation_without_applying_it()
    {
        await using var linear = await FakeLinear.Start();
        linear.Switches.DropBeforeApplying = true;

        _ = await Assert.ThrowsAnyAsync<HttpRequestException>(() => Gql(linear, Create, NewIssue(linear, "Never applied")));
        var next = await Gql(linear, List, []);

        Assert.Empty(linear.Issues);
        Assert.Empty(Identifiers(next));
        Assert.False(linear.Switches.DropBeforeApplying);
    }

    [Fact]
    public async Task MoveTeam_renumbers_the_issue_written_last_and_the_old_identifier_still_resolves()
    {
        await using var linear = await FakeLinear.Start(seed => seed.Team("ENG").Team("OPS"));
        _ = await Gql(linear, Create, NewIssue(linear, "Wrong team"));
        var id = linear.Issues.Single().Id;
        linear.Switches.MoveTeam = "OPS";

        var list = await Gql(linear, List, []);
        var byOld = await Gql(linear, One, new JsonObject { ["id"] = "ENG-1" });

        Assert.Equal(["OPS-1"], Identifiers(list));
        Assert.Equal("OPS", (string?)list["data"]!["issues"]!["nodes"]![0]!["team"]!["key"]);
        Assert.Equal(id, (string?)byOld["data"]!["issue"]!["id"]);
    }

    private static JsonObject NewIssue(FakeLinear linear, string title, string? clientId = null)
    {
        var input = new JsonObject { ["teamId"] = linear.TeamId("ENG"), ["title"] = title };
        if (clientId is not null)
        {
            input["id"] = clientId;
        }

        return new JsonObject { ["input"] = input };
    }

    private static async Task<JsonNode> Gql(FakeLinear linear, string query, JsonObject? variables)
    {
        var (status, body) = await Http.Post(linear.Url, linear.Token, new JsonObject { ["query"] = query, ["variables"] = variables }.ToJsonString());
        Assert.Equal(HttpStatusCode.OK, status);
        return body!;
    }

    private static List<string> Identifiers(JsonNode body) =>
        [.. (body["data"]!["issues"] ?? body)["nodes"]!.AsArray().Select(n => (string)n!["identifier"]!)];
}
