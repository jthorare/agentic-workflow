using System.Net;
using System.Text.Json.Nodes;
using Aw.Context.Testing.Fakes;
using Aw.Kernel.Testing;

namespace Aw.Context.Testing.Tests;

/// <summary>The fake Linear GraphQL server replays the recorded project on loopback (spec §10, §14 S-L2, D18).</summary>
[Lane(TestLane.Integration)]
public sealed class FakeLinearServerTests
{
    private const string IssuesQuery =
        "query Issues($filter: IssueFilter, $first: Int, $after: String, $orderBy: PaginationOrderBy) { " +
        "issues(filter: $filter, first: $first, after: $after, orderBy: $orderBy) { " +
        "nodes { id identifier updatedAt comments { nodes { body } } attachments { nodes { url } } } " +
        "pageInfo { hasNextPage endCursor } } }";

    [Fact]
    public async Task Issues_replay_every_recorded_issue_with_comments_and_pull_request_attachments()
    {
        await using var linear = await FakeLinearServer.Start();

        var issues = await Issues(linear, []);

        Assert.Equal(["SPL-101", "SPL-102", "SPL-103", "SPL-104", "SPL-105", "SPL-106"], Identifiers(issues));
        Assert.Equal(3, issues["nodes"]![4]!["comments"]!["nodes"]!.AsArray().Count);
        Assert.EndsWith("/pull/5", (string?)issues["nodes"]![4]!["attachments"]!["nodes"]![0]!["url"], StringComparison.Ordinal);
        Assert.False((bool)issues["pageInfo"]!["hasNextPage"]!);
    }

    [Fact]
    public async Task Issues_filter_by_team_key_and_updated_at()
    {
        await using var linear = await FakeLinearServer.Start();
        var filter = new JsonObject
        {
            ["team"] = new JsonObject { ["key"] = new JsonObject { ["eq"] = "SPL" } },
            ["updatedAt"] = new JsonObject { ["gt"] = "2026-07-22T10:20:00.000Z" },
        };

        Assert.Equal(["SPL-103", "SPL-105", "SPL-106"], Identifiers(await Issues(linear, new JsonObject { ["filter"] = filter.DeepClone() })));

        filter["team"]!["key"]!["eq"] = "OTHER";
        Assert.Empty(Identifiers(await Issues(linear, new JsonObject { ["filter"] = filter })));
    }

    [Fact]
    public async Task Issues_filter_with_or_and_some_over_a_connection()
    {
        await using var linear = await FakeLinearServer.Start();
        var filter = new JsonObject
        {
            ["labels"] = new JsonObject { ["some"] = new JsonObject { ["name"] = new JsonObject { ["eq"] = "layout" } } },
            ["or"] = new JsonArray(
                new JsonObject { ["identifier"] = new JsonObject { ["eq"] = "SPL-103" } },
                new JsonObject { ["state"] = new JsonObject { ["type"] = new JsonObject { ["neq"] = "completed" } } }),
        };

        Assert.Equal(["SPL-103", "SPL-106"], Identifiers(await Issues(linear, new JsonObject { ["filter"] = filter })));
    }

    [Fact]
    public async Task Issues_page_with_first_and_after_in_updated_at_order()
    {
        await using var linear = await FakeLinearServer.Start();

        var first = await Issues(linear, new JsonObject { ["first"] = 4, ["orderBy"] = "updatedAt" });
        var rest = await Issues(linear, new JsonObject { ["first"] = 4, ["orderBy"] = "updatedAt", ["after"] = (string?)first["pageInfo"]!["endCursor"] });
        var byCreation = await Issues(linear, new JsonObject { ["first"] = 4 });
        var (badOrder, _) = await Post(linear, IssuesQuery, new JsonObject { ["orderBy"] = "title" });

        Assert.Equal(["SPL-101", "SPL-102", "SPL-103", "SPL-104"], Identifiers(byCreation));
        Assert.Equal(HttpStatusCode.BadRequest, badOrder);
        Assert.Equal(["SPL-101", "SPL-102", "SPL-104", "SPL-103"], Identifiers(first));
        Assert.True((bool)first["pageInfo"]!["hasNextPage"]!);
        Assert.Equal(["SPL-105", "SPL-106"], Identifiers(rest));
        Assert.False((bool)rest["pageInfo"]!["hasNextPage"]!);
    }

