using Aw.Kernel.Testing;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxUp;

[Lane(TestLane.Unit)]
public sealed class FakeDeployFilesContractTests : DeployFilesContract
{
    protected override DeployFilesRig Create(IReadOnlyDictionary<string, byte[]> remote, IReadOnlyDictionary<string, byte[]> local)
        => new FakeRig(new FakeDeployFiles(remote, local));

    private sealed class FakeRig(FakeDeployFiles fake) : DeployFilesRig
    {
        public override Core.Onyx.IDeployFiles Files => fake;

        public override string DeployDir => "/deploy";

        public override IReadOnlyList<string> Requested => fake.Requested;

        public override byte[]? ReadLocal(string path) => fake.Local.TryGetValue(path, out var bytes) ? bytes : null;
    }
}
