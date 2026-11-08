using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Xunit;

namespace Aw.Sindri.Testing.Connections;

/// <summary>
/// The contract every <see cref="IConnectionStore"/> honours: a row reads back whole (and holds no secret field), upsert replaces by name, list is
/// sorted by name, an unknown name is SND-CONN-001, and remove says whether a row was there.
/// </summary>
public abstract class ConnectionStoreContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A fresh, empty store.</summary>
    /// <returns>The store.</returns>
    protected abstract IConnectionStore Create();

    /// <summary>A stored connection reads back whole.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_upserted_connection_reads_back_whole()
    {
        var store = Create();
        var connection = ConnectionSamples.Oauth();

        _ = await store.Upsert(connection, Token);
        var read = await store.Get(connection.Name, Token);

        Expect.True(read.IsSuccess);
        Expect.Equal(connection.Name, read.Value.Name);
        Expect.Equal(connection.Kind, read.Value.Kind);
        Expect.Equal(connection.Preset, read.Value.Preset);
        Expect.Equal(connection.SecretPointer?.ToString(), read.Value.SecretPointer?.ToString());
        Expect.Equal(connection.Status, read.Value.Status);
        Expect.Equal(connection.CreatedBy, read.Value.CreatedBy);
        Expect.Equal(connection.CreatedAt, read.Value.CreatedAt);
        Expect.Equal(connection.UpdatedAt, read.Value.UpdatedAt);
        Expect.Sequence(connection.Config.OrderBy(static p => p.Key, StringComparer.Ordinal), read.Value.Config.OrderBy(static p => p.Key, StringComparer.Ordinal));
    }

    /// <summary>A second upsert of a name replaces the row.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Upserting_a_name_twice_replaces_the_row()
    {
        var store = Create();
        var connection = ConnectionSamples.Oauth();
        _ = await store.Upsert(connection, Token);

        _ = await store.Upsert(ConnectionSamples.Oauth(connection.Name, ConnectionStatus.NeedsSignIn), Token);
        var listed = await store.List(Token);

        Expect.Equal(1, listed.Value.Count);
        var only = listed.Value[0];
        Expect.Equal(ConnectionStatus.NeedsSignIn, only.Status);
        Expect.Equal("the provider refused the refresh", only.StatusReason);
    }

    /// <summary>The list is sorted by name.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_list_is_sorted_by_name()
    {
        var store = Create();
        _ = await store.Upsert(ConnectionSamples.Oauth(ConnectionSamples.Name("slack", "team")), Token);
        _ = await store.Upsert(ConnectionSamples.Oauth(ConnectionSamples.Name("github", "work")), Token);
        _ = await store.Upsert(ConnectionSamples.Oauth(ConnectionSamples.Name("github", "home")), Token);

        var listed = await store.List(Token);

        Expect.Sequence(["github:home", "github:work", "slack:team"], listed.Value.Select(static c => c.Name.ToString()));
    }

    /// <summary>An unknown name is SND-CONN-001.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_unknown_name_is_not_found()
    {
        var store = Create();

        var read = await store.Get(ConnectionSamples.Name("linear", "nobody"), Token);

        Expect.True(read.IsError);
        Expect.Equal(SindriErrors.ConnectionNotFound.Code, read.Error.Code);
    }

    /// <summary>Remove says whether a row was there.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Remove_says_whether_a_row_was_there()
    {
        var store = Create();
        var connection = ConnectionSamples.Oauth();
        _ = await store.Upsert(connection, Token);

        var first = await store.Remove(connection.Name, Token);
        var second = await store.Remove(connection.Name, Token);

        Expect.True(first.Value);
        Expect.True(!second.Value);
        Expect.Equal(0, (await store.List(Token)).Value.Count);
    }
}
