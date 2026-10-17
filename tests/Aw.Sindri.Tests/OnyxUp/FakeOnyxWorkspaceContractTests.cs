using Aw.Kernel.Testing;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxUp;

[Lane(TestLane.Unit)]
public sealed class FakeOnyxWorkspaceContractTests : OnyxWorkspaceContract
{
    protected override OnyxWorkspaceRig Create() => new FakeRig();

    private sealed class FakeRig : OnyxWorkspaceRig
    {
        private readonly FakeOnyxWorkspace _fake = new();

        public override IOnyxWorkspace Workspace => _fake;

        public override OnyxPaths Paths { get; } = OnyxPaths.For("/home");

        public override void WriteRawState(string text) => _fake.StateIsCorrupt = true;
    }
}
