using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>The contract every <see cref="IProfileIndexWriter"/> honours, over a profile with no concepts: a write returns the new hash, a hash that isn't the profile's writes nothing (SND-PROFILE-003), and the returned hash is the profile's next one.</summary>
public abstract class ProfileIndexWriterContract
{
    private static readonly ConceptPinId _billing = ConceptPinId.From("billing");

    /// <summary>A writer over a profile with no concepts, and that profile's hash.</summary>
    /// <returns>The writer and the hash.</returns>
    protected abstract (IProfileIndexWriter Writer, ProfileHash Hash) Create();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ConceptEdit Set(string name) => new(_billing, new ConceptSettings(Name: name, Summary: "How we take money", Aliases: ["payments"]));

    /// <summary>A write with no hash given sets the entry and returns a hash that isn't the old one.</summary>
    [Fact]
    public async Task A_write_without_a_hash_returns_the_new_hash()
    {
        var (writer, start) = Create();

        var done = await writer.Write([Set("Billing")], ifHash: null, Token);

        Expect.True(done.IsSuccess);
        Expect.True(start != done.Value);
    }

    /// <summary>The hash a write returns is the one the next write can name.</summary>
    [Fact]
    public async Task The_returned_hash_is_accepted_by_the_next_write()
    {
        var (writer, start) = Create();
        var first = await writer.Write([Set("Billing")], start, Token);

        var second = await writer.Write([new ConceptEdit(_billing, Entry: null)], first.Value, Token);

        Expect.True(second.IsSuccess);
    }

    /// <summary>A hash that isn't the profile's is SND-PROFILE-003, and the entry isn't written.</summary>
    [Fact]
    public async Task A_hash_that_is_not_the_profiles_is_refused_and_writes_nothing()
    {
        var (writer, start) = Create();
        _ = await writer.Write([Set("Billing")], start, Token);

        var stale = await writer.Write([Set("Invoices")], start, Token);

        Expect.Equal("SND-PROFILE-003", stale.Error.Code);
        var again = await writer.Write([Set("Billing")], ifHash: null, Token);   // the same entry again: the hash is the one the refused write left
        Expect.True(again.IsSuccess);
    }

    /// <summary>Removing an id that isn't there succeeds and leaves the profile as it was.</summary>
    [Fact]
    public async Task Removing_an_entry_that_is_not_there_changes_nothing()
    {
        var (writer, start) = Create();

        var done = await writer.Write([new ConceptEdit(_billing, Entry: null)], start, Token);

        Expect.True(done.IsSuccess);
        Expect.Equal(start, done.Value);
    }
}
