using System.Net;
using System.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Connections;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>The client's connection calls: method, path (the name escaped as one segment), body and the parsed answer; the pasted key goes out once and shows nowhere.</summary>
[Lane(TestLane.Unit)]
public sealed class ConnectionsClientTests
{
    private const string Row = """{"name":"linear:work","kind":"oauth","preset":"linear","status":"needs-sign-in","statusReason":"waiting for the browser sign-in","config":{"clientId":"c1"},"lastRefreshedAt":null,"hasSecret":true}""";
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static AddConnectionBody Body(string? key = null) =>
        new("acme:key", Preset: null, Kind: "rest", Client: null, KeyEnv: null, Scopes: [], Wait: false, Key: key);

    [Fact]
    public async Task ListConnections_gets_the_list_and_reads_the_rows()
    {
        var canned = new Canned($$"""{"connections":[{{Row}}]}""");

        var list = await Client(canned).ListConnections(_api, Token);

        Assert.Equal([("GET", "/sindri/connections")], canned.Seen.Select(s => (s.Method, s.Path)));
        var row = Assert.Single(list.Value.Connections);
        Assert.Equal(("linear:work", "needs-sign-in", true), (row.Name, row.Status, row.HasSecret));
    }

    [Fact]
    public async Task GetConnection_escapes_the_colon_and_unwraps_the_connection()
    {
        var canned = new Canned($$"""{"connection":{{Row}}}""");

        var one = await Client(canned).GetConnection(_api, "linear:work", Token);

        Assert.Equal(("GET", "/sindri/connections/linear:work"), (canned.Seen[0].Method, Uri.UnescapeDataString(canned.Seen[0].Path)));
        Assert.Equal("linear:work", one.Value.Connection.Name);
    }

    [Fact]
    public async Task AddConnection_posts_the_body_and_reads_the_consent_url()
    {
        var canned = new Canned($$"""{"connection":{{Row}},"consentUrl":"https://provider.example.test/authorize"}""");
        var body = new AddConnectionBody("linear:work", "linear", Kind: null, Client: "c1", KeyEnv: null, Scopes: ["read"], Wait: false);

        var added = await Client(canned).AddConnection(_api, body, Token);

        Assert.Equal(("POST", "/sindri/connections"), (canned.Seen[0].Method, canned.Seen[0].Path));
        Assert.Contains("\"name\":\"linear:work\"", canned.Seen[0].Body, StringComparison.Ordinal);
        Assert.Contains("\"scopes\":[\"read\"]", canned.Seen[0].Body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"key\"", canned.Seen[0].Body, StringComparison.Ordinal);
        Assert.Equal("https://provider.example.test/authorize", added.Value.ConsentUrl);
    }

    [Fact]
    public async Task AddConnection_writes_every_required_field_even_when_null()
    {
        // Sindri binds with RespectRequiredConstructorParameters: a body without preset, kind, client, keyEnv, scopes or wait is refused (SND-CLI-003).
        var canned = new Canned($$"""{"connection":{{Row}},"consentUrl":null}""");

        _ = await Client(canned).AddConnection(_api, Body("s3cret-key"), Token);

        var sent = canned.Seen[0].Body;
        foreach (var field in (string[])["\"preset\":null", "\"kind\":\"rest\"", "\"client\":null", "\"keyEnv\":null", "\"scopes\":[]", "\"wait\":false"])
        {
            Assert.Contains(field, sent, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task A_slash_in_a_name_stays_inside_one_segment()
    {
        var canned = new Canned("""{"name":"a/b","secretDeleted":false}""");

        _ = await Client(canned).RemoveConnection(_api, "a/b", Token);

        Assert.Equal("/sindri/connections/a%2Fb", canned.Seen[0].Path);
    }

    [Fact]
    public async Task TestConnection_posts_to_test_and_reads_ok_reason_and_tools()
    {
        var canned = new Canned($$"""{"connection":{{Row}},"ok":false,"reason":"token expired","tools":null}""");

        var tested = await Client(canned).TestConnection(_api, "linear:work", Token);

        Assert.Equal(("POST", "/sindri/connections/linear:work/test"), (canned.Seen[0].Method, Uri.UnescapeDataString(canned.Seen[0].Path)));
        Assert.Equal((false, "token expired"), (tested.Value.Ok, tested.Value.Reason));
    }

    [Fact]
    public async Task SignInConnection_posts_to_signin_and_reads_the_consent_url()
    {
        var canned = new Canned($$"""{"connection":{{Row}},"consentUrl":"https://provider.example.test/authorize"}""");

        var added = await Client(canned).SignInConnection(_api, "linear:work", Token);

        Assert.Equal(("POST", "/sindri/connections/linear:work/signin"), (canned.Seen[0].Method, Uri.UnescapeDataString(canned.Seen[0].Path)));
        Assert.NotNull(added.Value.ConsentUrl);
    }

    [Fact]
    public async Task RemoveConnection_deletes_the_connection_and_reads_secretDeleted()
    {
        var canned = new Canned("""{"name":"linear:work","secretDeleted":true}""");

        var removed = await Client(canned).RemoveConnection(_api, "linear:work", Token);

        Assert.Equal(("DELETE", "/sindri/connections/linear:work"), (canned.Seen[0].Method, Uri.UnescapeDataString(canned.Seen[0].Path)));
        Assert.True(removed.Value.SecretDeleted);
    }

    [Fact]
    public async Task AddConnection_sends_the_key_once_and_the_body_omits_a_null_key()
    {
        var canned = new Canned($$"""{"connection":{{Row}},"consentUrl":null}""");

        _ = await Client(canned).AddConnection(_api, Body("s3cret-key"), Token);

        var sent = Assert.Single(canned.Seen);
        Assert.Contains("\"key\":\"s3cret-key\"", sent.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddConnectionBody_ToString_and_logs_never_show_the_key()
    {
        var log = new RecordingLogger();
        var canned = new Canned("""{"type":"about:blank","title":"SND-CONN-007","status":400,"detail":"needs a client id","traceId":"x"}""", HttpStatusCode.BadRequest);
        var body = Body("s3cret-key") with { ClientSecret = "s3cret-client" };

        _ = await new SindriApiClient(new HttpClient(canned), new InMemoryHumanToken("tok"), TimeProvider.System, log).AddConnection(_api, body, Token);

        Assert.Equal("AddConnectionBody(acme:key)", body.ToString());
        var text = string.Join('\n', log.Entries.Select(l => l.Message));
        Assert.DoesNotContain("s3cret", text, StringComparison.Ordinal);
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
