using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Redaction;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Infrastructure.Connections;
using Aw.Sindri.Infrastructure.Text;
using Aw.Sindri.Testing.Connections;

namespace Aw.Sindri.Tests.Connections;

/// <summary>The call_tool block's connection port (agent-first PR 16b): an 'mcp' connection's tools through the MCP client and a preset's rest actions through a signed request.</summary>
[Lane(TestLane.Integration)]
public sealed class ConnectionToolAdapterTests
{
    private const string Key = "mcp-key-SECRET-0001";
    private const string RestCredential = "rest-cred-0042";

    [Fact(Timeout = 60_000)]
    public async Task An_mcp_connections_tools_are_listed_and_a_call_reaches_the_server_with_its_arguments_and_no_key_in_the_result()
    {
        using var server = new FakeMcpServer(Key, ["search", "create_issue"]);
        using var http = new HttpClient();
        var world = new World(http);
        world.AddMcp("acme:tools", server.Endpoint, Key);

        var listed = await world.Adapter.ListTools("acme:tools", TestContext.Current.CancellationToken);
        var called = await world.Adapter.Call("acme:tools", "search", """{"q":"bug"}""", TestContext.Current.CancellationToken);

        Assert.Equal([("create_issue", "mcp"), ("search", "mcp")], listed.Value.Select(static t => (t.Name, t.Source)));
        Assert.Equal([("search", """{"q":"bug"}""")], server.Calls);
        Assert.Contains("\"tool\":\"search\"", called.Value, StringComparison.Ordinal);
        Assert.DoesNotContain(Key, called.Value, StringComparison.Ordinal);
    }

    [Fact(Timeout = 60_000)]
    public async Task A_rest_presets_actions_are_listed_and_a_call_sends_a_signed_request_to_the_providers_host_only()
    {
        var provider = new Recording($$"""{"number":7,"echo":"{{RestCredential}}"}""");
        using var http = new HttpClient(provider, disposeHandler: false);
        var world = new World(http);
        world.AddRest("github:work", "github", RestCredential);

        var listed = await world.Adapter.ListTools("github:work", TestContext.Current.CancellationToken);
        var called = await world.Adapter.Call("github:work", "create_issue", """{"owner":"acme","repo":"app","title":"Crash on start"}""", TestContext.Current.CancellationToken);

        Assert.Contains(("create_issue", "rest"), listed.Value.Select(static t => (t.Name, t.Source)));
        var sent = Assert.Single(provider.Requests);
        Assert.Equal(HttpMethod.Post, sent.Method);
        Assert.Equal("https://api.github.com/repos/acme/app/issues", sent.Uri);
        Assert.Equal("Bearer " + RestCredential, sent.Authorization);
        Assert.Equal("""{"title":"Crash on start"}""", sent.Body);
        Assert.Equal(7, JsonNode.Parse(called.Value)!["number"]!.GetValue<int>());
        Assert.DoesNotContain(RestCredential, called.Value, StringComparison.Ordinal);
    }

    [Fact(Timeout = 60_000)]
    public async Task The_github_list_runs_action_reads_a_branchs_latest_runs_with_a_get_and_the_rest_as_the_query()
    {
        var provider = new Recording("""{"total_count":0,"workflow_runs":[]}""");
        using var http = new HttpClient(provider, disposeHandler: false);
        var world = new World(http);
        world.AddRest("github:work", "github", RestCredential);

        var called = await world.Adapter.Call("github:work", "list_runs", """{"owner":"o","repo":"r","branch":"main","per_page":"5"}""", TestContext.Current.CancellationToken);

        Assert.True(called.IsSuccess);
        var sent = Assert.Single(provider.Requests);
        Assert.Equal(HttpMethod.Get, sent.Method);
        Assert.Equal("https://api.github.com/repos/o/r/actions/runs?branch=main&per_page=5", sent.Uri);
    }

