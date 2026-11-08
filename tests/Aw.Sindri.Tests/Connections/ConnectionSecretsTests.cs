using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections.OAuth;
using Aw.Sindri.Core.Secrets;
using Aw.Sindri.Infrastructure.Connections;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Infrastructure.Secrets;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Connections;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Connections;

/// <summary>No token reaches an error text, a log line's input, a printed record or a stored row, on any failure path.</summary>
[Lane(TestLane.Integration)]
public sealed class ConnectionSecretsTests
{
    private const string Access = "access-SECRET-ABCDEF123456";
    private const string Refresh = "refresh-SECRET-654321FEDCBA";


    private static void AssertClean(string text)
    {
        Assert.DoesNotContain(Access, text, StringComparison.Ordinal);
        Assert.DoesNotContain(Refresh, text, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET", text, StringComparison.Ordinal);
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
