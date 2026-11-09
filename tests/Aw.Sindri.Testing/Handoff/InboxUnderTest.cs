using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Handoff;

namespace Aw.Sindri.Testing.Handoff;

/// <summary>One implementation of the inbox contract, wired to a fake inbox the test can seed and inspect.</summary>
/// <param name="client">The implementation under test.</param>
/// <param name="backing">The fake inbox behind it.</param>
/// <param name="baseUrl">The base URL a target uses.</param>
/// <param name="owned">Something to dispose with it (a server), or null.</param>
public sealed class InboxUnderTest(IInboxClient client, FakeInbox backing, Uri baseUrl, IDisposable? owned) : IDisposable
{
    /// <summary>Gets the implementation under test.</summary>
    public IInboxClient Client { get; } = client;

    /// <summary>Gets the fake inbox behind it: seed it, read what it took.</summary>
    public FakeInbox Backing { get; } = backing;

    /// <summary>Gets the base URL a target uses.</summary>
    public Uri BaseUrl { get; } = baseUrl;

    /// <summary>Makes a target for an owner with the right key.</summary>
    /// <param name="owner">The owner.</param>
    /// <returns>The target.</returns>
    public InboxTarget Target(string owner) => new(BaseUrl, new TokenSet(Backing.Key, RefreshToken: null, ExpiresAt: null), owner);

    /// <summary>Makes a target for an owner with a wrong key.</summary>
    /// <param name="owner">The owner.</param>
    /// <returns>The target.</returns>
    public InboxTarget WrongKey(string owner) => new(BaseUrl, new TokenSet("not-" + Backing.Key, RefreshToken: null, ExpiresAt: null), owner);

    /// <inheritdoc />
    public void Dispose() => owned?.Dispose();
}
