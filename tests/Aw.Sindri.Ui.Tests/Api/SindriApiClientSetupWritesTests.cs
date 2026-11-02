using System.Net;
using System.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Setup;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>The client's setup writes (setup-once spec §5, §7.2): the route, the verb, the body, who the app says it is.</summary>
[Lane(TestLane.Unit)]
public sealed class SindriApiClientSetupWritesTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AddRepo_posts_the_path_to_the_repos_route_as_the_human_from_the_ui_and_never_as_a_terminal()
    {
        var canned = new Canned(HttpStatusCode.OK, GoldenFiles.SpecShaped("setup-repos", "started"));
        var client = new SindriApiClient(new HttpClient(canned), new InMemoryHumanToken("tok-1"), TimeProvider.System);

        var started = await client.AddRepo(_api, new AddRepoRequest("/work/demo"), Token);

        Assert.True(started.IsSuccess);
        Assert.Equal((HttpMethod.Post, "/sindri/setup/repos", "human", "ui", "tok-1", null), (canned.Method, canned.Path, canned.Caller, canned.Client, canned.Token, canned.Terminal));
        Assert.Contains("\"path\":\"/work/demo\"", canned.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegisterMcp_posts_to_the_mcp_route()
    {
        var canned = new Canned(HttpStatusCode.OK, GoldenFiles.SpecShaped("setup-mcp", "started"));
        var client = new SindriApiClient(new HttpClient(canned), new InMemoryHumanToken("tok-1"), TimeProvider.System);

        var started = await client.RegisterMcp(_api, new RegisterMcpRequest(), Token);

        Assert.True(started.IsSuccess);
        Assert.Equal((HttpMethod.Post, "/sindri/setup/mcp"), (canned.Method, canned.Path));
    }

    private sealed class Canned(HttpStatusCode code, string body, bool fail = false) : HttpMessageHandler
    {
        private HttpStatusCode _code = code;
        private string _answer = body;

        public HttpMethod? Method { get; private set; }

        public string? Path { get; private set; }

        public string? Caller { get; private set; }

        public string? Client { get; private set; }

        public string? Token { get; private set; }

        public string? Terminal { get; private set; }

        public string? Body { get; private set; }

        public int Calls { get; private set; }

        public void Respond(HttpStatusCode status, string text) => (_code, _answer) = (status, text);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string? Header(string name) => request.Headers.TryGetValues(name, out var values) ? values.Single() : null;
            Calls++;
            (Method, Path) = (request.Method, request.RequestUri!.AbsolutePath); // why: HttpClient always sets the URI
            (Caller, Client, Token, Terminal) = (Header("X-Sindri-Caller"), Header("X-Sindri-Client"), Header("X-Sindri-Token"), Header("X-Sindri-Terminal"));
            Body = request.Content is { } content ? await content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false) : null;
            if (fail)
            {
                throw new HttpRequestException("Connection refused (127.0.0.1:7410) with " + Body);
            }

            return new HttpResponseMessage(_code) { Content = new StringContent(_answer, Encoding.UTF8, "application/json") };
        }
    }
}
