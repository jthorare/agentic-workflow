using Aw.Kernel.Testing;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Features.AddConnection;
using Aw.Sindri.Testing.Connections;

namespace Aw.Sindri.Tests.Connections;

/// <summary>#4357: a preset whose first way to connect is a key (github, linear, slack) stores a key when the add names no kind; --kind oauth still signs in through the browser.</summary>
[Lane(TestLane.Unit)]
public sealed class KeyFirstAddTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ConnectionHarness KeyFirst() => new() { Presets = new FakeConnectionPresets(ConnectionHarness.Linear with { Auth = [ConnectionPreset.KeyAuth, ConnectionPreset.OAuthAuth] }) };

    private static AddConnectionRequest NoKind(string? keyEnv = "LINEAR_KEY", string? kind = null)
        => new("linear:work", Preset: "linear", Kind: kind, Client: kind is null ? null : "client-123", KeyEnv: keyEnv, Scopes: [], Wait: false);

    [Fact]
    public async Task An_add_with_no_kind_on_a_key_first_preset_stores_the_key_as_a_rest_connection_of_that_preset()
    {
        var world = KeyFirst();
        world.Environment.Set("LINEAR_KEY", "lin-api-test-0001");

        var added = await world.Add().Handle(NoKind(), Token);

        Assert.True(added.IsSuccess);
        var row = Assert.Single(world.Store.Rows);
        Assert.Equal((ConnectionKind.Rest, "linear", ConnectionStatus.Connected), (row.Kind, row.Preset, row.Status));
        Assert.Equal("lin-api-test-0001", world.Secrets.Tokens(row.SecretPointer!.ToString())!.AccessToken);
        Assert.Empty(world.SignIn.Clients);
    }

    [Fact]
    public async Task An_add_with_no_kind_and_no_key_on_a_key_first_preset_says_to_pass_a_key_or_kind_oauth_and_starts_no_sign_in()
    {
        var world = KeyFirst();

        var added = await world.Add().Handle(NoKind(keyEnv: null), Token);

        Assert.True(added.IsError);
        Assert.Contains("--key-env", added.Error.Message, StringComparison.Ordinal);
        Assert.Contains("--kind oauth", added.Error.Message, StringComparison.Ordinal);
        Assert.Empty(world.SignIn.Clients);
        Assert.Empty(world.Store.Rows);
    }

    [Fact]
    public async Task Kind_oauth_on_a_key_first_preset_still_runs_the_browser_sign_in()
    {
        var world = KeyFirst();

        var added = await world.Add().Handle(NoKind(keyEnv: null, kind: "oauth"), Token);

        Assert.True(added.IsSuccess);
        Assert.Equal(world.SignIn.ConsentUrl.ToString(), added.Value.ConsentUrl);
        Assert.Equal("client-123", Assert.Single(world.SignIn.Clients));
    }

    [Fact]
    public async Task A_key_for_a_preset_is_checked_with_its_service_before_it_is_stored()
    {
        var world = KeyFirst();
        world.Environment.Set("LINEAR_KEY", "lin-api-test-0001");

        _ = await world.Add().Handle(NoKind(), Token);

        Assert.Equal(("linear", "lin-api-test-0001"), Assert.Single(world.Keys.Calls));
    }

    [Theory]
    [InlineData(true, "SND-CONN-018")]
    [InlineData(false, "SND-CONN-019")]
    public async Task A_refused_or_unchecked_key_stores_no_secret_and_no_row(bool refuses, string code)
    {
        var world = KeyFirst();
        world.Environment.Set("LINEAR_KEY", "lin-api-test-0001");
        (world.Keys.Refuses, world.Keys.Unreachable) = (refuses, !refuses);

        var added = await world.Add().Handle(NoKind(), Token);

        Assert.Equal(code, added.Error.Code);
        Assert.DoesNotContain("lin-api-test-0001", added.Error.Message, StringComparison.Ordinal);
        Assert.Empty(world.Store.Rows);
        Assert.Empty(world.Secrets.Pointers);
    }

    [Fact]
    public async Task A_key_with_no_preset_is_stored_unchecked()
    {
        var world = new ConnectionHarness();
        world.Environment.Set("ACME_KEY", "sk-live-ACME-0001");

        var added = await world.Add().Handle(ConnectionHarness.Rest(), Token);

        Assert.True(added.IsSuccess);
        Assert.Empty(world.Keys.Calls);
    }
}
