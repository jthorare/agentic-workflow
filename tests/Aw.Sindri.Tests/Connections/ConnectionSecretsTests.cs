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
using Aw.Sindri.Infrastructure.Connections;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Infrastructure.Secrets;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Connections;
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

    /// <summary>Plants the canary as a rest key and an oauth token set, then runs add, a refused test and a failed sign-in.</summary>
    private static async Task<(ConnectionHarness World, List<string> Outputs)> DriveEveryPath()
    {
        var world = new ConnectionHarness();
        var outputs = new List<string>();
        world.Environment.Set("ACME_KEY", _canary);

        var added = await world.Add().Handle(ConnectionHarness.Rest(), Token);
        outputs.Add(JsonSerializer.Serialize(added.IsSuccess ? added.Value : null, SindriJsonContext.Default.AddConnectionResponse) + (added.IsError ? added.Error.Message : string.Empty));

        var oauthPointer = world.Secrets.Store(ConnectionSamples.Name(), new TokenSet(_canary, _canary + "-refresh", ExpiresAt: null)).Value;
        _ = await world.Store.Upsert(ConnectionSamples.Oauth() with { SecretPointer = oauthPointer }, Token);

        foreach (var echoes in new[] { false, true })
        {
            world.Probe.Refuses = true;
            var service = echoes ? new TestConnectionService(world.Store, world.Secrets, world.Presets, new EchoingProbe(), world.Clock) : world.Test();
            foreach (var name in new[] { "acme:key", "linear:work" })
            {
                var tested = await service.Handle(new TestConnectionRequest(name), Token);
                outputs.Add(tested.IsSuccess ? JsonSerializer.Serialize(tested.Value, SindriJsonContext.Default.TestConnectionResponse) : tested.Error.Message);
            }
        }

        var pending = world.Add().Handle(ConnectionHarness.Oauth(name: "linear:other"), Token).AsTask();
        world.SignIn.Fail(SindriErrors.ConnectionConsentDenied.With($"denied for {_canary}"));
        var failed = await pending;
        outputs.Add(failed.IsError ? failed.Error.Message : string.Empty);
        return (world, outputs);
    }

    [Fact]
    public async Task Secret_never_reaches_the_ledger()
    {
        var (world, _) = await DriveEveryPath();

        var events = world.Ledger.Events;
        Assert.All(events, e => Assert.DoesNotContain(_canary, JsonSerializer.Serialize(e.Envelope) + e.IdempotencyKey + e.Item, StringComparison.Ordinal));
        Assert.All(events, e => Assert.DoesNotContain(_canary, e.Envelope.Payload, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Secret_never_reaches_the_logs()
    {
        var (world, outputs) = await DriveEveryPath();

        Assert.All(world.Logs.Lines, line => Assert.DoesNotContain(_canary, line, StringComparison.Ordinal));
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
            .AddSingleton<Kernel.Services.IService<TestConnectionRequest, TestConnectionResponse>>(new TestConnectionService(world.Store, world.Secrets, world.Presets, new EchoingProbe(), world.Clock))
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

        Assert.All(results, result =>
        {
            var text = (result.StructuredContent?.GetRawText() ?? string.Empty) + string.Concat(result.Content.OfType<ModelContextProtocol.Protocol.TextContentBlock>().Select(static t => t.Text));
            Assert.DoesNotContain(_canary, text, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Secret_never_reaches_the_test_output()
    {
        var (_, outputs) = await DriveEveryPath();   // success and refused answers, as the CLI prints them and the HTTP route serializes them

        Assert.All(outputs, output => Assert.DoesNotContain(_canary, output, StringComparison.Ordinal));
        Assert.Contains(outputs, static output => output.Contains("acme:key", StringComparison.Ordinal));   // the answers are not empty
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

        var refreshed = await client.Refresh(server.Preset(), "client-123", Refresh, ct);
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

        var kept = await client.Refresh(server.Preset(), "client-123", "refresh-old", ct);
        server.RotateRefresh = true;
        var rotated = await client.Refresh(server.Preset(), "client-123", "refresh-old", ct);

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
