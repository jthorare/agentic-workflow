using System.Net;
using System.Text.Json;
using Aw.Context.Core.Linear;
using Aw.Context.Testing.Linear;
using Aw.Sindri.Tests.ApiGoldens;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.LinearWrites;

/// <summary>The comment and create routes' rules that no golden row shows (Linear hub spec §5.2, §5.3): what is sent to Context, what is refused before anything is sent, and the 201.</summary>
public sealed class LinearCommentCreateRouteTests
{
    private const string ClientId = "5d0a4f6e-3b1c-4c0e-9a57-2f1d8c9b7e01";
    private const string Member = "00000000-0000-4000-8000-0000000000a2";
    private const string Review = "00000000-0000-4000-8000-000000000053";

    [Fact, Trait("Lane", "integration")]
    public async Task Create_answers_201_with_the_new_issue_and_sends_the_draft_with_the_teams_id_and_the_client_id()
    {
        var ct = TestContext.Current.CancellationToken;
        var writes = new FakeContextLinearWrites();
        await using var host = await Start(writes, ct);

        var (status, body) = await Ask(host, "/sindri/linear/issues", $$"""{"clientId":"{{ClientId}}","team":"ENG","title":"Document the retry budget","assigneeId":"{{Member}}","stateId":"{{Review}}","priority":2}""");

        Assert.Equal(HttpStatusCode.Created, status);
        var sent = Assert.Single(writes.Calls).Draft!;
        Assert.Equal("00000000-0000-4000-8000-0000000000e1", sent.TeamId.Value);
        Assert.Equal(Member, sent.AssigneeId!.Value.Value);
        Assert.Equal(Review, sent.StateId!.Value.Value);
        Assert.Equal(2, sent.Priority);
        Assert.Equal(ClientId, writes.Calls[0].ClientId!.Value);
        Assert.Equal("applied", Text(body, "outcome"));
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("stateId", "00000000-0000-4000-8000-0000000000ff", "stateId isn't a state of ENG")]
    [InlineData("cycleId", "00000000-0000-4000-8000-0000000000ff", "cycleId isn't a cycle of ENG")]
    [InlineData("labelIds", "[\"00000000-0000-4000-8000-0000000000ff\"]", "labelIds has an id that isn't a label of ENG")]
    public async Task Create_refuses_an_id_that_is_not_in_the_teams_lookups_and_sends_nothing(string field, string value, string detail)
    {
        var ct = TestContext.Current.CancellationToken;
        var writes = new FakeContextLinearWrites();
        await using var host = await Start(writes, ct);
        var json = value.StartsWith('[') ? value : $"\"{value}\"";

        var (status, body) = await Ask(host, "/sindri/linear/issues", $$"""{"clientId":"{{ClientId}}","team":"ENG","title":"T","{{field}}":{{json}}}""");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("SND-LINEAR-006", Text(body, "title"));
        Assert.StartsWith(detail, Text(body, "detail"), StringComparison.Ordinal);
        Assert.Empty(writes.Calls);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Create_in_a_team_the_profile_did_not_pick_is_refused_003_and_sends_nothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var writes = new FakeContextLinearWrites();
        await using var host = await Start(writes, ct);

        var (status, body) = await Ask(host, "/sindri/linear/issues", $$"""{"clientId":"{{ClientId}}","team":"OPS","title":"T"}""");

        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Equal("SND-LINEAR-003", Text(body, "title"));
        Assert.Empty(writes.Calls);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Comment_on_an_issue_of_a_team_the_profile_did_not_pick_is_refused_003_and_sends_nothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var writes = new FakeContextLinearWrites();
        await using var host = await Start(writes, ct);

        var (status, body) = await Ask(host, "/sindri/linear/issues/OPS-40/comments", $$"""{"clientId":"{{ClientId}}","body":"hi"}""");

        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Equal("SND-LINEAR-003", Text(body, "title"));
        Assert.Empty(writes.Calls);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("not-a-uuid", "ENG-12")]
    [InlineData(ClientId, "not-a-key")]
    public async Task Comment_with_a_client_id_or_key_that_is_malformed_is_a_400_and_sends_nothing(string clientId, string key)
    {
        var ct = TestContext.Current.CancellationToken;
        var writes = new FakeContextLinearWrites();
        await using var host = await Start(writes, ct);

        var (status, body) = await Ask(host, $"/sindri/linear/issues/{key}/comments", $$"""{"clientId":"{{clientId}}","body":"hi"}""");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("SND-CLI-003", Text(body, "title"));
        Assert.Empty(writes.Calls);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Comment_sends_the_body_and_client_id_and_answers_the_comment_the_reread_holds()
    {
        var ct = TestContext.Current.CancellationToken;
        var writes = new FakeContextLinearWrites();
        await using var host = await Start(writes, ct);

        var (status, body) = await Ask(host, "/sindri/linear/issues/ENG-12/comments", $$"""{"clientId":"{{ClientId}}","body":"Budget resets at midnight UTC: agreed."}""");

        Assert.Equal(HttpStatusCode.OK, status);
        var call = Assert.Single(writes.Calls);
        Assert.Equal(("Comment", "ENG-12", ClientId, "Budget resets at midnight UTC: agreed."), (call.Method, call.Key!.ToString(), call.ClientId!.Value, call.Body));
        using var json = JsonDocument.Parse(body);
        Assert.Equal("00000000-0000-4000-8000-0000000000f3", json.RootElement.GetProperty("comment").GetProperty("id").GetString());
        Assert.False(json.RootElement.TryGetProperty("warning", out _));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Comment_that_Linear_took_but_the_store_did_not_is_200_with_the_013_warning()
    {
        var ct = TestContext.Current.CancellationToken;
        var writes = new FakeContextLinearWrites();
        writes.Next(new LinearWriteAppliedNotStored(Live: null));
        await using var host = await Start(writes, ct);

        var (status, body) = await Ask(host, "/sindri/linear/issues/ENG-12/comments", $$"""{"clientId":"{{ClientId}}","body":"A comment the store has not seen"}""");

        Assert.Equal(HttpStatusCode.OK, status);
        using var json = JsonDocument.Parse(body);
        Assert.Equal("SND-LINEAR-013", json.RootElement.GetProperty("warning").GetString());
        Assert.Equal("A comment the store has not seen", json.RootElement.GetProperty("comment").GetProperty("body").GetString());
        Assert.Equal(ClientId, json.RootElement.GetProperty("comment").GetProperty("id").GetString());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Unknown_outcome_is_504_with_outcome_and_client_id_members()
    {
        var ct = TestContext.Current.CancellationToken;
        var writes = new FakeContextLinearWrites();
        writes.Next(new LinearWriteUnknown());
        await using var host = await Start(writes, ct);

        var (status, body) = await Ask(host, "/sindri/linear/issues/ENG-12/comments", $$"""{"clientId":"{{ClientId}}","body":"hi"}""");

        Assert.Equal(HttpStatusCode.GatewayTimeout, status);
        Assert.Equal(("SND-LINEAR-011", "unknown", ClientId), (Text(body, "title"), Text(body, "outcome"), Text(body, "clientId")));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_refusal_of_Context_comes_back_with_its_own_code()
    {
        var ct = TestContext.Current.CancellationToken;
        var writes = new FakeContextLinearWrites();
        writes.NextError(LinearHubErrors.Rejected, "Not sent: a comment of 1 to 65536 characters");
        await using var host = await Start(writes, ct);

        var (_, body) = await Ask(host, "/sindri/linear/issues/ENG-12/comments", $$"""{"clientId":"{{ClientId}}","body":"hi"}""");

        Assert.Equal("SND-LINEAR-015", Text(body, "title"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_missing_write_key_is_403_002()
    {
        var ct = TestContext.Current.CancellationToken;
        var writes = new FakeContextLinearWrites();
        writes.NextError(LinearHubErrors.NoWriteKey, "context.sources.linear.writeToken: not set");
        await using var host = await Start(writes, ct);

        var (status, body) = await Ask(host, "/sindri/linear/issues/ENG-12/comments", $$"""{"clientId":"{{ClientId}}","body":"hi"}""");

        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Equal("SND-LINEAR-002", Text(body, "title"));
    }

    private static async Task<GoldenHost> Start(FakeContextLinearWrites writes, CancellationToken ct)
    {
        var host = await GoldenHost.Start(temporary: true, ct, ItemScenarios.ProfileYaml, extra: services => LinearWriteScenarios.Wire(services, writes));
        await ViewScenarios.Approve(host, ct);
        await ViewScenarios.CatchUp(host, ct);
        return host;
    }

    private static async Task<(HttpStatusCode Status, string Body)> Ask(GoldenHost host, string path, string json)
    {
        var answer = await host.Http.Ask(Post(path, json, ItemScenarios.Human(host)));
        return (answer.Status, answer.Body);
    }

    private static string Text(string body, string member)
    {
        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty(member).GetString()!;
    }
}