    [Fact]
    public async Task Issue_is_found_by_uuid_or_identifier_and_a_missing_one_is_a_graphql_error()
    {
        await using var linear = await FakeLinearServer.Start();
        const string query = "query One($id: String!) { issue(id: $id) { id identifier } }";

        var (Status, Body) = await Post(linear, query, new JsonObject { ["id"] = "SPL-104" });
        var byId = await Post(linear, query, new JsonObject { ["id"] = (string?)Body!["data"]!["issue"]!["id"] });
        var missing = await Post(linear, "{ issue(id: \"SPL-999\") { id } }", variables: null);

        Assert.Equal("SPL-104", (string?)byId.Body!["data"]!["issue"]!["identifier"]);
        Assert.Equal(HttpStatusCode.OK, missing.Status);
        Assert.Contains("SPL-999", (string?)missing.Body!["errors"]![0]!["message"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Projects_documents_and_project_updates_are_served()
    {
        await using var linear = await FakeLinearServer.Start();
        const string query = "{ projects { nodes { id } } documents { nodes { title project { id } } } projectUpdates(first: 1) { nodes { body } pageInfo { hasNextPage } } }";

        var data = (await Post(linear, query, variables: null)).Body!["data"]!;

        _ = Assert.Single(data["projects"]!["nodes"]!.AsArray());
        Assert.Equal("PRD: Split panes", (string?)data["documents"]!["nodes"]![0]!["title"]);
        Assert.True((bool)data["projectUpdates"]!["pageInfo"]!["hasNextPage"]!);
    }

    [Fact]
    public async Task An_unknown_root_field_or_a_mutation_is_a_clear_400()
    {
        await using var linear = await FakeLinearServer.Start();

        var (Status, Body) = await Post(linear, "{ viewer { id } }", variables: null);
        var mutation = await Post(linear, "mutation { issueCreate(input: {}) { success } }", variables: null);

        Assert.Equal(HttpStatusCode.BadRequest, Status);
        var message = (string?)Body!["errors"]![0]!["message"];
        Assert.Contains("\"viewer\"", message, StringComparison.Ordinal);
        Assert.Contains("issues", message, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, mutation.Status);
        Assert.Contains("read-only", (string?)mutation.Body!["errors"]![0]!["message"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_filter_comparing_a_date_with_a_non_date_is_a_clear_400()
    {
        await using var linear = await FakeLinearServer.Start();
        var filter = new JsonObject { ["updatedAt"] = new JsonObject { ["gt"] = "P1W" } };

        var (status, body) = await Post(linear, IssuesQuery, new JsonObject { ["filter"] = filter });

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("P1W", (string?)body!["errors"]![0]!["message"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_missing_or_wrong_token_is_401()
    {
        await using var linear = await FakeLinearServer.Start();

        var (Status, Body) = await Http.Post(linear.Url, authorization: null, "{\"query\":\"{ issues { nodes { id } } }\"}");
        var wrong = await Http.Post(linear.Url, "not-the-right-one", "{\"query\":\"{ issues { nodes { id } } }\"}");

        Assert.Equal(HttpStatusCode.Unauthorized, Status);
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.Status);
    }

    [Fact]
    public async Task It_listens_on_loopback_and_never_logs_the_authorization_header()
    {
        const string planted = "planted-linear-7f3a";
        await using var linear = await FakeLinearServer.Start(new FakeServerOptions { Token = planted });

        _ = await Issues(linear, []);
        _ = await Http.Post(linear.Url, "Bearer " + planted, "{\"query\":\"{ viewer { id } }\"}");
        _ = await Http.Post(linear.Url, "wrong-" + planted, "{\"query\":\"{ issues { nodes { id } } }\"}");

        Assert.Equal("127.0.0.1", linear.Url.Host);
        Assert.Equal(3, linear.Requests.Count);
        Assert.Equal("issues", linear.Requests[0].Operation);
        Assert.NotEmpty(linear.Log);
        Assert.DoesNotContain(linear.Log, line => line.Contains(planted, StringComparison.Ordinal));
        Assert.DoesNotContain(linear.Requests, r => r.ToString().Contains(planted, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Replies_carry_only_the_selected_fields_under_their_aliases_and_nested_first_cuts_a_connection()
    {
        await using var linear = await FakeLinearServer.Start();
        const string query = "query($id: String!) { one: issue(id: $id) { key: identifier comments(first: 1) { nodes { body } } } }";

        var one = (await Post(linear, query, new JsonObject { ["id"] = "SPL-105" })).Body!["data"]!["one"]!.AsObject();

        Assert.Equal(["key", "comments"], one.Select(p => p.Key), StringComparer.Ordinal);
        Assert.Equal("SPL-105", (string?)one["key"]);
        Assert.Equal(["body"], Assert.Single(one["comments"]!["nodes"]!.AsArray())!.AsObject().Select(p => p.Key), StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("{ issues { nodes { identifier estimate } } }", "estimate")]
    [InlineData("{ issues { nodes { identifier team } } }", "team")]
    [InlineData("{ issues { nodes { identifier { id } } } }", "scalar")]
    [InlineData("{ issues }", "subfields")]
    [InlineData("{ issues { nodes { ...IssueFields } } }", "Fragments")]
    [InlineData("query { issue(id: $id) { id } }", "$id")]
    [InlineData("{ issue(id: \"SPL-1\\q\") { id } }", "escape")]
    [InlineData("{ issue(id: \"SPL-10\\u 035\") { id } }", "escape")]
    [InlineData("{ issues(first: 1e999) { nodes { id } } }", "1e999")]
    [InlineData("{ issues(orderBy: 5) { nodes { id } } }", "orderBy")]
    [InlineData("{ issues(after: 5) { nodes { id } } }", "after")]
    public async Task A_selection_or_variable_the_fake_cannot_answer_truthfully_is_a_clear_400(string query, string named)
    {
        await using var linear = await FakeLinearServer.Start();

        var (status, body) = await Post(linear, query, variables: null);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains(named, (string?)body!["errors"]![0]!["message"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Variable_defaults_apply_when_the_request_leaves_the_variable_out()
    {
        await using var linear = await FakeLinearServer.Start();
        const string query = "query($first: Int = 3, $order: PaginationOrderBy = updatedAt) { issues(first: $first, orderBy: $order) { nodes { identifier } } }";
        const string inline = "{ issues(first: 3, orderBy: updatedAt) { nodes { identifier } } }";

        var (status, body) = await Post(linear, query, []);
        var literal = (await Post(linear, inline, variables: null)).Body!["data"]!["issues"]!;
        var escaped = (await Post(linear, "{ issue(id: \"SPL-10\\u0035\") { identifier } }", variables: null)).Body!["data"]!["issue"]!;

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(["SPL-101", "SPL-102", "SPL-104"], Identifiers(body!["data"]!["issues"]!));
        Assert.Equal(["SPL-101", "SPL-102", "SPL-104"], Identifiers(literal));
        Assert.Equal("SPL-105", (string?)escaped["identifier"]);
    }

    [Theory]
    [InlineData("{\"completedAt\":{\"null\":\"yes\"}}", "Boolean")]
    [InlineData("{\"stateName\":{\"eq\":\"Done\"}}", "stateName")]
    [InlineData("{\"labels\":{\"name\":{\"eq\":\"layout\"}}}", "some")]
    [InlineData("{\"team\":{\"key\":{\"eq\":\"OTHER\"}},\"bogus\":{\"eq\":1}}", "bogus")]
    [InlineData("{\"team\":{\"key\":{\"eq\":\"OTHER\"}},\"updatedAt\":{\"gt\":\"P1W\"}}", "P1W")]
    [InlineData("{\"or\":[{\"team\":{\"key\":{\"eq\":\"SPL\"}}},{\"priority\":{\"gt\":\"1\"}}]}", "different types")]
    [InlineData("{\"labels\":{\"some\":{\"or\":[{\"name\":{\"eq\":\"layout\"}},{\"bogus\":{\"eq\":1}}]}}}", "bogus")]
    [InlineData("{\"identifier\":{\"in\":[\"SPL-101\",\"SPL-102\",\"SPL-103\",\"SPL-104\",\"SPL-105\",\"SPL-106\",5]}}", "different types")]
    [InlineData("{\"completedAt\":{\"eq\":null}}", "'null' comparator")]
    [InlineData("{\"completedAt\":{\"neq\":null}}", "Filter 'neq' compares with null; use the 'null' comparator")]
    [InlineData("{\"completedAt\":{\"in\":[null]}}", "Filter 'in' compares with null")]
    [InlineData("{\"completedAt\":{\"gt\":{\"x\":1}}}", "string, number or Boolean")]
    [InlineData("{\"title\":{\"contains\":5}}", "contains")]
    public async Task A_filter_the_fake_cannot_evaluate_truthfully_is_a_clear_400(string filter, string named)
    {
        await using var linear = await FakeLinearServer.Start();

        var (status, body) = await Post(linear, IssuesQuery, new JsonObject { ["filter"] = JsonNode.Parse(filter) });

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains(named, (string?)body!["errors"]![0]!["message"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_nested_connection_pages_with_first_and_a_computed_page_info()
    {
        await using var linear = await FakeLinearServer.Start();
        const string query = "{ issue(id: \"SPL-105\") { comments(first: 2) { nodes { id } pageInfo { hasNextPage endCursor } } } }";

        var comments = (await Post(linear, query, variables: null)).Body!["data"]!["issue"]!["comments"]!;

        Assert.Equal(2, comments["nodes"]!.AsArray().Count);
        Assert.True((bool)comments["pageInfo"]!["hasNextPage"]!);
        Assert.Equal((string?)comments["nodes"]![1]!["id"], (string?)comments["pageInfo"]!["endCursor"]);
    }

    [Theory]
    [InlineData(32, false)]
    [InlineData(33, true)]
    public async Task Selection_sets_nest_at_most_32_deep(int depth, bool refused)
    {
        await using var linear = await FakeLinearServer.Start();
        var query = "{ issues " + string.Concat(Enumerable.Repeat("{ nodes ", depth - 2)) + "{ id" + new string('}', depth);

        var (status, body) = await Post(linear, query, variables: null);

        Assert.Equal(HttpStatusCode.BadRequest, status); // at 32 the reader accepts it and shaping refuses nodes.nodes
        Assert.Equal(refused, ((string?)body!["errors"]![0]!["message"])!.Contains("deeper", StringComparison.Ordinal));
    }

    private static async Task<JsonNode> Issues(FakeLinearServer linear, JsonObject variables)
    {
        var (status, body) = await Post(linear, IssuesQuery, variables);
        Assert.Equal(HttpStatusCode.OK, status);
        return body!["data"]!["issues"]!;
    }

    private static Task<(HttpStatusCode Status, JsonNode? Body)> Post(FakeLinearServer linear, string query, JsonObject? variables) =>
        Http.Post(linear.Url, linear.Token, new JsonObject { ["query"] = query, ["variables"] = variables }.ToJsonString());

    private static List<string> Identifiers(JsonNode connection) =>
        [.. connection["nodes"]!.AsArray().Select(n => (string)n!["identifier"]!)];
}
