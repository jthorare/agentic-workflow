using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Projects;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>The client's project calls (projects page spec §6.2): the routes, the id escaping, the cards the goldens carry and each card's truth (issue 3718).</summary>
[Lane(TestLane.Unit)]
public sealed class SindriApiClientProjectsTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SindriApiClient Client(HttpMessageHandler handler) => new(new HttpClient(handler), new InMemoryHumanToken("tok-1"), TimeProvider.System);

    [Fact]
    public void The_routes_escape_a_linear_key_into_one_segment_and_all_adds_the_query()
    {
        Assert.Equal("/sindri/projects/linear%3Aproject%2F00000000-0000-4000-8000-000000000001", ProjectRoutes.One("linear:project/00000000-0000-4000-8000-000000000001"));
        Assert.Equal("/sindri/projects/prj_00000000000000000000000008", ProjectRoutes.One("prj_00000000000000000000000008"));
        Assert.Equal("/sindri/projects?all=true", ProjectRoutes.List(all: true));
        Assert.Equal("/sindri/projects", ProjectRoutes.List(all: false));
    }

    [Fact]
    public async Task Projects_and_Project_call_the_routes_with_the_escaped_id()
    {
        var canned = new Canned(HttpStatusCode.OK, GoldenFiles.Api("projects", "mixed"));
        var client = Client(canned);

        _ = await client.Projects(_api, all: false, Token);
        _ = await client.Projects(_api, all: true, Token);
        _ = await client.Project(_api, "linear:project/abc", Token);

        Assert.Equal(["/sindri/projects", "/sindri/projects", "/sindri/projects/linear%3Aproject%2Fabc"], canned.Seen.Select(u => u.AbsolutePath), StringComparer.Ordinal);
        Assert.Equal(["", "?all=true", ""], canned.Seen.Select(u => u.Query), StringComparer.Ordinal);
    }

    [Fact]
    public async Task LinearProjects_calls_the_linear_route_and_reads_the_four_goldens()
    {
        Assert.Equal("/sindri/projects/linear", ProjectRoutes.Linear);
        var some = (await Client(new Canned(HttpStatusCode.OK, GoldenFiles.Api("projects-linear", "some"))).LinearProjects(_api, Token)).Value;
        var empty = (await Client(new Canned(HttpStatusCode.OK, GoldenFiles.Api("projects-linear", "empty"))).LinearProjects(_api, Token)).Value;
        var down = (await Client(new Canned(HttpStatusCode.OK, GoldenFiles.Api("projects-linear", "context-down"))).LinearProjects(_api, Token)).Value;
        var untrusted = (await Client(new Canned(HttpStatusCode.OK, GoldenFiles.Api("projects-linear", "untrusted-name"))).LinearProjects(_api, Token)).Value;

        Assert.Equal((null, 3, "Ana Ruiz", "backlog", 2, "https://linear.app/acme/project/3"), (some.Error, some.Projects.Count, some.Projects[0].Lead, some.Projects[0].State, some.Projects[0].OpenIssues, some.Projects[0].Url));
        Assert.Null(some.Projects[1].Lead);
        Assert.Equal((null, 0), (empty.Error, empty.Projects.Count));
        Assert.Equal(("Context is not answering", 0), (down.Error, down.Projects.Count));
        Assert.StartsWith("<untrusted", Assert.Single(untrusted.Projects).Name, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_mixed_golden_reads_the_leads_the_note_the_finished_card_and_the_hidden_list()
    {
        var list = (await Client(new Canned(HttpStatusCode.OK, GoldenFiles.Api("projects", "mixed"))).Projects(_api, all: true, Token)).Value;

        var led = list.Projects[0];
        Assert.Equal(("led", true, "atRisk", "2026-01-31", 12), (led.Origin, led.Lead!.You, led.Health, led.Target, led.OpenIssues));
        var attached = list.Projects.Single(p => p.Origin == "attached");
        Assert.Equal(("Waiting on the vendor", "Sam Lee", 2), (attached.Notes, attached.Lead!.Name, attached.Links.Count));
        Assert.Null(list.Projects.Single(p => p.Origin == "handMade").Lead);
        _ = Assert.Single(list.Finished);
        Assert.True(list.Finished[0].FinishedAt is not null);
        _ = Assert.Single(list.Hidden);
        Assert.Equal(("Jo Smith", 3, null), (list.Viewer, list.Freshness.Count, list.Empty));
    }

    [Fact]
    public async Task Project_reads_the_one_card_with_its_hidden_flag()
    {
        var one = (await Client(new Canned(HttpStatusCode.OK, GoldenFiles.Api("projects-id", "attached"))).Project(_api, "prj_00000000000000000000000008", Token)).Value;

        Assert.Equal(("Mobile app", "Waiting on the vendor", false), (one.Project.Name, one.Project.Notes, one.Hidden));
    }

    [Fact]
    public async Task A_card_with_truth_reads_every_pr_state_the_counts_the_agents_and_the_health()
    {
        var served = Mixed();
        served["projects"]![0]!["truth"] = JsonNode.Parse(Truth("ciRed", "atRisk"));

        var truth = (await Read(served)).Projects[0].Truth!;

        Assert.Equal(["draft", "ready", "ciRed", "unknown", "waiting"], truth.OpenPrs.Select(p => p.State), StringComparer.Ordinal);
        var red = truth.OpenPrs[2];
        Assert.Equal(("https://github.com/acme/shop/pull/3", "acme/shop", 3, "Fix the checkout tests", new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero), null), (red.Url, red.Repo, red.Number, red.Title, red.DecidedAt, red.WaitingOn));
        var waiting = truth.OpenPrs[4];
        Assert.Equal((null, "ana, sam"), (waiting.DecidedAt, waiting.WaitingOn));
        Assert.Equal((2, 3, 5, 1), (truth.OpenItems, truth.DoneItems, truth.TotalItems, truth.RunningAgents));
        var health = truth.Health!;
        Assert.Equal(("atRisk", "At risk: 1 of 4 PRs red, 1 not checked yet"), (health.Level, health.Reason));
    }

    [Fact]
    public async Task A_card_without_truth_still_reads_and_its_truth_is_null()
    {
        var served = Mixed();
        foreach (var card in served["projects"]!.AsArray().Concat(served["finished"]!.AsArray()).Concat(served["hidden"]!.AsArray()))
        {
            _ = card!.AsObject().Remove("truth");
        }

        var list = await Read(served);

        Assert.Equal(3, list.Projects.Count);
        Assert.All(list.Projects.Concat(list.Finished).Concat(list.Hidden), c => Assert.Null(c.Truth));
    }

    [Fact]
    public async Task A_pr_state_or_health_level_this_build_does_not_know_is_kept_as_served()
    {
        var served = Mixed();
        served["projects"]![0]!["truth"] = JsonNode.Parse(Truth("conflict", "stalled"));

        var truth = (await Read(served)).Projects[0].Truth!;

        Assert.Equal(("conflict", "stalled"), (truth.OpenPrs[2].State, truth.Health!.Level));
    }

    [Fact]
    public async Task A_truth_with_no_open_pr_has_no_health()
    {
        var served = Mixed();
        served["projects"]![0]!["truth"] = JsonNode.Parse("""{"openPrs":[],"openItems":0,"doneItems":0,"totalItems":0,"runningAgents":0,"health":null}""");

        var truth = (await Read(served)).Projects[0].Truth!;

        Assert.Equal((0, null), (truth.OpenPrs.Count, truth.Health));
    }

    private static JsonNode Mixed() => JsonNode.Parse(GoldenFiles.Api("projects", "mixed"))!; // why: the golden is a JSON document

    private static async Task<ProjectList> Read(JsonNode served) => (await Client(new Canned(HttpStatusCode.OK, served.ToJsonString())).Projects(_api, all: true, Token)).Value;

    // One open PR per state, the third in redState; two open items, three done, one running agent.
    private static string Truth(string redState, string level) => $$"""
        {
          "openPrs": [
            { "url": "https://github.com/acme/shop/pull/1", "repo": "acme/shop", "number": 1, "title": "Try the new cart", "state": "draft", "decidedAt": null, "waitingOn": null },
            { "url": "https://github.com/acme/shop/pull/2", "repo": "acme/shop", "number": 2, "title": "Bump the payments SDK", "state": "ready", "decidedAt": null, "waitingOn": null },
            { "url": "https://github.com/acme/shop/pull/3", "repo": "acme/shop", "number": 3, "title": "Fix the checkout tests", "state": "{{redState}}", "decidedAt": "2026-01-01T08:00:00.0000000Z", "waitingOn": null },
            { "url": "https://github.com/acme/shop/pull/4", "repo": "acme/shop", "number": 4, "title": "Log refunds", "state": "unknown", "decidedAt": null, "waitingOn": null },
            { "url": "https://github.com/acme/api/pull/5", "repo": "acme/api", "number": 5, "title": "Add the refund route", "state": "waiting", "decidedAt": null, "waitingOn": "ana, sam" }
          ],
          "openItems": 2,
          "doneItems": 3,
          "totalItems": 5,
          "runningAgents": 1,
          "health": { "level": "{{level}}", "reason": "At risk: 1 of 4 PRs red, 1 not checked yet" }
        }
        """;

    private sealed class Canned(HttpStatusCode code, string body) : HttpMessageHandler
    {
        public List<Uri> Seen { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Seen.Add(request.RequestUri!); // why: HttpClient always sets the URI
            return Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
