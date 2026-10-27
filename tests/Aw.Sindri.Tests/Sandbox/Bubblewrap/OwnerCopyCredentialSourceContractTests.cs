using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Testing.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>The owner-copy source passes the credential-source contract every adapter passes (sandbox spec SB18).</summary>
[Lane(TestLane.Integration)]
public sealed class OwnerCopyCredentialSourceContractTests : IModelCredentialSourceContract
{
    protected override IModelCredentialSource Create(LoginState state) => new FakeOwnerHome(state).Source();
}
