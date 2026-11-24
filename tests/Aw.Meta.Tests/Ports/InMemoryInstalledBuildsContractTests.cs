using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Unit)]
public sealed class InMemoryInstalledBuildsContractTests : InstalledBuildsContract
{
    protected override (IInstalledBuilds Builds, Action<string, string, IReadOnlyList<string>?> Put) Create()
    {
        var fake = new InMemoryInstalledBuilds();
        return (fake, (name, revision, inputs) => fake.Put(name, new InstalledBinary(revision, inputs)));
    }
}
