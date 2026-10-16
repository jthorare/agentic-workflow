using System.Net;
using System.Text.Json;
using Aw.Sindri.Tests.ApiGoldens;
using Aw.Sindri.Tests.Callers;

namespace Aw.Sindri.Tests.CloseItem;

/// <summary>
/// API P4 unit G through the running host: closing an item with a pending decision voids the decision (with the voided notice
/// and 'cleared'), clears its needs-you row, and a later decide is a 409 "the item was closed"; the item reads
/// 'needs-decision' while a decision is pending and its prior state after.
/// </summary>
public sealed class CloseItemVoidsDecisionsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<(GoldenHost Host, string Item, string Decision)> Pending()
    {
        var host = await GoldenHost.Start(temporary: true, Token, ItemScenarios.ProfileYaml);
        await ViewScenarios.Approve(host, Token);
        var item = await ItemScenarios.Create(host, "Try the queue", "demo");
        var (_, decision) = await RequestCalls.Filed(host, "Split the work.", ItemScenarios.Session("demo"), Token, item: item);
        return (host, item, decision);
    }

    private static async Task<JsonElement> Read(GoldenHost host, string route) =>
        JsonDocument.Parse(await RequestCalls.Read(host, route, ItemScenarios.Human(host), Token)).RootElement.Clone();

    [Fact, Trait("Lane", "integration")]
    public async Task Closing_an_item_voids_its_pending_decision_and_clears_the_needs_you_row()
    {
        var (host, item, decision) = await Pending();
        await using var scope = host;
        Assert.Equal(1, (await Read(host, "/sindri/needs-you")).GetProperty("rows").GetArrayLength());

        var closed = await host.Http.Ask(CallerRequests.Post($"/sindri/items/{item}/close", """{"reason":"done","itemVersion":1}""", ItemScenarios.Human(host)));
        Assert.Equal(HttpStatusCode.OK, closed.Status);
        await ViewScenarios.CatchUp(host, Token);

        Assert.Equal(0, (await Read(host, "/sindri/needs-you")).GetProperty("rows").GetArrayLength());
        var voided = (await Read(host, "/sindri/decisions?pending=false")).GetProperty("decisions")[0];
        Assert.Equal(decision, voided.GetProperty("id").GetString());
        Assert.Equal("voided", voided.GetProperty("state").GetString());
        Assert.Equal("voided: the item was closed", voided.GetProperty("notice").GetString());
        Assert.Equal("voided: the item was closed", voided.GetProperty("cleared").GetString());
        Assert.Equal(JsonValueKind.Array, voided.GetProperty("actions").ValueKind);
        Assert.Equal(0, voided.GetProperty("actions").GetArrayLength());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Deciding_a_voided_decision_is_409_the_item_was_closed()
    {
        var (host, item, decision) = await Pending();
        await using var scope = host;
        _ = await host.Http.Ask(CallerRequests.Post($"/sindri/items/{item}/close", """{"reason":"cancelled","itemVersion":1}""", ItemScenarios.Human(host)));

        var answer = await RequestCalls.Decide(host, decision, "accept", note: null, ItemScenarios.Human(host), Token);

        Assert.Equal(HttpStatusCode.Conflict, answer.Status);
        Assert.Contains("the item was closed", answer.Body, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_item_reads_needs_decision_while_a_decision_is_pending_and_new_after_it_is_decided()
    {
        var (host, item, decision) = await Pending();
        await using var scope = host;
        Assert.Equal("needs-decision", (await Read(host, "/sindri/items/" + item)).GetProperty("state").GetString());

        _ = await RequestCalls.Decide(host, decision, "decline", note: null, ItemScenarios.Human(host), Token);

        Assert.Equal("new", (await Read(host, "/sindri/items/" + item)).GetProperty("state").GetString());
    }
}
