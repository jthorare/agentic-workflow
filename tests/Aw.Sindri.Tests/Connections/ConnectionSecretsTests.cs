using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.OAuth;
using Aw.Sindri.Core.Secrets;
using Aw.Sindri.Features.TestConnection;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Mcp;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Connections;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Infrastructure.Secrets;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Connections;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Connections;

/// <summary>No token reaches an error text, a log line's input, a printed record or a stored row, on any failure path.</summary>
[Lane(TestLane.Integration)]
public sealed class ConnectionSecretsTests
{
    private const string Access = "access-SECRET-ABCDEF123456";
    private const string Refresh = "refresh-SECRET-654321FEDCBA";


    // The canary is the secret of every connection below. Each surface test plants it, drives add, test and a failing path,
    // then searches that surface's raw text. A provider that echoes the token back in an error body is part of the setup.
    private static readonly string _canary = "aw-canary-tok-" + RealTime.NewGuid().ToString("N");

    // The OAuth client secret has a canary of its own: the CLI posts it beside the key, and a leak of one must not hide behind the other.
    private static readonly string _secretCanary = "aw-canary-clientsecret-" + RealTime.NewGuid().ToString("N");

    private static void AssertNoCanary(string text)
    {
        Assert.DoesNotContain(_canary, text, StringComparison.Ordinal);
        Assert.DoesNotContain(_secretCanary, text, StringComparison.Ordinal);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A provider whose refusal body repeats the credential it was sent.</summary>
    private sealed class EchoingProbe : IConnectionProbe
    {
        public ValueTask<Result<Unit>> Probe(string url, TokenSet tokens, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(tokens);
            return ValueTask.FromResult<Result<Unit>>(SindriErrors.ConnectionNeedsSignIn.With($"401 from {url}: invalid token {tokens.AccessToken} (refresh {tokens.RefreshToken})"));
        }
    }

    /// <summary>An oauth add that succeeds with a client secret: it is stored with the tokens and must be in no row.</summary>
    private static async Task<string> DriveOauthSuccess(ConnectionHarness world)
    {
        var signed = new FakeSignInSession();
        var okAdd = world.Add(signed).Handle(ConnectionHarness.Oauth(name: "linear:ok") with { ClientSecret = _secretCanary }, Token).AsTask();
        signed.Succeed(new TokenSet("access-ok", "refresh-ok", ExpiresAt: null));
        return JsonSerializer.Serialize((await okAdd).Value, SindriJsonContext.Default.AddConnectionResponse);
    }

    /// <summary>The MCP adds: a key and a client secret named by the variables of serve's environment, one add that fails because the secret store is down, and a remove.</summary>
    private static async Task<List<ModelContextProtocol.Protocol.CallToolResult>> DriveMcpAdds(IServiceProvider services, ConnectionHarness world)
    {
        var results = new List<ModelContextProtocol.Protocol.CallToolResult>();
        world.Environment.Set("ACME_MCP_KEY", _canary);
        world.Environment.Set("GH_MCP_SECRET", _secretCanary);
        var addArguments = new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["name"] = JsonSerializer.SerializeToElement("acme:mcp"), ["kind"] = JsonSerializer.SerializeToElement("rest"), ["keyEnv"] = JsonSerializer.SerializeToElement("ACME_MCP_KEY") };
        results.Add(await ConnectionTools.CallAdd(services, addArguments, Token));
        var oauthArguments = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["name"] = JsonSerializer.SerializeToElement("linear:mcp"),
            ["preset"] = JsonSerializer.SerializeToElement("linear"),
            ["clientId"] = JsonSerializer.SerializeToElement("client-123"),
            ["clientSecretEnv"] = JsonSerializer.SerializeToElement("GH_MCP_SECRET"),
        };
        results.Add(await ConnectionTools.CallAdd(services, oauthArguments, Token));
        world.Secrets.Fails = true;
        addArguments["name"] = JsonSerializer.SerializeToElement("acme:mcpdown");
        results.Add(await ConnectionTools.CallAdd(services, addArguments, Token));
        world.Secrets.Fails = false;
        results.Add(await ConnectionTools.CallRemove(services, new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["name"] = JsonSerializer.SerializeToElement("acme:mcp") }, Token));
        return results;
    }

    /// <summary>The CLI path: the key and the client secret ride in the request, read from the CLI's own environment.</summary>
    private static async Task<string> DriveCliPath(ConnectionHarness world)
    {
        var request = ConnectionHarness.Rest("acme:cli") with { KeyEnv = null, Key = _canary };
        var sent = await world.Add().Handle(request, Token);
        return JsonSerializer.Serialize(sent.IsSuccess ? sent.Value : null, SindriJsonContext.Default.AddConnectionResponse)
            + (sent.IsError ? sent.Error.Message : string.Empty)
            + (request with { ClientSecret = _secretCanary });
    }

    /// <summary>Plants the canary as a rest key and an oauth token set, then runs add, a refused test and a failed sign-in.</summary>
    private static async Task<(ConnectionHarness World, List<string> Outputs)> DriveEveryPath()
    {
        var world = new ConnectionHarness();
        var outputs = new List<string>();
        world.Environment.Set("ACME_KEY", _canary);

        var added = await world.Add().Handle(ConnectionHarness.Rest(), Token);
        outputs.Add(JsonSerializer.Serialize(added.IsSuccess ? added.Value : null, SindriJsonContext.Default.AddConnectionResponse) + (added.IsError ? added.Error.Message : string.Empty));

        outputs.Add(await DriveCliPath(world));

        var oauthPointer = world.Secrets.Store(ConnectionSamples.Name(), new TokenSet(_canary, _canary + "-refresh", ExpiresAt: null)).Value;
        _ = await world.Store.Upsert(ConnectionSamples.Oauth() with { SecretPointer = oauthPointer }, Token);

        foreach (var echoes in new[] { false, true })
        {
            world.Probe.Refuses = true;
            var service = echoes ? world.TestWith(new EchoingProbe()) : world.Test();
            foreach (var name in new[] { "acme:key", "linear:work" })
            {
                var tested = await service.Handle(new TestConnectionRequest(name), Token);
                outputs.Add(tested.IsSuccess ? JsonSerializer.Serialize(tested.Value, SindriJsonContext.Default.TestConnectionResponse) : tested.Error.Message);
            }
        }

        await DriveFailedRefresh(world);

        var pending = world.Add().Handle(ConnectionHarness.Oauth(name: "linear:other") with { ClientSecret = _secretCanary }, Token).AsTask();
        world.SignIn.Fail(SindriErrors.ConnectionConsentDenied.With($"denied for {_canary} and {_secretCanary}"));
        var failed = await pending;
        outputs.Add(failed.IsError ? failed.Error.Message : string.Empty);

        outputs.Add(await DriveOauthSuccess(world));

        // an add that fails after the key was read: the secret store is down
        world.Secrets.Fails = true;
        var down = await world.Add().Handle(ConnectionHarness.Rest("acme:down") with { KeyEnv = null, Key = _canary }, Token);
        outputs.Add(down.IsError ? down.Error.Message : string.Empty);
        world.Secrets.Fails = false;
        return (world, outputs);
    }

    // a refresh the provider refuses, whose error text repeats the canary: the event it writes must hold the code only
    private static async Task DriveFailedRefresh(ConnectionHarness world)
    {
        world.OAuth.RefuseWith = "invalid_grant";
        var expiring = ConnectionSamples.Name("linear", "expiring");
        var expiringPointer = world.Secrets.Store(expiring, new TokenSet(_canary, _canary + "-refresh", world.Clock.GetUtcNow().AddMinutes(1))).Value;
        _ = await world.Store.Upsert(ConnectionSamples.Oauth(expiring) with { SecretPointer = expiringPointer }, Token);
        _ = await world.Refresh().Run(await world.Tick(), Token);
    }

    [Fact]
    public async Task Secret_never_reaches_the_ledger()
    {
        var (world, _) = await DriveEveryPath();

        var events = world.Ledger.Events;
        Assert.NotEmpty(events);   // a failed refresh wrote 'connection.needs-sign-in': the leak check below reads real events (10a passed with none)
        Assert.All(events, e => AssertNoCanary(JsonSerializer.Serialize(e.Envelope) + e.IdempotencyKey + e.Item));
        Assert.All(events, e => AssertNoCanary(e.Envelope.Payload));
    }

    [Fact]
    public async Task Secret_never_reaches_the_logs()
    {
        var (world, outputs) = await DriveEveryPath();

        Assert.All(world.Logs.Lines, AssertNoCanary);
        Assert.NotEmpty(outputs);   // the paths ran: a stub that throws fails above, never here
    }

    [Fact]
    public async Task Secret_never_reaches_the_mcp_results()
    {
        var (world, _) = await DriveEveryPath();
        await using var services = new ServiceCollection()
            .AddSingleton(AwJson.CreateOptions(SindriJsonContext.Default))
            .AddSingleton<Kernel.Services.IService<Features.GetConnection.GetConnectionRequest, Features.GetConnection.GetConnectionResponse>>(world.Get())
            .AddSingleton<Kernel.Services.IService<Features.ListConnections.ListConnectionsRequest, Features.ListConnections.ListConnectionsResponse>>(world.List())
            .AddSingleton<Kernel.Services.IService<TestConnectionRequest, TestConnectionResponse>>(world.TestWith(new EchoingProbe()))
            .AddSingleton<Kernel.Services.IService<Features.AddConnection.AddConnectionRequest, Features.AddConnection.AddConnectionResponse>>(world.Add())
            .AddSingleton<Kernel.Services.IService<Features.RemoveConnection.RemoveConnectionRequest, Features.RemoveConnection.RemoveConnectionResponse>>(world.Remove())
            .BuildServiceProvider();
        var results = new List<ModelContextProtocol.Protocol.CallToolResult>
        {
            await ConnectionTools.CallList(services, new Dictionary<string, JsonElement>(StringComparer.Ordinal), Token),
        };
        foreach (var name in new[] { "acme:key", "linear:work", "linear:nobody" })
        {
            var arguments = new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["name"] = JsonSerializer.SerializeToElement(name) };
            results.Add(await ConnectionTools.CallGet(services, arguments, Token));
            results.Add(await ConnectionTools.CallTest(services, arguments, Token));
        }

        results.AddRange(await DriveMcpAdds(services, world));

        Assert.All(results, result =>
        {
            var text = (result.StructuredContent?.GetRawText() ?? string.Empty) + string.Concat(result.Content.OfType<ModelContextProtocol.Protocol.TextContentBlock>().Select(static t => t.Text));
            AssertNoCanary(text);
        });
    }

    [Fact]
    public async Task Secret_never_reaches_the_service_answers_the_cli_and_the_http_route_print()
    {
        // the answers the CLI prints and the route serializes, one per path; the CLI's own stdout and stderr only print these fields
        var (_, outputs) = await DriveEveryPath();

        Assert.All(outputs, AssertNoCanary);
        Assert.Contains(outputs, static output => output.Contains("acme:key", StringComparison.Ordinal));   // the answers are not empty
    }

    [Fact]
    public async Task Secret_never_reaches_a_stored_row_in_memory()
    {
        var (world, _) = await DriveEveryPath();

        foreach (var row in world.Store.Rows)
        {
            var fields = string.Join('\n', [row.Name.ToString(), row.Preset, row.StatusReason, row.SecretPointer?.ToString(), row.CreatedBy, .. row.Config.Select(static p => p.Key + "=" + p.Value)]);
            AssertNoCanary(fields);
        }

        Assert.Contains(world.Store.Rows, static r => r.Name.ToString() == "acme:cli");   // the CLI-sent key was stored (in the secret store), so the scan above read a real row
    }

    [Fact]
    public async Task Secret_never_reaches_the_sqlite_row()
    {
        using var home = new TempHome();
        using var database = new LedgerDatabase(new SindriSettings(home.Path, Path.Combine(home.Path, "profile.yaml")), new FakeTimeProvider(KnownInstants.Epoch));
        var world = new ConnectionHarness();
        var store = new SqliteConnectionStore(database);
        var oauth = new Features.AddConnection.OAuthSignInFlow(store, world.Secrets, world.SignIn, world.Clock, world.Outcomes);
        var service = new Features.AddConnection.AddConnectionService(store, world.Secrets, world.Presets, world.Environment, world.Clock, world.Callers, oauth);

        var added = await service.Handle(ConnectionHarness.Rest("acme:sql") with { KeyEnv = null, Key = _canary }, Token);
        var signed = new FakeSignInSession();
        var oauthService = new Features.AddConnection.AddConnectionService(store, world.Secrets, world.Presets, world.Environment, world.Clock, world.Callers, new Features.AddConnection.OAuthSignInFlow(store, world.Secrets, signed, world.Clock, world.Outcomes));
        var oauthAdd = oauthService.Handle(ConnectionHarness.Oauth(name: "linear:sql") with { ClientSecret = _secretCanary }, Token).AsTask();
        signed.Succeed(new TokenSet("access-sql", "refresh-sql", ExpiresAt: null));
        var oauthAdded = await oauthAdd;

        Assert.True(added.IsSuccess, added.IsError ? added.Error.Message : null);
        Assert.True(oauthAdded.IsSuccess, oauthAdded.IsError ? oauthAdded.Error.Message : null);
        var file = Path.Combine(home.Path, "ledger.db");
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={file};Mode=ReadOnly;Pooling=False");
        await connection.OpenAsync(Token);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT group_concat(name || ' ' || kind || ' ' || ifnull(preset, '') || ' ' || config_json || ' ' || ifnull(secret_pointer, '') || ' ' || status || ' ' || ifnull(status_reason, '') || ' ' || created_by, char(10)) FROM connection";
        var text = Convert.ToString(await command.ExecuteScalarAsync(Token), System.Globalization.CultureInfo.InvariantCulture);
        command.CommandText = "SELECT group_concat(config_json, char(10)) FROM connection";
        var config = Convert.ToString(await command.ExecuteScalarAsync(Token), System.Globalization.CultureInfo.InvariantCulture);
        Assert.Contains("acme:sql", text, StringComparison.Ordinal);
        AssertNoCanary(text ?? string.Empty);
        AssertNoCanary(config ?? string.Empty);   // config_json alone: neither value lands there
    }

    private static void AssertClean(string text)
    {
        Assert.DoesNotContain(Access, text, StringComparison.Ordinal);
        Assert.DoesNotContain(Refresh, text, StringComparison.Ordinal);
        // no prefix or tail of either token; the bare word "SECRET" would also match the SND-SECRET-004 error code
        foreach (var part in new[] { "access-SECRET", "refresh-SECRET", "ABCDEF123456", "654321FEDCBA" })
        {
            Assert.DoesNotContain(part, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Secrets_are_scrubbed_from_logs_errors_and_the_ledger()
    {
        var tokens = new TokenSet(Access, Refresh, ExpiresAt: null);
        var connection = ConnectionSamples.Oauth();

        // printed records: the tokens hide, the connection row never held them
        AssertClean(tokens.ToString());
        AssertClean(connection.ToString());
        AssertClean(new CodeExchange(ConnectionHarness.Linear, "client-123", Access, "http://127.0.0.1:1/callback", Refresh).ToString());

        // the secret store failing, and a stored value that is not a token set
        var failing = new FakeSecretWriter { Fails = true };
        AssertClean(new KeychainConnectionSecrets(failing, failing, new FakeToolRunner(ToolRun.Missing), KeychainPlatform.Linux).Store(ConnectionSamples.Name(), tokens).Error.Message);
        var pointer = SecretPointer.Parse("keychain:sindri-connection/linear.work").Value;
        var garbled = new FakeSecretWriter { Stored = { [pointer.ToString()] = Access } };
        var read = new KeychainConnectionSecrets(garbled, garbled, new FakeToolRunner(ToolRun.Missing), KeychainPlatform.Linux).Read(pointer);
        Assert.True(read.IsError);
        AssertClean(read.Error.Message);
    }

    [Fact(Timeout = 60_000)]
    public async Task A_refused_refresh_and_a_refused_code_name_the_provider_error_code_never_a_token()
    {
        var ct = TestContext.Current.CancellationToken;
        using var server = new FakeOAuthServer { RevokeRefresh = true };
        using var http = new HttpClient();
        var client = new HttpOAuthClient(http, new FakeTimeProvider(KnownInstants.Epoch));

        var refreshed = await client.Refresh(server.Preset(), "client-123", Refresh, clientSecret: null, ct);
        var exchanged = await client.ExchangeCode(new CodeExchange(server.Preset(), "client-123", "code-not-issued", "http://127.0.0.1:1/callback", Access), ct);

        Assert.Equal(SindriErrors.ConnectionRefreshFailed.Code, refreshed.Error.Code);
        Assert.Contains("invalid_grant", refreshed.Error.Message, StringComparison.Ordinal);
        AssertClean(refreshed.Error.Message);
        Assert.True(exchanged.IsError);
        AssertClean(exchanged.Error.Message);
        Assert.DoesNotContain("code-not-issued", exchanged.Error.Message, StringComparison.Ordinal);
    }

    [Fact(Timeout = 60_000)]
    public async Task A_refresh_gets_a_new_access_token_and_keeps_or_rotates_the_refresh_token()
    {
        var ct = TestContext.Current.CancellationToken;
        using var server = new FakeOAuthServer();
        using var http = new HttpClient();
        var client = new HttpOAuthClient(http, new FakeTimeProvider(KnownInstants.Epoch));

        var kept = await client.Refresh(server.Preset(), "client-123", "refresh-old", clientSecret: null, ct);
        server.RotateRefresh = true;
        var rotated = await client.Refresh(server.Preset(), "client-123", "refresh-old", clientSecret: null, ct);

        Assert.Equal("refresh-old", kept.Value.RefreshToken);
        Assert.NotEqual("refresh-old", rotated.Value.RefreshToken, StringComparer.Ordinal);
        Assert.Equal(server.IssuedAccessTokens[0], kept.Value.AccessToken);
        Assert.Equal(KnownInstants.Epoch.AddSeconds(3600), kept.Value.ExpiresAt);
    }

    [Fact(Timeout = 60_000)]
    public async Task A_revoke_sends_the_token_to_the_providers_revoke_endpoint_and_a_preset_without_one_makes_no_call()
    {
        var ct = TestContext.Current.CancellationToken;
        using var server = new FakeOAuthServer();
        using var http = new HttpClient();
        var client = new HttpOAuthClient(http, new FakeTimeProvider(KnownInstants.Epoch));

        var revoked = await client.Revoke(server.Preset(), "client-123", "refresh-to-revoke", ct);
        var none = await client.Revoke(server.Preset(revoke: false), "client-123", "refresh-to-revoke", ct);

        Assert.True(revoked.IsSuccess);
        Assert.True(none.IsSuccess);
        Assert.Equal(["refresh-to-revoke"], server.RevokedTokens);
    }
}
