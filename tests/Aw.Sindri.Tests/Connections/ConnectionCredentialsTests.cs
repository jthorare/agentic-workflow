using Aw.Sindri.Core.Connections;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Connections;

namespace Aw.Sindri.Tests.Connections;

// #2508: one token order for every source a Connections-page connection can feed: the connected connection first, the profile pointer second.
public sealed class ConnectionCredentialsTests
{
    private readonly FakeSecretWriter _pointers = new();
    private readonly InMemoryConnectionStore _connections = new();
    private readonly FakeConnectionSecrets _connectionSecrets = new();

    public static TheoryData<string> Presets => ["github", "slack", "linear"];

    private string Pointer(string preset)
    {
        _ = _pointers.Store("aw", preset, preset + "-profile-token").Value;
        return "keychain:aw/" + preset;
    }

    private async Task Connect(string preset, ConnectionStatus status, string name = "work")
    {
        var connection = ConnectionSamples.Oauth(ConnectionSamples.Name(preset, name), status);
        _ = (await _connections.Insert(connection, CancellationToken.None)).Value;
        _ = _connectionSecrets.Store(connection.Name, new TokenSet(preset + "-connection-token-" + name, RefreshToken: null, ExpiresAt: null)).Value;
    }

    private ConnectionCredentials Credentials() => new(_connections, _connectionSecrets, _pointers);

    [Theory, MemberData(nameof(Presets)), Trait("Lane", "unit")]
    public async Task A_connected_connection_wins_over_the_profile_pointer(string preset)
    {
        await Connect(preset, ConnectionStatus.Connected);

        Assert.Equal(preset + "-connection-token-work", Credentials().TokenFor(preset, Pointer(preset)));
    }

    [Theory, MemberData(nameof(Presets)), Trait("Lane", "unit")]
    public async Task A_connected_connection_gives_the_token_when_the_profile_has_no_pointer(string preset)
    {
        await Connect(preset, ConnectionStatus.Connected);

        Assert.Equal(preset + "-connection-token-work", Credentials().TokenFor(preset, profilePointer: null));
    }

    [Theory, MemberData(nameof(Presets)), Trait("Lane", "unit")]
    public async Task A_connection_that_needs_sign_in_falls_back_to_the_pointer(string preset)
    {
        await Connect(preset, ConnectionStatus.NeedsSignIn);

        Assert.Equal(preset + "-profile-token", Credentials().TokenFor(preset, Pointer(preset)));
    }

    [Theory, MemberData(nameof(Presets)), Trait("Lane", "unit")]
    public void No_connection_gives_the_pointer(string preset)
        => Assert.Equal(preset + "-profile-token", Credentials().TokenFor(preset, Pointer(preset)));

    [Theory, MemberData(nameof(Presets)), Trait("Lane", "unit")]
    public void Neither_a_connection_nor_a_pointer_gives_null(string preset)
        => Assert.Null(Credentials().TokenFor(preset, profilePointer: null));

    [Theory, MemberData(nameof(Presets)), Trait("Lane", "unit")]
    public async Task A_connection_of_another_preset_is_ignored(string preset)
    {
        var other = preset == "slack" ? "linear" : "slack";
        await Connect(other, ConnectionStatus.Connected);

        Assert.Equal(preset + "-profile-token", Credentials().TokenFor(preset, Pointer(preset)));
        Assert.Null(Credentials().TokenFor(preset, profilePointer: null));
    }

    [Theory, MemberData(nameof(Presets)), Trait("Lane", "unit")]
    public async Task The_first_connected_connection_by_name_is_read(string preset)
    {
        await Connect(preset, ConnectionStatus.Connected, "zeta");
        await Connect(preset, ConnectionStatus.Connected, "alpha");

        Assert.Equal(preset + "-connection-token-alpha", Credentials().TokenFor(preset, profilePointer: null));
    }

    [Fact, Trait("Lane", "unit")]
    public void No_store_reads_the_pointer_alone()
        => Assert.Equal("slack-profile-token", new ConnectionCredentials(connections: null, connectionSecrets: null, _pointers).TokenFor("slack", Pointer("slack")));
}
