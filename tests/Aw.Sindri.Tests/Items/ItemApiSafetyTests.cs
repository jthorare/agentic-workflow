using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.ApiGoldens;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.Items;

/// <summary>What the item routes never let out or in (review #229 SEC-1, SEC-2): the event stream carries no item body and respects a session's repo; a close reason is a name.</summary>
public sealed class ItemApiSafetyTests
{
    private static (string Name, string Value)[] Session(string repo) => [(SindriHeaders.Caller, "session"), (SindriHeaders.Client, "cli"), (SindriHeaders.Repo, repo)];

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task Stream_never_carries_an_item_body()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await GoldenHost.Start(temporary: true, ct, ItemScenarios.ProfileYaml);
        await ViewScenarios.Approve(host, ct);
        _ = await Create(host, "Try red", "demo", body: "a private body");

        var created = await Read(host, Human(host.Token.Value), stopAt: "item.created", ct);

        var data = Assert.Single(created, static e => e.Type == "item.created").Data;
        Assert.DoesNotContain("private body", data, StringComparison.Ordinal);
        Assert.False(JsonNode.Parse(data)!.AsObject().ContainsKey("body"));
        Assert.Contains("Try red", data, StringComparison.Ordinal);
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task A_session_streams_the_item_rows_of_every_repo()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await GoldenHost.Start(temporary: true, ct, ItemScenarios.ProfileYaml);
        await ViewScenarios.Approve(host, ct);
        var other = await Create(host, "Hidden in other", "other", body: null);
        await ViewScenarios.CatchUp(host, ct);
        var closed = await host.Http.Ask(Post($"/sindri/items/{other}/close", """{"reason":"done","itemVersion":1}""", Human(host.Token.Value)));
        Assert.Equal(HttpStatusCode.OK, closed.Status);   // the state-change row exists, so the scoping below is tested
        _ = await Create(host, "Visible in demo", "demo", body: null);

        var seen = await Read(host, Session("demo"), stopAt: "Visible in demo", ct);

        Assert.Contains(seen, e => e.Data.Contains(other, StringComparison.Ordinal));
        Assert.Contains(seen, static e => e.Type == "item.created");
    }

    [Theory(Timeout = 30_000), Trait("Lane", "integration")]
    [InlineData("""{"reason":1,"itemVersion":1}""")]
    [InlineData("""{"reason":7,"itemVersion":1}""")]
    public async Task A_numeric_close_reason_is_a_400(string body)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await GoldenHost.Start(temporary: true, ct, ItemScenarios.ProfileYaml);
        await ViewScenarios.Approve(host, ct);
        var item = await Create(host, "Close me", "demo", body: null);
        await ViewScenarios.CatchUp(host, ct);

        var answer = await host.Http.Ask(Post($"/sindri/items/{item}/close", body, Human(host.Token.Value)));

        Assert.Equal((HttpStatusCode.BadRequest, "SND-CLI-003"), (answer.Status, answer.Code));
    }

    private static async Task<string> Create(GoldenHost host, string title, string repo, string? body)
    {
        var json = new JsonObject { ["title"] = title, ["repo"] = repo, ["body"] = body };
        var answer = await host.Http.Ask(Post("/sindri/items", json.ToJsonString(), Human(host.Token.Value)));
        return JsonDocument.Parse(answer.Body).RootElement.GetProperty("itemId").GetString() ?? throw new InvalidOperationException($"no itemId in {answer.Body}");
    }

    /// <summary>Reads GET /sindri/events from the start until a row's type or data contains <paramref name="stopAt"/> (or 10 s pass).</summary>
    private static async Task<List<(string Type, string Data)>> Read(GoldenHost host, (string Name, string Value)[] headers, string stopAt, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var request = Get("/sindri/events", headers);
        using var response = await host.Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(timeout.Token));
        var seen = new List<(string, string)>();
        var type = string.Empty;
        try
        {
            while (await reader.ReadLineAsync(timeout.Token) is { } line)
            {
                if (line.StartsWith("event: ", StringComparison.Ordinal))
                {
                    type = line["event: ".Length..];
                }
                else if (line.StartsWith("data: ", StringComparison.Ordinal))
                {
                    seen.Add((type, line["data: ".Length..]));
                    if (type.Contains(stopAt, StringComparison.Ordinal) || line.Contains(stopAt, StringComparison.Ordinal))
                    {
                        break;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // the stop row never came: the assertions say what was missing
        }

        return seen;
    }
}
