using Aw.Kernel.Testing;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Infrastructure.Connections;
using Aw.Sindri.Testing.Connections;

namespace Aw.Sindri.Tests.Connections;

/// <summary>The 'connection-refresh' service over a real HTTP client against a loopback fake OAuth server: integration, so it is its own class (a test carries exactly one Lane trait, #2534).</summary>
[Lane(TestLane.Integration)]
public sealed class ConnectionRefreshHttpTests
{
    private const string OldAccess = "access-OLD-0001";
    private const string OldRefresh = "refresh-OLD-0001";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_github_style_preset_refreshes_with_the_client_secret_and_keeps_it()
    {
        using var server = new FakeOAuthServer();
        using var http = new HttpClient();
        var world = new ConnectionHarness { Presets = new FakeConnectionPresets(server.Preset("linear")) };
        var name = ConnectionSamples.Name();
        var pointer = world.Secrets.Store(name, new TokenSet(OldAccess, OldRefresh, world.Clock.GetUtcNow().AddMinutes(3), ClientSecret: "app-value-0001")).Value;
        _ = await world.Store.Upsert(ConnectionSamples.Oauth(name) with { SecretPointer = pointer }, Token);

        var ran = await world.Refresh(new HttpOAuthClient(http, world.Clock)).Run(await world.Tick(), Token);

        Assert.True(ran.IsSuccess);
        var token = Assert.Single(server.Requests, static r => r.Path == "/token");
        Assert.Equal("app-value-0001", token.Form["client_secret"]);   // the provider got the secret in the refresh form
        var stored = world.Secrets.Tokens(pointer.ToString())!;
        Assert.Equal(server.IssuedAccessTokens[0], stored.AccessToken);
        Assert.Equal("app-value-0001", stored.ClientSecret);   // and the replaced secret still carries it, so the next refresh sends it again
        Assert.Equal(OldRefresh, stored.RefreshToken);
    }
}
