using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Testing.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>The owner-copy source passes the credential-source contract every adapter passes (sandbox spec SB18).</summary>
[Lane(TestLane.Integration)]
public sealed class OwnerCopyCredentialSourceContractTests : IModelCredentialSourceContract, IDisposable
{
    private readonly List<FakeOwnerHome> _homes = [];

    /// <summary>The home path of each fake made so far.</summary>
    internal IReadOnlyList<string> HomePaths => [.. _homes.Select(h => h.Root)];

    /// <summary>Makes a source the way the contract base does; the cleanup tests call it.</summary>
    /// <param name="state">The login state.</param>
    /// <returns>The source.</returns>
    internal IModelCredentialSource CreateForTest(LoginState state) => Create(state);

    public void Dispose()
    {
        foreach (var home in _homes)
        {
            home.Dispose();   // best effort by itself, so one home cannot skip the next (#3262)
        }

        _homes.Clear();
    }

    protected override IModelCredentialSource Create(LoginState state)
    {
        var home = new FakeOwnerHome(state);
        _homes.Add(home);
        return home.Source();
    }
}
