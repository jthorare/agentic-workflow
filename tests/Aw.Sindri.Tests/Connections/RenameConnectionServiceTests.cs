using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Features.RenameConnection;
using Aw.Sindri.Testing.Connections;

namespace Aw.Sindri.Tests.Connections;

[Lane(TestLane.Unit)]
public sealed class RenameConnectionServiceTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Rename_changes_only_the_label_and_keeps_the_rest_of_the_row()
    {
        var world = new ConnectionHarness();
        _ = world.Secrets.Store(ConnectionSamples.Name("slack", "old"), new TokenSet("access-1", "refresh-1", ExpiresAt: null));
        _ = await world.Store.Upsert(ConnectionSamples.Oauth(ConnectionSamples.Name("slack", "old")), Token);

        var renamed = await world.Rename().Handle(new RenameConnectionRequest("slack:old", "new"), Token);

        Assert.True(renamed.IsSuccess);
        Assert.Equal("slack:new", renamed.Value.Name);
        Assert.Equal("slack:old", renamed.Value.PreviousName);
        var row = Assert.Single(world.Store.Rows);
        Assert.Equal("slack:new", row.Name.ToString());
        Assert.Equal("slack", row.Preset);
        Assert.Equal("client-123", row.Config["clientId"]);
        Assert.Equal(ConnectionStatus.Connected, row.Status);
        Assert.Equal("human", row.CreatedBy);
    }

    [Fact]
    public async Task The_secret_still_resolves_after_the_rename_and_the_old_entry_is_gone()
    {
        var world = new ConnectionHarness();
        var oldPointer = world.Secrets.Store(ConnectionSamples.Name("slack", "old"), new TokenSet("access-1", "refresh-1", ExpiresAt: null)).Value;
        _ = await world.Store.Upsert(ConnectionSamples.Oauth(ConnectionSamples.Name("slack", "old")), Token);

        var renamed = await world.Rename().Handle(new RenameConnectionRequest("slack:old", "new"), Token);

        Assert.True(renamed.Value.SecretMoved);
        var row = Assert.Single(world.Store.Rows);
        var tokens = world.Secrets.Read(row.SecretPointer!);
        Assert.True(tokens.IsSuccess);
        Assert.Equal("access-1", tokens.Value.AccessToken);
        Assert.NotEqual(oldPointer.ToString(), row.SecretPointer!.ToString(), StringComparer.Ordinal);
        Assert.Equal([row.SecretPointer.ToString()], world.Secrets.Pointers);
        Assert.Equal([oldPointer.ToString()], world.Secrets.Deleted);
    }

    [Fact]
    public async Task A_connection_with_no_secret_yet_renames_without_one()
    {
        var world = new ConnectionHarness();
        _ = await world.Store.Upsert(ConnectionSamples.Oauth(ConnectionSamples.Name("slack", "old")) with { SecretPointer = null }, Token);

        var renamed = await world.Rename().Handle(new RenameConnectionRequest("slack:old", "new"), Token);

        Assert.True(renamed.IsSuccess);
        Assert.False(renamed.Value.SecretMoved);
        Assert.Null(Assert.Single(world.Store.Rows).SecretPointer);
        Assert.Empty(world.Secrets.Pointers);
    }

    [Fact]
    public async Task An_unknown_name_is_SND_CONN_001()
    {
        var world = new ConnectionHarness();

        var renamed = await world.Rename().Handle(new RenameConnectionRequest("slack:nobody", "new"), Token);

        Assert.Equal(SindriErrors.ConnectionNotFound.Code, renamed.Error.Code);
    }

    [Fact]
    public async Task A_new_name_another_connection_uses_is_SND_CONN_010_naming_it_and_nothing_changes()
    {
        var world = new ConnectionHarness();
        _ = world.Secrets.Store(ConnectionSamples.Name("slack", "old"), new TokenSet("access-1", "refresh-1", ExpiresAt: null));
        _ = world.Secrets.Store(ConnectionSamples.Name("slack", "taken"), new TokenSet("access-2", "refresh-2", ExpiresAt: null));
        _ = await world.Store.Upsert(ConnectionSamples.Oauth(ConnectionSamples.Name("slack", "old")), Token);
        _ = await world.Store.Upsert(ConnectionSamples.Oauth(ConnectionSamples.Name("slack", "taken")), Token);

        var renamed = await world.Rename().Handle(new RenameConnectionRequest("slack:old", "taken"), Token);

        Assert.Equal(SindriErrors.ConnectionExists.Code, renamed.Error.Code);
        Assert.Contains("slack:taken", renamed.Error.Message, StringComparison.Ordinal);
        Assert.Equal(["slack:old", "slack:taken"], world.Store.Rows.Select(static r => r.Name.ToString()), StringComparer.Ordinal);
        Assert.Equal(2, world.Secrets.Pointers.Count);
        Assert.Empty(world.Secrets.Deleted);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("Has Caps")]
    [InlineData("a:b")]
    [InlineData("-leading")]
    [InlineData("this-label-is-longer-than-thirty-two-characters")]
    public async Task A_bad_label_is_SND_CONN_008(string label)
    {
        var world = new ConnectionHarness();
        _ = await world.Store.Upsert(ConnectionSamples.Oauth(ConnectionSamples.Name("slack", "old")), Token);

        var renamed = await world.Rename().Handle(new RenameConnectionRequest("slack:old", label), Token);

        Assert.Equal(SindriErrors.ConnectionNameInvalid.Code, renamed.Error.Code);
        Assert.Equal("slack:old", Assert.Single(world.Store.Rows).Name.ToString());
    }

    [Fact]
    public async Task A_bad_current_name_is_SND_CONN_008()
    {
        var world = new ConnectionHarness();

        var renamed = await world.Rename().Handle(new RenameConnectionRequest("not-a-name", "new"), Token);

        Assert.Equal(SindriErrors.ConnectionNameInvalid.Code, renamed.Error.Code);
    }

    [Fact]
    public async Task Renaming_to_the_same_label_is_refused_as_invalid_input_and_changes_nothing()
    {
        var world = new ConnectionHarness();
        _ = world.Secrets.Store(ConnectionSamples.Name("slack", "old"), new TokenSet("access-1", "refresh-1", ExpiresAt: null));
        _ = await world.Store.Upsert(ConnectionSamples.Oauth(ConnectionSamples.Name("slack", "old")), Token);

        var renamed = await world.Rename().Handle(new RenameConnectionRequest("slack:old", "old"), Token);

        Assert.Equal(SindriErrors.InvalidInput.Code, renamed.Error.Code);
        Assert.Empty(world.Secrets.Deleted);
        _ = Assert.Single(world.Store.Rows);
    }

    [Fact]
    public async Task A_secret_store_that_fails_keeps_the_old_row_and_names_no_secret()
    {
        var world = new ConnectionHarness();
        _ = world.Secrets.Store(ConnectionSamples.Name("slack", "old"), new TokenSet("access-1", "refresh-1", ExpiresAt: null));
        _ = await world.Store.Upsert(ConnectionSamples.Oauth(ConnectionSamples.Name("slack", "old")), Token);
        world.Secrets.Fails = true;

        var renamed = await world.Rename().Handle(new RenameConnectionRequest("slack:old", "new"), Token);

        Assert.Equal(SindriErrors.ConnectionSecretUnavailable.Code, renamed.Error.Code);
        Assert.DoesNotContain("access-1", renamed.Error.Message, StringComparison.Ordinal);
        Assert.Equal("slack:old", Assert.Single(world.Store.Rows).Name.ToString());
    }

    [Fact]
    public async Task A_connection_that_needs_a_sign_in_moves_its_needs_you_row_to_the_new_name()
    {
        var world = new ConnectionHarness();
        await world.Hold();
        _ = world.Secrets.Store(ConnectionSamples.Name("slack", "old"), new TokenSet("access-1", "refresh-1", ExpiresAt: null));
        _ = await world.Store.Upsert(ConnectionSamples.Oauth(ConnectionSamples.Name("slack", "old"), ConnectionStatus.NeedsSignIn), Token);

        var renamed = await world.Rename().Handle(new RenameConnectionRequest("slack:old", "new"), Token);

        Assert.True(renamed.IsSuccess);
        Assert.Equal(ConnectionStatus.NeedsSignIn, Assert.Single(world.Store.Rows).Status);
        var cleared = Assert.Single(world.Ledger.Events, static e => e.Envelope.Type == EventTypes.ConnectionSignedIn);
        Assert.Contains("slack:old", cleared.Envelope.Payload, StringComparison.Ordinal);
        var raised = Assert.Single(world.Ledger.Events, static e => e.Envelope.Type == EventTypes.ConnectionNeedsSignIn);
        Assert.Contains("slack:new", raised.Envelope.Payload, StringComparison.Ordinal);
    }
}
