using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Resources;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Infrastructure.Resources;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Resources;

[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
[Lane(TestLane.Integration)]
public sealed class ResourceDoctorChecksTests
{
    [Fact]
    public async Task A_bookkeeping_read_that_fails_is_a_warning_not_ok()
    {
        using var home = new TempHome();
        var blocked = Path.Combine(home.Path, "not-a-folder");
        await File.WriteAllTextAsync(blocked, "a file where the home folder should be", TestContext.Current.CancellationToken);
        var clock = new FakeTimeProvider(KnownInstants.Epoch);
        using var ledger = new LedgerDatabase(new SindriSettings(blocked, Path.Combine(home.Path, "profile.yaml")), clock);
        var checks = new ResourceDoctorChecks(new EmptyFiles(), new ResourceProvisioner(new ResourceStore(Path.Combine(home.Path, "resources")), ledger, clock));

        var check = Assert.Single(await checks.Run(TestContext.Current.CancellationToken));

        Assert.Equal(("resources", CheckState.Warn), (check.Name, check.State));
    }

    private sealed class EmptyFiles : IResourceFiles
    {
        public Result<IReadOnlyList<ResourceEntry>> List(ResourceOwner? owner) => Results.Success<IReadOnlyList<ResourceEntry>>([]);

        public Result<int> Purge(ResourceOwner owner) => Results.Success(0);
    }
}
