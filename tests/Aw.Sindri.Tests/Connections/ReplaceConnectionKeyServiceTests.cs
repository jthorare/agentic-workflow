using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Features.AddConnection;
using Aw.Sindri.Testing.Connections;

namespace Aw.Sindri.Tests.Connections;

/// <summary>#4358: a connected key connection takes a new key without a remove and re-add; the new key is checked like a new one and the old key stays until it passes.</summary>
[Lane(TestLane.Unit)]
public sealed class ReplaceConnectionKeyServiceTests
{
    private const string Old = "lin-api-old-0001";
    private const string Fresh = "lin-api-new-0002";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<(ConnectionHarness World, Connection Row)> Holding(ConnectionStatus status = ConnectionStatus.Connected, string? preset = "linear", ConnectionKind kind = ConnectionKind.Rest)
    {
        var world = new ConnectionHarness();
        await world.Hold();
        var row = ConnectionSamples.Oauth(ConnectionSamples.Name("linear", "work"), status) with { Kind = kind, Preset = preset, Config = new Dictionary<string, string>(StringComparer.Ordinal) };
        var pointer = world.Secrets.Store(row.Name, new TokenSet(Old, RefreshToken: null, ExpiresAt: null)).Value;
        row = row with { SecretPointer = pointer };
        _ = await world.Store.Upsert(row, Token);
        return (world, row);
    }

    private static string StoredKey(ConnectionHarness world) => world.Secrets.Read(Assert.Single(world.Store.Rows).SecretPointer!).Value.AccessToken;

    [Fact]
    public async Task A_good_key_replaces_the_old_one_in_the_keychain_and_the_row_stays_connected()
    {
        var (world, row) = await Holding(ConnectionStatus.Error);
        world.Clock.Advance(TimeSpan.FromHours(1));

        var replaced = await world.ReplaceKey().Handle(new ReplaceConnectionKeyRequest("linear:work", KeyEnv: null, Key: Fresh), Token);

        Assert.True(replaced.IsSuccess);
        Assert.Equal((true, null), (replaced.Value.Checked, replaced.Value.UncheckedReason));
        Assert.Equal(("linear", Fresh), Assert.Single(world.Keys.Calls));
        Assert.Equal(Fresh, StoredKey(world));
        var stored = Assert.Single(world.Store.Rows);
        Assert.Equal((ConnectionStatus.Connected, null, row.CreatedAt, row.CreatedBy, row.Name), (stored.Status, stored.StatusReason, stored.CreatedAt, stored.CreatedBy, stored.Name));
        Assert.Equal(world.Clock.GetUtcNow(), stored.UpdatedAt);
    }

    [Theory]
    [InlineData(true, false, false, "SND-CONN-018")]
    [InlineData(false, true, false, "SND-CONN-019")]
    [InlineData(false, false, true, "SND-CONN-020")]
    public async Task A_refused_new_key_keeps_the_old_key_and_the_row_as_they_were(bool refuses, bool unreachable, bool limited, string code)
    {
        var (world, row) = await Holding();
        (world.Keys.Refuses, world.Keys.Unreachable, world.Keys.RateLimited) = (refuses, unreachable, limited);

        var replaced = await world.ReplaceKey().Handle(new ReplaceConnectionKeyRequest("linear:work", KeyEnv: null, Key: Fresh), Token);

        Assert.Equal(code, replaced.Error.Code);
        Assert.DoesNotContain(Fresh, replaced.Error.Message, StringComparison.Ordinal);
        Assert.Equal(Old, StoredKey(world));
        Assert.Equal(row, Assert.Single(world.Store.Rows));
    }

    [Fact]
    public async Task A_key_named_by_variable_is_read_from_the_environment_and_trimmed()
    {
        var (world, _) = await Holding();
        world.Environment.Set("LINEAR_NEW_KEY", Fresh + "\r\n");

        var replaced = await world.ReplaceKey().Handle(new ReplaceConnectionKeyRequest("linear:work", KeyEnv: "LINEAR_NEW_KEY"), Token);

        Assert.True(replaced.IsSuccess);
        Assert.Equal(("linear", Fresh), Assert.Single(world.Keys.Calls));
        Assert.Equal(Fresh, StoredKey(world));
    }

    [Fact]
    public async Task No_key_or_only_whitespace_is_SND_CLI_003_and_checks_nothing()
    {
        var (world, _) = await Holding();

        var none = await world.ReplaceKey().Handle(new ReplaceConnectionKeyRequest("linear:work"), Token);
        var blank = await world.ReplaceKey().Handle(new ReplaceConnectionKeyRequest("linear:work", KeyEnv: null, Key: " \n"), Token);

        Assert.Equal(("SND-CLI-003", "SND-CLI-003"), (none.Error.Code, blank.Error.Code));
        Assert.Empty(world.Keys.Calls);
        Assert.Equal(Old, StoredKey(world));
    }

