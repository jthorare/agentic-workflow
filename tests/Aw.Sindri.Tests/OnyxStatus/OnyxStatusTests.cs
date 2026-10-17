using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Features.OnyxStatus;
using Aw.Sindri.Infrastructure.Onyx;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxStatus;

public sealed class OnyxStatusTests
{
    private const long Gib = 1L << 30;

    private static async Task<OnyxStatusResponse> Status(long freeBytes)
    {
        var probes = new FakeHostProbes { FreeBytes = freeBytes };
        _ = probes.Directories.Add("/sindri-home");
        var service = new OnyxStatusService(new EmbeddedOnyxPins(), new FakeContainerRuntime(FakeContainerRuntime.Healthy), probes, new FakeSindriHome("/sindri-home"));

        return (await service.Handle(new OnyxStatusRequest(), TestContext.Current.CancellationToken)).Value;
    }

    private static async Task<OnyxStatusResponse> StatusWithDataRoot(bool onThisHost, ContainerRuntimeFacts? docker = null)
    {
        var probes = new FakeHostProbes { FreeBytes = 100 * Gib };
        _ = probes.Directories.Add("/sindri-home");
        if (onThisHost)
        {
            _ = probes.Directories.Add("/var/lib/docker");
            probes.VolumeFreeBytes["/var/lib/docker"] = 12 * Gib;
        }

        var service = new OnyxStatusService(new EmbeddedOnyxPins(), new FakeContainerRuntime(docker ?? FakeContainerRuntime.Healthy), probes, new FakeSindriHome("/sindri-home"));

        return (await service.Handle(new OnyxStatusRequest(), TestContext.Current.CancellationToken)).Value;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_ignores_a_host_directory_of_the_data_root_name_when_docker_runs_in_a_vm()
    {
        var status = await StatusWithDataRoot(onThisHost: true, FakeContainerRuntime.Healthy);   // Docker Desktop, and a /var/lib/docker with 12 GiB on this host

        var disk = Assert.Single(status.Checks, static c => c.Name == "disk");
        Assert.Equal(CheckState.Ok, disk.State);
        Assert.Equal("100.0 GiB free at /sindri-home, needs 16.3 GiB; Docker's data lives in a VM here; its disk image size is capped separately (Docker Desktop: Settings → Resources)", disk.Detail);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_measures_a_native_engines_data_root()
    {
        var status = await StatusWithDataRoot(onThisHost: true, FakeContainerRuntime.NativeEngine);

        var disk = Assert.Single(status.Checks, static c => c.Name == "disk");
        Assert.Equal(CheckState.Fail, disk.State);
        Assert.Equal(1, status.ExitCode);
        Assert.StartsWith("12.0 GiB free at /var/lib/docker", disk.Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_keeps_the_home_measurement_and_notes_the_vm_cap_when_the_data_root_is_in_a_vm()
    {
        var status = await StatusWithDataRoot(onThisHost: false);

        var disk = Assert.Single(status.Checks, static c => c.Name == "disk");
        Assert.Equal(CheckState.Ok, disk.State);
        Assert.Equal("100.0 GiB free at /sindri-home, needs 16.3 GiB; Docker's data lives in a VM here; its disk image size is capped separately (Docker Desktop: Settings → Resources)", disk.Detail);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_is_not_set_up_with_the_pins_version()
    {
        var status = await Status(100 * Gib);

        Assert.Equal(OnyxState.NotSetUp, status.State);
        Assert.Equal("v4.9.0", status.PinnedVersion);
        Assert.Equal(40, status.PinnedCommit.Length);
        Assert.Equal(0, status.ExitCode);
        Assert.Equal(3000, status.Port);
        Assert.Equal(["docker", "compose", "memory", "disk", "port"], status.Checks.Select(static c => c.Name), StringComparer.Ordinal);
        Assert.All(status.Checks, static c => Assert.Equal(CheckState.Ok, c.State));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_exit_1_when_disk_is_short()
    {
        var status = await Status(11 * Gib);

        Assert.Equal(1, status.ExitCode);
        Assert.Equal("SND-ONYX-003", status.Checks.Single(static c => c.Name == "disk").Problem?.Code);
    }
}