    [Fact(Timeout = 60_000)]
    public async Task A_path_argument_with_a_quote_is_url_escaped_as_the_value_it_is_not_as_json_text()
    {
        var provider = new Recording("{}");
        using var http = new HttpClient(provider, disposeHandler: false);
        var world = new World(http);
        world.AddRest("github:work", "github", RestCredential);

        var called = await world.Adapter.Call("github:work", "create_issue", """{"owner":"a\"b","repo":"app","title":"t"}""", TestContext.Current.CancellationToken);

        Assert.True(called.IsSuccess);
        var sent = Assert.Single(provider.Requests);
        Assert.DoesNotContain("u0022", sent.Uri, StringComparison.Ordinal);   // the JSON escape of the quote, kept by Trim('"') today
        Assert.DoesNotContain("%5C", sent.Uri, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(Timeout = 60_000)]
    public async Task A_missing_connection_is_SND_CONN_001_and_an_unknown_action_or_a_missing_path_argument_sends_nothing()
    {
        var provider = new Recording("{}");
        using var http = new HttpClient(provider, disposeHandler: false);
        var world = new World(http);
        world.AddRest("github:work", "github", RestCredential);

        var missing = await world.Adapter.ListTools("nope:x", TestContext.Current.CancellationToken);
        var unknown = await world.Adapter.Call("github:work", "delete_everything", "{}", TestContext.Current.CancellationToken);
        var partial = await world.Adapter.Call("github:work", "create_issue", """{"owner":"acme"}""", TestContext.Current.CancellationToken);

        Assert.Equal(SindriErrors.ConnectionNotFound.Code, missing.Error.Code);
        Assert.True(unknown.IsError);
        Assert.True(partial.IsError);
        Assert.Empty(provider.Requests);
    }

    [Fact(Timeout = 60_000)]
    public async Task Post_message_refuses_the_messages_connection()
    {
        var provider = new Recording("{}");
        using var http = new HttpClient(provider, disposeHandler: false);
        var world = new World(http);
        world.AddRest("slack:messages", preset: null, RestCredential);

        var called = await world.Adapter.Call("slack:messages", "post_message", """{"channel":"C123","text":"hi"}""", TestContext.Current.CancellationToken);

        Assert.True(called.IsError);
        Assert.Empty(provider.Requests);
    }

    private sealed record Sent(HttpMethod Method, string Uri, string? Authorization, string? Body);

    private sealed class Recording(string answer) : HttpMessageHandler
    {
        public List<Sent> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new Sent(request.Method, request.RequestUri!.ToString(), request.Headers.Authorization?.ToString(), body));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(answer, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class World
    {
        private readonly InMemoryConnectionStore _store = new();
        private readonly FakeConnectionSecrets _secrets = new();

        public World(HttpClient http)
        {
            Adapter = new ConnectionToolAdapter(_store, _secrets, new McpConnectionClient(http), http, new RedactingScrubber(Redactor.Default));
        }

        public ConnectionToolAdapter Adapter { get; }

        public void AddMcp(string name, Uri server, string key) => Add(name, ConnectionKind.Mcp, preset: null, new Dictionary<string, string>(StringComparer.Ordinal) { ["serverUrl"] = server.ToString() }, key);

        public void AddRest(string name, string? preset, string token) => Add(name, ConnectionKind.OAuth, preset, new Dictionary<string, string>(StringComparer.Ordinal), token);

        private void Add(string name, ConnectionKind kind, string? preset, Dictionary<string, string> config, string token)
        {
            var parsed = ConnectionName.Parse(name).Value;
            var pointer = _secrets.Store(parsed, new TokenSet(token, RefreshToken: null, ExpiresAt: null)).Value;
            _ = _store.Upsert(ConnectionSamples.Oauth(parsed) with { Kind = kind, Preset = preset, Config = config, SecretPointer = pointer }, CancellationToken.None).AsTask().Result;
        }
    }
}
