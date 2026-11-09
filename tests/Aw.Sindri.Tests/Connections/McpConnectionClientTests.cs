using System.Security.Cryptography;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Features.AddConnection;
using Aw.Sindri.Features.TestConnection;
using Aw.Sindri.Infrastructure.Connections;
using Aw.Sindri.Testing.Connections;

namespace Aw.Sindri.Tests.Connections;

/// <summary>The 'mcp' connection kind (connections PR 10b) against an MCP server on 127.0.0.1: a key, a sign-in with a registered client, listing the tools.</summary>
[Lane(TestLane.Integration)]
public sealed class McpConnectionClientTests
{
    private const string Key = "mcp-key-SECRET-0001";
    private static readonly string[] _tools = ["create_issue", "list_issues", "search"];

    [Fact(Timeout = 60_000)]
    public async Task A_key_connects_and_lists_the_servers_tools_sorted()
    {
        using var server = new FakeMcpServer(Key, ["search", "create_issue", "list_issues"]);
        using var http = new HttpClient();

        var tools = await new McpConnectionClient(http).ListTools(server.Endpoint, new TokenSet(Key, RefreshToken: null, ExpiresAt: null), TestContext.Current.CancellationToken);

        Assert.True(tools.IsSuccess, tools.IsError ? tools.Error.Message : null);
        Assert.Equal(_tools, tools.Value);
        Assert.Equal(["initialize", "notifications/initialized", "tools/list"], server.Methods);
    }

    [Fact(Timeout = 60_000)]
    public async Task A_refused_key_is_SND_CONN_002_and_the_text_holds_no_key()
    {
        using var server = new FakeMcpServer(Key, _tools);
        using var http = new HttpClient();

        var tools = await new McpConnectionClient(http).ListTools(server.Endpoint, new TokenSet("wrong-key-SECRET-9999", RefreshToken: null, ExpiresAt: null), TestContext.Current.CancellationToken);

        Assert.True(tools.IsError);
        Assert.Equal(SindriErrors.ConnectionNeedsSignIn.Code, tools.Error.Code);
        Assert.DoesNotContain("SECRET", tools.Error.Message, StringComparison.Ordinal);
    }

    [Fact(Timeout = 60_000)]
    public async Task A_server_that_cannot_be_reached_is_SND_CONN_005()
    {
        Uri gone;
        using (var server = new FakeMcpServer(Key, _tools))
        {
            gone = server.Endpoint;
        }

        using var http = new HttpClient();
        var tools = await new McpConnectionClient(http).ListTools(gone, new TokenSet(Key, RefreshToken: null, ExpiresAt: null), TestContext.Current.CancellationToken);

        Assert.Equal(SindriErrors.ConnectionRefreshFailed.Code, tools.Error.Code);
    }

    [Fact(Timeout = 60_000)]
    public async Task An_mcp_add_with_a_key_stores_it_and_the_test_lists_the_tools()
    {
        using var server = new FakeMcpServer(Key, _tools);
        using var http = new HttpClient();
        var world = new ConnectionHarness();
        world.Environment.Set("MCP_KEY", Key);
        var request = new AddConnectionRequest("acme:tools", Preset: null, Kind: "mcp", Client: null, KeyEnv: "MCP_KEY", Scopes: [], Wait: true, Url: server.Endpoint.ToString());

        var added = await world.Add().Handle(request, TestContext.Current.CancellationToken);
        var tested = await world.Test(new McpConnectionClient(http)).Handle(new TestConnectionRequest("acme:tools"), TestContext.Current.CancellationToken);

        Assert.True(added.IsSuccess, added.IsError ? added.Error.Message : null);
        var row = Assert.Single(world.Store.Rows);
        Assert.Equal(ConnectionKind.Mcp, row.Kind);
        Assert.Equal(server.Endpoint.ToString(), row.Config["serverUrl"]);
        Assert.Equal(Key, world.Secrets.Tokens(row.SecretPointer!.ToString())!.AccessToken);
        Assert.True(tested.Value.Ok);
        Assert.Equal(_tools, tested.Value.Tools);
        Assert.DoesNotContain(Key, System.Text.Json.JsonSerializer.Serialize(added.Value.Connection), StringComparison.Ordinal);
    }

