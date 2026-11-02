using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Testing.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>The contract base on the test-only pass-through: the cases that need confinement are skipped, the rest is its documented behaviour.</summary>
[Lane(TestLane.Integration)]
public sealed class PassThroughSandboxContractTests : ISandboxContract
{
    protected override bool Confines => false;

    protected override string? Unavailable => null;

    protected override ISandbox CreateSandbox() => new PassThroughSandbox();
}
