using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Infrastructure.Sessions;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>The macOS table passes the process table contract over the recorded ps and lsof output.</summary>
[Lane(TestLane.Unit)]
public sealed class MacProcessTableContractTests : ProcessTableContract
{
    protected override IProcessTable Create() => new MacProcessTable(new RecordedMacTools(), new PlantedMacArguments(), ownUid: 501);
}
