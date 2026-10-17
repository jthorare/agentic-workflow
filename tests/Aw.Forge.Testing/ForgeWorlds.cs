using Aw.Forge.Core.Refs;
using Aw.Forge.Infrastructure;
using Aw.Redaction;

namespace Aw.Forge.Testing;

/// <summary>Ready-made adapters for the contract runs.</summary>
public static class ForgeWorlds
{
    /// <summary>The host the fake and file worlds allow.</summary>
    public const string Host = "forge.test";

    /// <summary>A repository on <see cref="Host"/>.</summary>
    public static ForgeRepo Repo { get; } = new(Host, "acme", "widgets");

    /// <summary>A fresh in-memory forge with its own world.</summary>
    /// <returns>The adapter; a push is simulated.</returns>
    public static ForgeUnderTest Fake()
    {
        var forge = new FakeForge(new ForgeHosts([Host]));
        return new ForgeUnderTest(forge, forge, Repo, () => forge.Requests, CanPush: true);
    }

    /// <summary>A file forge whose world lives under <paramref name="directory"/>.</summary>
    /// <param name="directory">The directory; the caller deletes it.</param>
    /// <returns>The adapter; a push goes through real git, so the contract skips Push for it.</returns>
    public static ForgeUnderTest File(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        var forge = new FileForge(new ForgeHosts([Host]), directory, Redactor.Default);
        return new ForgeUnderTest(forge, forge, Repo, () => forge.Requests, CanPush: false);
    }
}
