using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>The client's Start routes: the request as the API pins it on the wire (base64 content), the once-only POST, and what the answer is read for.</summary>
[Lane(TestLane.Unit)]
public sealed class SindriApiClientStartTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SindriApiClient Client(Recorded recorded) => new(new HttpClient(recorded), new InMemoryHumanToken("tok-1"), TimeProvider.System);

    [Fact]
    public void The_request_serialises_to_the_request_golden()
    {
        var wire = AwJson.Serialize(StartGoldens.Request(), SindriApiClient.Json);

        Assert.True(JsonNode.DeepEquals(GoldenFiles.Node("workflow-instances-start", "request"), JsonNode.Parse(wire)), Encoding.UTF8.GetString(wire));
    }

    [Fact]
    public async Task Start_posts_the_request_golden_once_to_the_instances_route()
    {
        var recorded = new Recorded(HttpStatusCode.OK, GoldenFiles.Api("workflow-instances-start", "started"));

        var started = await Client(recorded).StartInstance(_api, StartGoldens.Request(), Token);

        Assert.True(started.IsSuccess);
        var seen = Assert.Single(recorded.Seen);
        Assert.Equal((HttpMethod.Post, "/sindri/workflow/instances"), (seen.Method, seen.Uri.AbsolutePath));
        Assert.True(JsonNode.DeepEquals(GoldenFiles.Node("workflow-instances-start", "request"), JsonNode.Parse(seen.Body)));
        Assert.Equal(("human", "ui", "tok-1"), (seen.Caller, seen.Client, seen.Token));
    }

    [Fact]
    public async Task Start_reads_the_id_the_address_existing_and_finished_and_ignores_the_rest_of_the_engine_view()
    {
        var golden = GoldenFiles.Node("workflow-instances-start", "started");

        var started = (await Client(new Recorded(HttpStatusCode.OK, golden.ToJsonString())).StartInstance(_api, StartGoldens.Request(), Token)).Value;

        Assert.Equal(((string)golden["instance"]!["id"]!, (string)golden["instance"]!["address"]!, false, false), (started.Instance.Id, started.Instance.Address, started.Existing, started.Finished)); // why: the golden names its instance
    }

    [Fact]
    public async Task Recipes_get_the_list_route_and_read_every_row()
    {
        var recorded = new Recorded(HttpStatusCode.OK, GoldenFiles.Api("workflow-recipes", "list"));

        var list = (await Client(recorded).Recipes(_api, Token)).Value;

        var seen = Assert.Single(recorded.Seen);
        Assert.Equal((HttpMethod.Get, "/sindri/workflow/recipes"), (seen.Method, seen.Uri.AbsolutePath));
        Assert.Equal(((string)GoldenFiles.Node("workflow-recipes", "list")["recipes"]![0]!["id"]!, "approved"), (list.Recipes[0].Id, list.Recipes[0].Standing)); // why: the golden has a row
    }

    [Fact]
    public async Task Recipes_at_a_window_get_the_list_route_with_the_window_and_read_the_stats_subset()
    {
        var recorded = new Recorded(HttpStatusCode.OK, GoldenFiles.Api("workflow-recipes", "list"));

        var list = (await Client(recorded).RecipesAt(_api, "7d", Token)).Value;

        var seen = Assert.Single(recorded.Seen);
        Assert.Equal((HttpMethod.Get, "/sindri/workflow/recipes", "?window=7d"), (seen.Method, seen.Uri.AbsolutePath, seen.Uri.Query));
        Assert.Equal(((string)GoldenFiles.Node("workflow-recipes", "list")["window"]!, 0), (list.Window, list.Recipes[0].Stats?.Runs));   // why: the golden is a thirty-day list whose first recipe has no runs
    }

    [Theory]
    [InlineData(null, "?window=30d")]
    [InlineData("e204c3b4ff17", "?window=30d&hash=e204c3b4ff17")]
    public async Task Recipe_stats_get_the_stats_route_with_the_window_and_the_hash_prefix(string? hash, string query)
    {
        var recorded = new Recorded(HttpStatusCode.OK, GoldenFiles.Api("workflow-recipes-id-stats", "workflow-item"));

        var stats = (await Client(recorded).RecipeStats(_api, "workflow.item", "30d", hash, Token)).Value;

        var seen = Assert.Single(recorded.Seen);
        Assert.Equal((HttpMethod.Get, "/sindri/workflow/recipes/workflow.item/stats", query), (seen.Method, seen.Uri.AbsolutePath, seen.Uri.Query));
        Assert.Equal((4, 2, 2700L, false), (stats.Runs, stats.Done, stats.MedianSeconds, stats.ChildOnly));   // why: the golden's own figures
    }

    private sealed record Seen(HttpMethod Method, Uri Uri, string? Caller, string? Client, string? Token, byte[] Body);

    private sealed class Recorded(HttpStatusCode code, string body) : HttpMessageHandler
    {
        public List<Seen> Seen { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string? Header(string name) => request.Headers.TryGetValues(name, out var values) ? values.Single() : null;
            var sent = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            Seen.Add(new Seen(request.Method, request.RequestUri!, Header("X-Sindri-Caller"), Header("X-Sindri-Client"), Header("X-Sindri-Token"), sent)); // why: HttpClient always sets the URI
            return new HttpResponseMessage(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
