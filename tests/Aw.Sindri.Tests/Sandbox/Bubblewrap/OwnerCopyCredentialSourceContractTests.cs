using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Testing.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>The owner-copy source passes the credential-source contract every adapter passes (sandbox spec SB18).</summary>
[Lane(TestLane.Integration)]
public sealed class OwnerCopyCredentialSourceContractTests : IModelCredentialSourceContract, IDisposable
{
    private readonly List<FakeOwnerHome> _homes = [];

    public void Dispose()
    {
        foreach (var home in _homes)
        {
            home.Dispose();
        }
    }

    protected override IModelCredentialSource Create(LoginState state)
    {
        var home = new FakeOwnerHome(state);
        _homes.Add(home);
        return home.Source();
    }
}
