using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Aw.Sindri.Tests.ApiGoldens;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.NeedsYou;

/// <summary>
/// GET /sindri/needs-you answers the panel in one read and answers 304 when nothing changed (issue #2789). Today the app reads
/// /needs-you and then every pending decision from /decisions?pending=true on every event, to find a decision row's options: about
/// 130 ms and 480 KB per refresh at 100k events. The decision row now carries its decision, and every other row says "decision": null.
/// </summary>
public sealed class NeedsYouDecisionTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task A_decision_row_carries_its_decision_as_the_pending_list_shows_it_and_other_rows_say_null()
    {
        await using var host = await Host();
        var (_, decision) = await RequestCalls.Filed(host, "Split the checkout work into two items.", ItemScenarios.Session("demo"), Token);

        using var needs = JsonDocument.Parse(await ViewScenarios.Read(host, "/sindri/needs-you"));
        using var pending = JsonDocument.Parse(await ViewScenarios.Read(host, "/sindri/decisions?pending=true"));

        var rows = needs.RootElement.GetProperty("rows").EnumerateArray().ToList();
        var row = rows.Single(r => r.GetProperty("kind").GetString() == "decision");
        var served = row.GetProperty("decision");
        var listed = pending.RootElement.GetProperty("decisions").EnumerateArray().Single(d => d.GetProperty("id").GetString() == decision);
        Assert.Equal(JsonValueKind.Object, served.ValueKind);
        Assert.Equal(row.GetProperty("subject").GetProperty("id").GetString(), served.GetProperty("id").GetString());
        foreach (var key in new[] { "id", "question", "filedBy", "recommended" })
        {
            Assert.Equal(listed.GetProperty(key).GetString(), served.GetProperty(key).GetString());
        }

        Assert.Equal(Options(listed), Options(served));
        Assert.All(rows.Where(r => r.GetProperty("kind").GetString() != "decision"), r => Assert.Equal(JsonValueKind.Null, r.GetProperty("decision").ValueKind));
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task The_same_tag_answers_304_with_no_body_and_a_new_request_brings_the_body_back()
    {
        await using var host = await Host();
        _ = await RequestCalls.Filed(host, "Split the checkout work into two items.", ItemScenarios.Session("demo"), Token);
        await ViewScenarios.CatchUp(host, Token);

        using var first = await host.Http.SendAsync(Get("/sindri/needs-you", ItemScenarios.Human(host)), Token);
        var tag = first.Headers.ETag;
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.NotNull(tag);

        using var same = await host.Http.SendAsync(Conditional(host, tag), Token);
        Assert.Equal(HttpStatusCode.NotModified, same.StatusCode);
        Assert.Equal(string.Empty, await same.Content.ReadAsStringAsync(Token));

        _ = await RequestCalls.Filed(host, "Add a task for the logout flow.", ItemScenarios.Session("demo"), Token, kind: "add-task");
        using var moved = await host.Http.SendAsync(Conditional(host, tag), Token);
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.NotEqual(tag, moved.Headers.ETag);
        Assert.Equal(2, JsonDocument.Parse(await moved.Content.ReadAsStringAsync(Token)).RootElement.GetProperty("rows").EnumerateArray().Count(r => r.GetProperty("kind").GetString() == "decision"));
    }

    private static async Task<GoldenHost> Host()
    {
        var host = await GoldenHost.Start(temporary: true, Token, ItemScenarios.ProfileYaml);
        await ViewScenarios.Approve(host, Token);
        return host;
    }

    private static HttpRequestMessage Conditional(GoldenHost host, EntityTagHeaderValue tag)
    {
        var request = Get("/sindri/needs-you", ItemScenarios.Human(host));
        request.Headers.IfNoneMatch.Add(tag);
        return request;
    }

    private static List<string> Options(JsonElement decision) =>
        [.. decision.GetProperty("options").EnumerateArray().Select(static o =>
            $"{o.GetProperty("id").GetString()}|{o.GetProperty("label").GetString()}|{o.GetProperty("cli").GetString()}|{o.GetProperty("rejectLike").GetBoolean()}")];
}