    [Fact]
    public async Task A_key_connection_with_no_preset_takes_the_new_key_unchecked()
    {
        var (world, _) = await Holding(preset: null);

        var replaced = await world.ReplaceKey().Handle(new ReplaceConnectionKeyRequest("linear:work", KeyEnv: null, Key: Fresh), Token);

        Assert.True(replaced.IsSuccess);
        Assert.Empty(world.Keys.Calls);
        Assert.Equal(Fresh, StoredKey(world));
        Assert.False(replaced.Value.Checked);
        Assert.Contains("no preset", replaced.Value.UncheckedReason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_cli_connection_takes_the_new_key_unchecked_and_the_answer_says_so()
    {
        var (world, _) = await Holding(kind: ConnectionKind.Cli);

        var replaced = await world.ReplaceKey().Handle(new ReplaceConnectionKeyRequest("linear:work", KeyEnv: null, Key: Fresh), Token);

        Assert.True(replaced.IsSuccess);
        Assert.Empty(world.Keys.Calls);
        Assert.Equal(Fresh, StoredKey(world));
        Assert.False(replaced.Value.Checked);
        Assert.Contains("cli connection", replaced.Value.UncheckedReason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_preset_with_no_key_check_stores_the_key_and_says_it_was_not_checked()
    {
        var (world, _) = await Holding();
        var plain = ConnectionHarness.Linear with { Key = "acme", DisplayName = "Acme", ProbeUrl = null };
        world.Presets = new FakeConnectionPresets(plain);
        _ = await world.Store.Upsert(Assert.Single(world.Store.Rows) with { Preset = "acme" }, Token);

        var replaced = await world.ReplaceKey().Handle(new ReplaceConnectionKeyRequest("linear:work", KeyEnv: null, Key: Fresh), Token);

        Assert.True(replaced.IsSuccess);
        Assert.False(replaced.Value.Checked);
        Assert.Equal("Acme has no key check", replaced.Value.UncheckedReason);
        Assert.Equal(Fresh, StoredKey(world));
    }

    [Fact]
    public async Task A_row_whose_preset_left_the_catalog_is_refused_like_an_unknown_preset_and_nothing_is_checked_or_written()
    {
        var (world, row) = await Holding(preset: "gone");

        var replaced = await world.ReplaceKey().Handle(new ReplaceConnectionKeyRequest("linear:work", KeyEnv: null, Key: Fresh), Token);

        Assert.Equal("SND-CLI-003", replaced.Error.Code);
        Assert.Contains("no connection preset named 'gone'", replaced.Error.Message, StringComparison.Ordinal);
        Assert.Empty(world.Keys.Calls);
        Assert.Equal(Old, StoredKey(world));
        Assert.Equal(row, Assert.Single(world.Store.Rows));
    }

    [Fact]
    public async Task A_connection_that_needs_a_sign_in_ends_connected_and_clears_its_needs_you_row()
    {
        var (world, _) = await Holding(ConnectionStatus.NeedsSignIn);

        var replaced = await world.ReplaceKey().Handle(new ReplaceConnectionKeyRequest("linear:work", KeyEnv: null, Key: Fresh), Token);

        Assert.True(replaced.IsSuccess);
        Assert.Equal(ConnectionStatus.Connected, Assert.Single(world.Store.Rows).Status);
        Assert.Contains(EventTypes.ConnectionSignedIn, world.Ledger.Events.Select(static e => e.Envelope.Type), StringComparer.Ordinal);
    }

    [Fact]
    public async Task An_oauth_connection_has_no_key_to_replace_and_keeps_its_tokens()
    {
        var (world, _) = await Holding(kind: ConnectionKind.OAuth);

        var replaced = await world.ReplaceKey().Handle(new ReplaceConnectionKeyRequest("linear:work", KeyEnv: null, Key: Fresh), Token);

        Assert.Equal("SND-CLI-003", replaced.Error.Code);
        Assert.Contains("signin", replaced.Error.Message, StringComparison.Ordinal);
        Assert.Empty(world.Keys.Calls);
        Assert.Equal(Old, StoredKey(world));
    }

    [Fact]
    public async Task An_unknown_name_is_SND_CONN_001_and_checks_nothing()
    {
        var world = new ConnectionHarness();

        var replaced = await world.ReplaceKey().Handle(new ReplaceConnectionKeyRequest("linear:nobody", KeyEnv: null, Key: Fresh), Token);

        Assert.Equal(SindriErrors.ConnectionNotFound.Code, replaced.Error.Code);
        Assert.Empty(world.Keys.Calls);
    }

    [Fact]
    public async Task A_keychain_that_cannot_store_keeps_the_row_and_says_so_without_the_key()
    {
        var (world, row) = await Holding();
        world.Secrets.Fails = true;

        var replaced = await world.ReplaceKey().Handle(new ReplaceConnectionKeyRequest("linear:work", KeyEnv: null, Key: Fresh), Token);

        Assert.True(replaced.IsError);
        Assert.DoesNotContain(Fresh, replaced.Error.Message, StringComparison.Ordinal);
        Assert.Equal(row, Assert.Single(world.Store.Rows));
    }
}
