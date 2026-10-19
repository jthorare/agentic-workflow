using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Testing.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>The contract base on the real bubblewrap adapter. Skips cleanly where bwrap, a user namespace or git is missing.</summary>
[Lane(TestLane.Integration)]
public sealed class BubblewrapSandboxContractTests : ISandboxContract
{
    protected override bool Confines => true;

    protected override string? Unavailable => BubblewrapBox.Unavailable;

    protected override ISandbox CreateSandbox() => BubblewrapBox.Create();
}