    [Fact(Timeout = 60_000)]
    public async Task An_mcp_test_with_a_refused_key_is_Ok_false_naming_the_code_and_marks_the_connection()
    {
        using var server = new FakeMcpServer("another-key-SECRET-7777", _tools);
        using var http = new HttpClient();
        var world = new ConnectionHarness();
        world.Environment.Set("MCP_KEY", Key);
        _ = await world.Add().Handle(new AddConnectionRequest("acme:tools", Preset: null, Kind: "mcp", Client: null, KeyEnv: "MCP_KEY", Scopes: [], Wait: true, Url: server.Endpoint.ToString()), TestContext.Current.CancellationToken);

        var tested = await world.Test(new McpConnectionClient(http)).Handle(new TestConnectionRequest("acme:tools"), TestContext.Current.CancellationToken);

        Assert.True(tested.IsSuccess);
        Assert.False(tested.Value.Ok);
        Assert.Contains("SND-CONN-", tested.Value.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET", tested.Value.Reason, StringComparison.Ordinal);
        Assert.NotEqual(ConnectionStatus.Connected, Assert.Single(world.Store.Rows).Status);
    }

    [Fact(Timeout = 60_000)]
    public async Task The_servers_own_sign_in_registers_a_client_and_ends_with_tokens()
    {
        using var oauth = new FakeOAuthServer();
        using var server = new FakeMcpServer(Key, _tools, oauth.Url);
        using var http = new HttpClient();
        using var browser = new FakeBrowser();
        var world = new ConnectionHarness();
        var signIn = new LoopbackSignIn(new HttpOAuthClient(http, world.Clock), browser, TimeSpan.FromSeconds(30), RandomNumberGenerator.GetBytes);

        var started = await signIn.StartMcp(server.Endpoint, TestContext.Current.CancellationToken);
        var finished = await started.Value.Completion;

        Assert.True(finished.IsSuccess, finished.IsError ? finished.Error.Message : null);
        _ = Assert.Single(server.Registered);   // RFC 7591: the client was registered on the spot
        Assert.Equal(oauth.IssuedAccessTokens[0], finished.Value.AccessToken);
        Assert.Contains("dcr-client-1", Assert.Single(browser.Opened).Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_mcp_add_without_a_key_starts_the_servers_own_sign_in_and_stores_the_row_when_it_finishes()
    {
        var world = new ConnectionHarness();
        var pending = world.Add().Handle(new AddConnectionRequest("acme:tools", Preset: null, Kind: "mcp", Client: null, KeyEnv: null, Scopes: [], Wait: true, Url: "http://127.0.0.1:1/mcp"), TestContext.Current.CancellationToken).AsTask();
        world.SignIn.Succeed(new TokenSet("access-1", "refresh-1", ExpiresAt: null));

        var added = await pending;

        Assert.True(added.IsSuccess, added.IsError ? added.Error.Message : null);
        Assert.Equal("http://127.0.0.1:1/mcp", Assert.Single(world.SignIn.McpServers).ToString());
        Assert.Equal("connected", added.Value.Connection.Status);
        Assert.Equal("access-1", world.Secrets.Tokens(Assert.Single(world.Store.Rows).SecretPointer!.ToString())!.AccessToken);
    }

    [Fact]
    public async Task An_mcp_add_without_a_server_url_is_an_error_that_names_it()
    {
        var world = new ConnectionHarness();

        var added = await world.Add().Handle(new AddConnectionRequest("acme:tools", Preset: null, Kind: "mcp", Client: null, KeyEnv: "MCP_KEY", Scopes: [], Wait: true), TestContext.Current.CancellationToken);

        Assert.True(added.IsError);
        Assert.Contains("url", added.Error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(world.Store.Rows);
    }
}
