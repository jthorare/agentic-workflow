using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Features.AddConnection;
using Aw.Sindri.Testing.Connections;

namespace Aw.Sindri.Tests.Connections;

/// <summary>
/// #4477: the key check can take 15 s, so a poller or a rename that writes the row meanwhile must not be lost when the replace writes its own fields; and a keyed mcp connection (added with --key-env)
/// takes a new key like a rest one, unchecked, while an oauth mcp connection keeps the sign-in refusal.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ReplaceConnectionKeyRaceAndMcpTests
{
    private const string Old = "lin-api-old-0001";
    private const string Fresh = "lin-api-new-0002";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A key check that runs a write against the store while it "waits for the service", as a poller finishing during the 15 s check would.</summary>
    private sealed class DuringCheck(Func<Task> write) : IKeyCheck
    {
        public async ValueTask<Result<KeyCheckOutcome>> Check(ConnectionPreset preset, string key, CancellationToken cancellationToken)
        {
            await write().ConfigureAwait(false);
            return KeyCheckOutcome.Checked;
        }
    }

    private static async Task<(ConnectionHarness World, Connection Row)> Holding(ConnectionKind kind = ConnectionKind.Rest, TokenSet? tokens = null, string? preset = "linear", ConnectionStatus status = ConnectionStatus.Connected, string? auth = null)
    {
        var world = new ConnectionHarness();
        await world.Hold();
        var config = new Dictionary<string, string>(StringComparer.Ordinal);
        if (kind == ConnectionKind.Mcp)
        {
            config["serverUrl"] = "https://mcp.example.test/";
        }

        if (auth is not null)
        {
            config["auth"] = auth;
        }

        var row = ConnectionSamples.Oauth(ConnectionSamples.Name("linear", "work"), status) with { Kind = kind, Preset = preset, Config = config };
        var pointer = world.Secrets.Store(row.Name, tokens ?? new TokenSet(Old, RefreshToken: null, ExpiresAt: null)).Value;
        row = row with { SecretPointer = pointer };
        _ = await world.Store.Upsert(row, Token);
        return (world, row);
    }

    private static ReplaceConnectionKeyService During(ConnectionHarness world, Func<Task> write) => new(world.Store, world.Secrets, world.Presets, world.Environment, world.Events(), new DuringCheck(write));

    private static string StoredKey(ConnectionHarness world) => world.Secrets.Read(Assert.Single(world.Store.Rows).SecretPointer!).Value.AccessToken;

    [Fact]
    public async Task A_write_made_to_the_row_during_the_check_is_kept_and_only_the_replace_fields_change()
    {
        var (world, row) = await Holding(status: ConnectionStatus.Error);
        var polled = row with { Config = new Dictionary<string, string>(StringComparer.Ordinal) { ["pollCursor"] = "after-check" }, LastRefreshedAt = KnownInstants.Epoch.AddMinutes(5) };
        var service = During(world, async () => _ = await world.Store.Upsert(polled, Token));
        world.Clock.Advance(TimeSpan.FromHours(1));

        var replaced = await service.Handle(new ReplaceConnectionKeyRequest("linear:work", KeyEnv: null, Key: Fresh), Token);

        Assert.True(replaced.IsSuccess);
        var stored = Assert.Single(world.Store.Rows);
        Assert.Equal("after-check", stored.Config["pollCursor"]);
        Assert.Equal(polled.LastRefreshedAt, stored.LastRefreshedAt);
        Assert.Equal((ConnectionStatus.Connected, null, world.Clock.GetUtcNow()), (stored.Status, stored.StatusReason, stored.UpdatedAt));
        Assert.Equal(Fresh, StoredKey(world));
        Assert.Equal("after-check", replaced.Value.Connection.Config["pollCursor"]);
    }

    [Fact]
    public async Task A_row_removed_during_the_check_is_SND_CONN_001_and_the_old_key_is_not_overwritten()
    {
        var (world, row) = await Holding();
        var service = During(world, async () => _ = await world.Store.Remove(row.Name, Token));

        var replaced = await service.Handle(new ReplaceConnectionKeyRequest("linear:work", KeyEnv: null, Key: Fresh), Token);

        Assert.Equal(SindriErrors.ConnectionNotFound.Code, replaced.Error.Code);
        Assert.Empty(world.Store.Rows);
        Assert.Equal(Old, world.Secrets.Read(row.SecretPointer!).Value.AccessToken);
    }

    [Fact]
    public async Task A_row_that_turned_into_an_oauth_connection_during_the_check_is_refused_and_keeps_its_tokens()
    {
        var (world, row) = await Holding();
        var service = During(world, async () => _ = await world.Store.Upsert(row with { Kind = ConnectionKind.OAuth }, Token));

        var replaced = await service.Handle(new ReplaceConnectionKeyRequest("linear:work", KeyEnv: null, Key: Fresh), Token);

        Assert.Equal("SND-CLI-003", replaced.Error.Code);
        Assert.Contains("signin", replaced.Error.Message, StringComparison.Ordinal);
        Assert.Equal(Old, world.Secrets.Read(row.SecretPointer!).Value.AccessToken);
    }

    [Fact]
    public async Task A_keyed_mcp_connection_takes_the_new_key_unchecked_and_the_answer_says_so()
    {
        var (world, _) = await Holding(ConnectionKind.Mcp, preset: null);

        var replaced = await world.ReplaceKey().Handle(new ReplaceConnectionKeyRequest("linear:work", KeyEnv: null, Key: Fresh), Token);

        Assert.True(replaced.IsSuccess);
        Assert.Equal((false, "an mcp connection has no key check"), (replaced.Value.Checked, replaced.Value.UncheckedReason));
        Assert.Empty(world.Keys.Calls);
        Assert.Equal(Fresh, StoredKey(world));
        Assert.Equal(ConnectionKind.Mcp, Assert.Single(world.Store.Rows).Kind);
    }

    [Fact]
    public async Task An_oauth_mcp_connection_keeps_the_sign_in_refusal_and_its_tokens()
    {
        var (world, row) = await Holding(ConnectionKind.Mcp, new TokenSet(Old, "mcp-refresh", KnownInstants.Epoch.AddHours(1)), preset: null);

        var replaced = await world.ReplaceKey().Handle(new ReplaceConnectionKeyRequest("linear:work", KeyEnv: null, Key: Fresh), Token);

        Assert.Equal("SND-CLI-003", replaced.Error.Code);
        Assert.Contains("signin", replaced.Error.Message, StringComparison.Ordinal);
        Assert.Equal(Old, world.Secrets.Read(row.SecretPointer!).Value.AccessToken);
    }

    // #4482: the row says how the connection signs in, so a server whose own sign-in returns a bare token is not mistaken for a key.
    [Fact]
    public async Task An_mcp_row_marked_signin_is_refused_even_when_its_token_is_bare()
    {
        var (world, row) = await Holding(ConnectionKind.Mcp, new TokenSet(Old, RefreshToken: null, ExpiresAt: null), preset: null, auth: "signin");

        var replaced = await world.ReplaceKey().Handle(new ReplaceConnectionKeyRequest("linear:work", KeyEnv: null, Key: Fresh), Token);

        Assert.Equal("SND-CLI-003", replaced.Error.Code);
        Assert.Contains("signin", replaced.Error.Message, StringComparison.Ordinal);
        Assert.Equal(Old, world.Secrets.Read(row.SecretPointer!).Value.AccessToken);
    }

    [Fact]
    public async Task An_mcp_row_marked_key_takes_the_new_key_even_when_its_token_has_a_refresh_token()
    {
        var (world, _) = await Holding(ConnectionKind.Mcp, new TokenSet(Old, "odd-refresh", KnownInstants.Epoch.AddHours(1)), preset: null, auth: "key");

        var replaced = await world.ReplaceKey().Handle(new ReplaceConnectionKeyRequest("linear:work", KeyEnv: null, Key: Fresh), Token);

        Assert.True(replaced.IsSuccess);
        Assert.Equal(Fresh, StoredKey(world));
    }

    [Fact]
    public async Task An_mcp_add_with_a_key_records_auth_key_on_its_row()
    {
        var world = new ConnectionHarness();
        world.Environment.Set("MCP_KEY", Fresh);

        var added = await world.Add().Handle(new AddConnectionRequest("acme:tools", Preset: null, Kind: "mcp", Client: null, KeyEnv: "MCP_KEY", Scopes: [], Wait: true, Url: "https://mcp.example.test/"), Token);

        Assert.True(added.IsSuccess);
        Assert.Equal("key", Assert.Single(world.Store.Rows).Config["auth"]);
    }

    [Fact]
    public async Task An_mcp_add_through_the_servers_own_sign_in_records_auth_signin_on_its_row()
    {
        var world = new ConnectionHarness();
        var pending = world.Add().Handle(new AddConnectionRequest("acme:tools", Preset: null, Kind: "mcp", Client: null, KeyEnv: null, Scopes: [], Wait: true, Url: "http://127.0.0.1:1/mcp"), Token).AsTask();
        world.SignIn.Succeed(new TokenSet("access-1", RefreshToken: null, ExpiresAt: null));

        var added = await pending;

        Assert.True(added.IsSuccess);
        Assert.Equal("signin", Assert.Single(world.Store.Rows).Config["auth"]);
    }
}
