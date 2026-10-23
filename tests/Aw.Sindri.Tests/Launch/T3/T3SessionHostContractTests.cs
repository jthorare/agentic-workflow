using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Testing.Launch;

namespace Aw.Sindri.Tests.Launch.T3;

/// <summary>The T3 host against the shared session host contract, over the fake T3.</summary>
[Lane(TestLane.Integration)]
public sealed class T3SessionHostContractTests : SessionHostContract, IDisposable
{
    private readonly T3Rig _rig = new();

    public void Dispose() => _rig.Dispose();

    protected override ISessionHost Create() => _rig.Host();

    protected override HostSession Session(string name) => _rig.Session(name);
}
