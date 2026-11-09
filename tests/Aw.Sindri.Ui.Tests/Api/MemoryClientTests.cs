using System.Net;
using System.Text;
using System.Web;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Memory;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>The client's memory calls: method, path, escaped query and the parsed answer.</summary>
[Lane(TestLane.Unit)]
public sealed class MemoryClientTests
{
    private const string Store = """{"name":"team-notes","description":null,"createdBy":"owner","createdAt":"2026-11-08T10:00:00+00:00","facts":2}""";
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ListMemories_gets_the_stores()
    {
        var canned = new Canned($$"""{"memories":[{{Store}}]}""");

        var list = await Client(canned).ListMemories(_api, Token);

        Assert.Equal([("GET", "/sindri/memories")], canned.Seen.Select(s => (s.Method, s.Path)));
        Assert.Equal(("team-notes", 2), (list.Value.Memories[0].Name, list.Value.Memories[0].Facts));
    }

    [Fact]
    public async Task CreateMemory_posts_the_body_and_reads_the_store()
    {
        var canned = new Canned($$"""{"store":{{Store}}}""");

        var created = await Client(canned).CreateMemory(_api, new CreateMemoryBody("team-notes", "what we learned"), Token);

        Assert.Equal(("POST", "/sindri/memories"), (canned.Seen[0].Method, canned.Seen[0].Path));
        Assert.Contains("\"description\":\"what we learned\"", canned.Seen[0].Body, StringComparison.Ordinal);
        Assert.Equal("team-notes", created.Value.Store.Name);
    }

    [Fact]
    public async Task SearchMemory_sends_query_level_and_limit_escaped_and_reads_mode_and_facts()
    {
        var canned = new Canned("""{"facts":[{"id":"f1","text":"use heavy","tags":["build"],"provenance":{"workflow":null,"run":null,"item":null,"session":null},"expiresAt":null,"refreshedAt":"2026-11-08T10:00:00+00:00","score":0.9}],"count":1,"mode":"keyword","note":"no embeddings"}""");

        var found = await Client(canned).SearchMemory(_api, new MemorySearch("team-notes", "a&b c", "deep", 5), Token);

        Assert.Equal(("GET", "/sindri/memories/team-notes/search"), (canned.Seen[0].Method, canned.Seen[0].Path));
        var query = HttpUtility.ParseQueryString(canned.Seen[0].Query);
        Assert.Equal(["a&b c", "deep", "5"], [query["query"], query["level"], query["limit"]]);
        Assert.Equal(("keyword", 1, "f1"), (found.Value.Mode, found.Value.Count, found.Value.Facts[0].Key));
    }

    [Fact]
    public async Task SearchMemory_leaves_out_a_null_level_and_limit()
    {
        var canned = new Canned("""{"facts":[],"count":0,"mode":"semantic","note":null}""");

        _ = await Client(canned).SearchMemory(_api, new MemorySearch("team-notes", "x"), Token);

        Assert.Equal("?query=x", canned.Seen[0].Query);
    }

    [Fact]
    public async Task WriteMemoryFact_posts_to_facts_and_reads_id_and_action()
    {
        var canned = new Canned("""{"id":"f2","action":"written"}""");

        var written = await Client(canned).WriteMemoryFact(_api, "team-notes", new WriteMemoryFactBody("use heavy", ["build"], ExpiresAt: null, Provenance: null), Token);

        Assert.Equal(("POST", "/sindri/memories/team-notes/facts"), (canned.Seen[0].Method, canned.Seen[0].Path));
        Assert.Contains("\"text\":\"use heavy\"", canned.Seen[0].Body, StringComparison.Ordinal);
        Assert.Equal(("f2", "written"), (written.Value.Key, written.Value.Action));
    }

    [Fact]
    public async Task ForgetMemoryFacts_deletes_with_the_fact_tag_and_expired_in_the_query()
    {
        var canned = new Canned("""{"deleted":3}""");
        var client = Client(canned);

        var byFact = await client.ForgetMemoryFacts(_api, new MemoryForget("team-notes", FactId: "f1"), Token);
        _ = await client.ForgetMemoryFacts(_api, new MemoryForget("team-notes", Tag: "a&b"), Token);
        _ = await client.ForgetMemoryFacts(_api, new MemoryForget("team-notes", Expired: true), Token);

        Assert.All(canned.Seen, s => Assert.Equal(("DELETE", "/sindri/memories/team-notes/facts"), (s.Method, s.Path)));
        Assert.Equal(["?fact=f1", "?tag=a%26b", "?expired=true"], canned.Seen.Select(s => s.Query), StringComparer.Ordinal);
        Assert.Equal(3, byFact.Value.Deleted);
    }

    [Fact]
    public async Task A_slash_in_a_store_name_stays_inside_one_segment()
    {
        var canned = new Canned("""{"deleted":0}""");

        _ = await Client(canned).ForgetMemoryFacts(_api, new MemoryForget("a/b", FactId: "f"), Token);

        Assert.Equal("/sindri/memories/a%2Fb/facts", canned.Seen[0].Path);
    }

    private static SindriApiClient Client(HttpMessageHandler handler) => new(new HttpClient(handler), new InMemoryHumanToken("tok-1"), TimeProvider.System);

    private sealed class Canned(string body, HttpStatusCode code = HttpStatusCode.OK) : HttpMessageHandler
    {
        public List<(string Method, string Path, string Query, string Body)> Seen { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!; // why: HttpClient always sets the URI
            var sent = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Seen.Add((request.Method.Method, uri.AbsolutePath, uri.Query, sent));
            return new HttpResponseMessage(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
