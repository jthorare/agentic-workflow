using Aw.Kernel.Testing;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxUp;

[Lane(TestLane.Unit)]
public sealed class FakeOnyxHealthContractTests : OnyxHealthContract
{
    protected override OnyxHealthRig Create(OnyxServerScript script) => new FakeRig(script.Expected);

    private sealed class FakeRig(OnyxProbe answer) : OnyxHealthRig
    {
        private readonly FakeOnyxHealth _fake = new() { Default = answer };

        public override IOnyxHealth Health => _fake;

        public override int Port => 3000;
    }
}
