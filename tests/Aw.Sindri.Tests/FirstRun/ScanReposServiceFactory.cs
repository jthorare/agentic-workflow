using Aw.Sindri.Features.ScanRepos;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.FirstRun;

/// <summary>Builds the scan service over a slice's caller and profile, a fake tree and a fake GitHub read.</summary>
internal static class ScanReposServiceFactory
{
    public static ScanReposService Create(SliceHarness slice, FakeRepoScanTree tree, FakeKnownRepos known) => new(slice.Callers, tree, known, slice.Profiles);
}
