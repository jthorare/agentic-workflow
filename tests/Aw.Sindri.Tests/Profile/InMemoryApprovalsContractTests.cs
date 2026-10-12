using Aw.Kernel.Testing;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Profile;

[Lane(TestLane.Unit)]
public sealed class InMemoryApprovalsContractTests : ApprovalsContract
{
    protected override IApprovals Create() => new InMemoryProfiles();

    protected override (LoadedProfile First, LoadedProfile Second) Profiles() => (SliceHarness.Profile("5s"), SliceHarness.Profile("7s", "ada"));
}
