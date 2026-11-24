using Aw.Kernel.Testing;
using Aw.Sindri.Core.Home;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.HomeLanes;

/// <summary>The in-memory aw-meta team against the reader contract.</summary>
[Lane(TestLane.Unit)]
public sealed class FakeMetaTeamReaderContractTests : MetaTeamReaderContract
{
    protected override IMetaTeamReader Create(MetaTeamRead team) => new FakeMetaTeamReader(team);
}
