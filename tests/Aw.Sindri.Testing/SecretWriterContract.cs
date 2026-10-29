using Aw.Sindri.Core.Secrets;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>
/// The contract every <see cref="ISecretWriter"/> honours, with the resolver that reads back what it wrote. Run against the in-memory fake and KeychainSecretWriter (its owner-only file fallback, which a resolver reads on any machine).
/// ISecretPrompts has no contract: it is a one-call wrapper over the OS (read a console line with echo off) with nothing to pin down but the OS; its fake only records calls (recorded exemption, owner review of #357).
/// </summary>
public abstract class SecretWriterContract
{
    private const string Key = "on_Abc123-xyz.789";

    /// <summary>The writer under test and the resolver for the pointers it returns.</summary>
    /// <returns>Both, over the same store.</returns>
    protected abstract (ISecretWriter Writer, ISecretPointers Pointers) Create();

    /// <summary>A stored secret resolves, through the returned pointer, to the same value.</summary>
    [Fact]
    public void A_stored_secret_resolves_to_the_same_value()
    {
        var (writer, pointers) = Create();

        var pointer = writer.Store("onyx", "sindri-onyx", Key).Value;

        Expect.Equal(Key, pointers.Resolve(pointer).Value);
    }

    /// <summary>Storing again under the same name replaces the secret.</summary>
    [Fact]
    public void Storing_again_replaces_the_secret()
    {
        var (writer, pointers) = Create();
        _ = writer.Store("onyx", "sindri-onyx", Key);

        var pointer = writer.Store("onyx", "sindri-onyx", "on_second-0123").Value;

        Expect.Equal("on_second-0123", pointers.Resolve(pointer).Value);
    }
}
