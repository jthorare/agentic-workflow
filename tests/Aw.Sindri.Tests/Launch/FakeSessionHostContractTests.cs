using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Testing.Launch;

namespace Aw.Sindri.Tests.Launch;

[Lane(TestLane.Unit)]
public sealed class FakeSessionHostContractTests : SessionHostContract
{
    protected override ISessionHost Create() => new FakeSessionHost();

    protected override HostSession Session(string name) =>
        new(name, "/work/repo", ["sleep", "30"], new Dictionary<string, string>(StringComparer.Ordinal));
}
