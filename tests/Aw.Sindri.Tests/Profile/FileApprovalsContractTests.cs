using Aw.Kernel.Testing;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Profile;

[Lane(TestLane.Integration)]
public sealed class FileApprovalsContractTests : ApprovalsContract, IDisposable
{
    private readonly ProfileFiles _files = new();

    public void Dispose() => _files.Dispose();

    protected override IApprovals Create() => _files.Approvals;

    protected override (LoadedProfile First, LoadedProfile Second) Profiles() => (SliceHarness.Profile("5s"), SliceHarness.Profile("7s", "ada"));
}
