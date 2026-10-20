using System.Net;
using System.Text;
using System.Web;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Jobs;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>The client's job calls (API P6): the routes, the escaping, the identity headers and the single send of a cancel.</summary>
[Lane(TestLane.Unit)]
public sealed class SindriApiClientJobsTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SindriApiClient Client(HttpMessageHandler handler) => new(new HttpClient(handler), new InMemoryHumanToken("tok-1"), TimeProvider.System);

    [Fact]
    public async Task Jobs_sends_each_filter_as_an_escaped_query_parameter_and_none_by_default()
    {
        var canned = new Canned(HttpStatusCode.OK, GoldenFiles.Api("jobs", "empty"));
        var client = Client(canned);

        _ = await client.Jobs(_api, new JobQuery(), Token);
        _ = await client.Jobs(_api, new JobQuery(State: "running", Kind: "a&b"), Token);

        Assert.Equal(string.Empty, canned.Seen[0].Query);
        var query = HttpUtility.ParseQueryString(canned.Seen[1].Query);
        Assert.Equal(["running", "a&b"], [query["state"], query["kind"]]);
        Assert.All(canned.Seen, u => Assert.Equal("/sindri/jobs", u.AbsolutePath));
    }

    [Fact]
    public async Task Job_and_cancel_escape_the_id_as_one_segment()
    {
        var canned = new Canned(HttpStatusCode.NotFound, GoldenFiles.Api("jobs-id", "not-found"));
        var client = Client(canned);

        _ = await client.Job(_api, "a/b?c", Token);
        _ = await client.CancelJob(_api, "a/b?c", Token);

        Assert.Equal(["/sindri/jobs/a%2Fb%3Fc", "/sindri/jobs/a%2Fb%3Fc/cancel"], canned.Seen.Select(u => u.AbsolutePath), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Cancel_is_a_post_made_as_the_human_from_the_ui_with_the_token()
    {
        var canned = new Canned(HttpStatusCode.OK, GoldenFiles.Api("jobs-id-cancel", "accepted"));

        var accepted = await Client(canned).CancelJob(_api, "JOB-1", Token);

        Assert.Equal(("human", "ui", "tok-1", null), canned.Headers[0]);
        Assert.True(accepted.IsSuccess);
    }

    private sealed class Canned(HttpStatusCode code, string body) : HttpMessageHandler
    {
        public List<Uri> Seen { get; } = [];

        public List<(string? Caller, string? Client, string? Token, string? Terminal)> Headers { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string? Header(string name) => request.Headers.TryGetValues(name, out var values) ? values.Single() : null;
            Seen.Add(request.RequestUri!); // why: HttpClient always sets the URI
            Headers.Add((Header("X-Sindri-Caller"), Header("X-Sindri-Client"), Header("X-Sindri-Token"), Header("X-Sindri-Terminal")));
            return Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
