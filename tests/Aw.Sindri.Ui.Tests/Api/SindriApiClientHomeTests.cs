using System.Net;
using System.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>The client's Home read (home spec §8): the route, the escaped cursor and what the golden carries.</summary>
[Lane(TestLane.Unit)]
public sealed class SindriApiClientHomeTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SindriApiClient Client(HttpMessageHandler handler) => new(new HttpClient(handler), new InMemoryHumanToken("tok-1"), TimeProvider.System);

    [Fact]
    public async Task Home_reads_the_first_page_with_no_query()
    {
        var canned = new Canned(HttpStatusCode.OK, GoldenFiles.Api("home", "fixture"));

        var page = await Client(canned).Home(_api, after: null, Token);

        Assert.Equal(("/sindri/home", string.Empty), (canned.Seen[0].AbsolutePath, canned.Seen[0].Query));
        Assert.Equal((6, null), (page.Value.NeedsYou.Count, page.Value.Cursor));
    }

    [Fact]
    public async Task Home_sends_the_cursor_escaped_as_after()
    {
        var canned = new Canned(HttpStatusCode.OK, GoldenFiles.Api("home", "after-cursor"));

        var page = await Client(canned).Home(_api, "gh:ready:jthorare/agentic-workflow#3099", Token);

        Assert.Equal("?after=gh%3Aready%3Ajthorare%2Fagentic-workflow%233099", canned.Seen[0].Query);
        Assert.Equal("gh:ready:jthorare/agentic-workflow#3101", page.Value.Cursor);
    }

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
