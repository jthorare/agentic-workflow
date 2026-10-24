using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>The client's profile approval (needs-you slice 3): a POST of the full hash and nothing else, made as the human, and the server's own answers read back.</summary>
[Lane(TestLane.Unit)]
public sealed class SindriApiClientProfileApproveTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private const string Hash = "70e1581b3ecaf617318482f6b0844b67f79a287a7136b4e4b6ff5d26005e4261";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ApproveProfile_posts_the_hash_alone_to_the_profile_route_as_the_human()
    {
        var canned = new Canned(HttpStatusCode.OK, GoldenFiles.Api("profile-approve", "approved"));
        var client = new SindriApiClient(new HttpClient(canned), new InMemoryHumanToken("tok-1"), TimeProvider.System);

        var done = await client.ApproveProfile(_api, new ApproveProfileRequest(Hash), Token);

        Assert.True(done.IsSuccess);
        Assert.Equal((true, 3L), (done.Value.Approved, done.Value.Sequence));
        Assert.Equal((HttpMethod.Post, "/sindri/profile/approve", "human", "ui"), (canned.Method, canned.Path, canned.Caller, canned.Client));
        var body = JsonNode.Parse(canned.Body!)!;
        Assert.Equal(Hash, body["hash"]!.GetValue<string>());
        Assert.Equal(["hash"], body.AsObject().Select(p => p.Key));   // no challenge, typed or heldMs
    }

    [Fact]
    public async Task A_session_refusal_comes_back_as_the_servers_code()
    {
        var canned = new Canned(HttpStatusCode.Forbidden, GoldenFiles.Api("profile-approve", "refused-session"));
        var client = new SindriApiClient(new HttpClient(canned), new InMemoryHumanToken("tok-1"), TimeProvider.System);

        var done = await client.ApproveProfile(_api, new ApproveProfileRequest(Hash), Token);

        Assert.True(done.IsError);
        Assert.Equal("SND-AUTH-002", done.Error.Code);
    }

    private sealed class Canned(HttpStatusCode code, string body) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }

        public string? Path { get; private set; }

        public string? Caller { get; private set; }

        public string? Client { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string? Header(string name) => request.Headers.TryGetValues(name, out var values) ? values.Single() : null;
            (Method, Path) = (request.Method, request.RequestUri!.AbsolutePath); // why: HttpClient always sets the URI
            (Caller, Client) = (Header("X-Sindri-Caller"), Header("X-Sindri-Client"));
            Body = request.Content is { } content ? await content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false) : null;
            return new HttpResponseMessage(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
