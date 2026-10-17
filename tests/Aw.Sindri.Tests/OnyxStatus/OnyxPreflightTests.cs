using Aw.Sindri.Core;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxStatus;

public sealed class OnyxPreflightTests
{
    private const long Gib = 1L << 30;
    private const string VmNote = "; Docker's data lives in a VM here; its disk image size is capped separately (Docker Desktop: Settings → Resources)";

    private static readonly OnyxPins _pins = new(
        "v4.9.0", new string('0', 40), [], [], DiskBytes: 30 * Gib, DockerMemoryBytes: 5_905_580_032);   // the lock's floor: 5.5 GiB of MemTotal for a 6 GiB VM

    private static readonly DiskFacts _roomy = new("/h", 80 * Gib);
    private static readonly PortFacts _free = new(3000, PreferredFree: true, FirstFreeFallback: 3001);

    private static PreflightCheck Check(PreflightReport report, string name) => Assert.Single(report.Checks, c => c.Name == name);

    [Fact, Trait("Lane", "unit")]
    public void Evaluate_all_ok_returns_port_3000()
    {
        var report = OnyxPreflight.Evaluate(FakeContainerRuntime.Healthy, new HostFacts(_roomy, DataRoot: null, _free), _pins);

        Assert.True(report.Ok);
        Assert.Equal(3000, report.Port);
        Assert.Equal(["docker", "compose", "memory", "disk", "port"], report.Checks.Select(static c => c.Name), StringComparer.Ordinal);
        Assert.Equal("29.4.0", Check(report, "docker").Detail);
        Assert.Equal("v2.40.3", Check(report, "compose").Detail);
        Assert.Equal("15.7 GiB for Docker, needs a 6 GiB setting", Check(report, "memory").Detail);
        Assert.Equal("80.0 GiB free at /h, needs 30.0 GiB" + VmNote, Check(report, "disk").Detail);
        Assert.Equal("3000 free", Check(report, "port").Detail);
    }

    [Fact, Trait("Lane", "unit")]
    public void Evaluate_fails_001_with_an_install_fix_when_the_cli_is_missing()
    {
        var report = OnyxPreflight.Evaluate(FakeContainerRuntime.NoCli, new HostFacts(_roomy, DataRoot: null, _free), _pins);

        var docker = Check(report, "docker");
        Assert.Equal(CheckState.Fail, docker.State);
        Assert.Equal("SND-ONYX-001", docker.Problem?.Code);
        Assert.Contains("install Docker", docker.Problem?.Message, StringComparison.Ordinal);
        Assert.False(report.Ok);
    }

    [Fact, Trait("Lane", "unit")]
    public void Evaluate_fails_001_with_a_start_fix_when_the_daemon_is_not_answering()
    {
        // Docker installed, Docker Desktop not started: the fix says start it, not install it.
        var report = OnyxPreflight.Evaluate(FakeContainerRuntime.DaemonDown, new HostFacts(_roomy, DataRoot: null, _free), _pins);

        var docker = Check(report, "docker");
        Assert.Equal("SND-ONYX-001", docker.Problem?.Code);
        Assert.Contains("start Docker Desktop", docker.Problem?.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("install", docker.Problem?.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(CheckState.Warn, Check(report, "compose").State);
        Assert.Equal("needs docker", Check(report, "compose").Detail);
        Assert.Equal(CheckState.Warn, Check(report, "memory").State);
    }

    [Fact, Trait("Lane", "unit")]
    public void Evaluate_fails_001_with_a_group_fix_when_the_socket_is_permission_denied()
    {
        var report = OnyxPreflight.Evaluate(FakeContainerRuntime.PermissionDenied, new HostFacts(_roomy, DataRoot: null, _free), _pins);

        var docker = Check(report, "docker");
        Assert.Equal(CheckState.Fail, docker.State);
        Assert.Equal("SND-ONYX-001", docker.Problem?.Code);
        Assert.Equal("docker CLI found, permission denied on the daemon socket", docker.Detail);
        Assert.Contains("add your user to the docker group (`sudo usermod -aG docker $USER`, then log out and in), or use Docker Desktop", docker.Problem?.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("isn't running", docker.Problem?.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Evaluate_fails_001_with_a_wait_fix_when_the_daemon_did_not_answer_in_time()
    {
        var report = OnyxPreflight.Evaluate(FakeContainerRuntime.Starting, new HostFacts(_roomy, DataRoot: null, _free), _pins);

        var docker = Check(report, "docker");
        Assert.Equal(CheckState.Fail, docker.State);
        Assert.Equal("SND-ONYX-001", docker.Problem?.Code);
        Assert.Equal("Docker didn't answer within 25 s; it may be starting (Docker Desktop's Resource Saver)", docker.Detail);
        Assert.Contains("wait", docker.Problem?.Message, StringComparison.Ordinal);
        Assert.Contains("run this again", docker.Problem?.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("isn't running", docker.Problem?.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Evaluate_fails_001_with_a_context_fix_when_the_docker_context_does_not_exist()
    {
        var report = OnyxPreflight.Evaluate(FakeContainerRuntime.ContextMissing, new HostFacts(_roomy, DataRoot: null, _free), _pins);

        var docker = Check(report, "docker");
        Assert.Equal("SND-ONYX-001", docker.Problem?.Code);
        Assert.Equal("docker CLI found, the selected docker context doesn't exist", docker.Detail);
        Assert.Contains("docker context use default", docker.Problem?.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("isn't running", docker.Problem?.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Evaluate_fails_002_when_only_compose_v1_is_present()
    {
        var report = OnyxPreflight.Evaluate(FakeContainerRuntime.ComposeV1Only, new HostFacts(_roomy, DataRoot: null, _free), _pins);

        var compose = Check(report, "compose");
        Assert.Equal(CheckState.Fail, compose.State);
        Assert.Equal("SND-ONYX-002", compose.Problem?.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Evaluate_fails_003_when_docker_memory_is_short()
    {
        var docker = FakeContainerRuntime.Healthy with { MemoryBytes = 4 * Gib };

        var memory = Check(OnyxPreflight.Evaluate(docker, new HostFacts(_roomy, DataRoot: null, _free), _pins), "memory");

        Assert.Equal(CheckState.Fail, memory.State);
        Assert.Equal("4.0 GiB for Docker, needs a 6 GiB setting", memory.Detail);
        Assert.Equal("SND-ONYX-003", memory.Problem?.Code);
        Assert.Contains("set Docker's memory to at least 6 GiB in Docker Desktop → Settings → Resources", memory.Problem?.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Evaluate_passes_a_6_GiB_vm_whose_MemTotal_reads_5_8_GiB()
    {
        // Docker Desktop's `docker info` MemTotal sits a few percent below the VM limit the user set.
        var docker = FakeContainerRuntime.Healthy with { MemoryBytes = (long)(5.8 * Gib) };

        var memory = Check(OnyxPreflight.Evaluate(docker, new HostFacts(_roomy, DataRoot: null, _free), _pins), "memory");

        Assert.Equal(CheckState.Ok, memory.State);
    }

    [Fact, Trait("Lane", "unit")]
    public void Evaluate_fails_a_MemTotal_of_5_4_GiB_with_the_6_GiB_fix()
    {
        var docker = FakeContainerRuntime.Healthy with { MemoryBytes = (long)(5.4 * Gib) };

        var memory = Check(OnyxPreflight.Evaluate(docker, new HostFacts(_roomy, DataRoot: null, _free), _pins), "memory");

        Assert.Equal(CheckState.Fail, memory.State);
        Assert.Equal("5.4 GiB for Docker, needs a 6 GiB setting", memory.Detail);
        Assert.Contains("at least 6 GiB in Docker Desktop", memory.Problem?.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Evaluate_warns_when_docker_memory_is_unknown()
    {
        var docker = FakeContainerRuntime.Healthy with { MemoryBytes = null };

        var memory = Check(OnyxPreflight.Evaluate(docker, new HostFacts(_roomy, DataRoot: null, _free), _pins), "memory");

        Assert.Equal(CheckState.Warn, memory.State);
        Assert.Null(memory.Problem);
    }

    [Fact, Trait("Lane", "unit")]
    public void Evaluate_reports_the_smaller_of_the_sindri_home_and_a_data_root_on_this_host()
    {
        var disk = Check(OnyxPreflight.Evaluate(FakeContainerRuntime.NativeEngine, new HostFacts(_roomy, new DiskFacts("/var/lib/docker", 11 * Gib), _free), _pins), "disk");

        Assert.Equal(CheckState.Fail, disk.State);
        Assert.Equal("11.0 GiB free at /var/lib/docker, needs 30.0 GiB", disk.Detail);
        Assert.Equal("SND-ONYX-003", disk.Problem?.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Evaluate_keeps_the_sindri_home_when_it_is_the_smaller()
    {
        var disk = Check(OnyxPreflight.Evaluate(FakeContainerRuntime.NativeEngine, new HostFacts(new DiskFacts("/h", 40 * Gib), new DiskFacts("/var/lib/docker", 90 * Gib), _free), _pins), "disk");

        Assert.Equal(CheckState.Ok, disk.State);
        Assert.Equal("40.0 GiB free at /h, needs 30.0 GiB", disk.Detail);
    }

    [Fact, Trait("Lane", "unit")]
    public void Evaluate_notes_the_vm_disk_image_cap_when_the_data_root_is_not_on_this_host()
    {
        var ok = Check(OnyxPreflight.Evaluate(FakeContainerRuntime.Healthy, new HostFacts(_roomy, DataRoot: null, _free), _pins), "disk");
        var fail = Check(OnyxPreflight.Evaluate(FakeContainerRuntime.Healthy, new HostFacts(new DiskFacts("/h", 11 * Gib), DataRoot: null, _free), _pins), "disk");

        Assert.Equal("80.0 GiB free at /h, needs 30.0 GiB" + VmNote, ok.Detail);
        Assert.Equal("11.0 GiB free at /h, needs 30.0 GiB" + VmNote, fail.Detail);
        Assert.Equal("SND-ONYX-003", fail.Problem?.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Evaluate_notes_the_vm_even_when_the_data_root_is_unknown()
    {
        var docker = FakeContainerRuntime.Healthy with { DataRoot = null };

        var disk = Check(OnyxPreflight.Evaluate(docker, new HostFacts(_roomy, DataRoot: null, _free), _pins), "disk");

        Assert.Equal("80.0 GiB free at /h, needs 30.0 GiB" + VmNote, disk.Detail);
    }

    [Fact, Trait("Lane", "unit")]
    public void Evaluate_adds_no_note_for_a_native_engine()
    {
        var disk = Check(OnyxPreflight.Evaluate(FakeContainerRuntime.NativeEngine, new HostFacts(_roomy, DataRoot: null, _free), _pins), "disk");

        Assert.Equal("80.0 GiB free at /h, needs 30.0 GiB", disk.Detail);
    }

    [Fact, Trait("Lane", "unit")]
    public void Evaluate_ignores_a_host_directory_of_the_data_root_name_when_docker_runs_in_a_vm()
    {
        // Docker Desktop's DockerRootDir is /var/lib/docker inside its VM; a /var/lib/docker on this host (an old install) is not that disk.
        var disk = Check(OnyxPreflight.Evaluate(FakeContainerRuntime.Healthy, new HostFacts(_roomy, new DiskFacts("/var/lib/docker", 11 * Gib), _free), _pins), "disk");

        Assert.Equal(CheckState.Ok, disk.State);
        Assert.Equal("80.0 GiB free at /h, needs 30.0 GiB" + VmNote, disk.Detail);
    }

    [Fact, Trait("Lane", "unit")]
    public void Evaluate_shows_an_unreadable_version_for_a_runtime_that_answers_without_one()
    {
        var docker = FakeContainerRuntime.Healthy with { ServerVersion = null, ComposeVersion = null };

        var report = OnyxPreflight.Evaluate(docker, new HostFacts(_roomy, DataRoot: null, _free), _pins);

        Assert.Equal("unreadable version", Check(report, "docker").Detail);
        Assert.Equal("unreadable version", Check(report, "compose").Detail);
        Assert.Equal(CheckState.Ok, Check(report, "docker").State);
    }

    [Fact, Trait("Lane", "unit")]
    public void Evaluate_fails_003_when_disk_is_short()
    {
        var disk = Check(OnyxPreflight.Evaluate(FakeContainerRuntime.Healthy, new HostFacts(new DiskFacts("/h", 11 * Gib), DataRoot: null, _free), _pins), "disk");

        Assert.Equal(CheckState.Fail, disk.State);
        Assert.Equal("11.0 GiB free at /h, needs 30.0 GiB" + VmNote, disk.Detail);
        Assert.Equal("SND-ONYX-003", disk.Problem?.Code);
        Assert.Contains("11.0 GiB free at /h, needs 30.0 GiB" + VmNote, disk.Problem?.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Evaluate_uses_the_first_free_fallback()
    {
        var report = OnyxPreflight.Evaluate(FakeContainerRuntime.Healthy, new HostFacts(_roomy, DataRoot: null, new PortFacts(3000, PreferredFree: false, FirstFreeFallback: 3002)), _pins);

        var port = Check(report, "port");
        Assert.Equal(CheckState.Ok, port.State);
        Assert.Equal("3000 taken; would use 3002", port.Detail);
        Assert.Equal(3002, report.Port);
    }

    [Fact, Trait("Lane", "unit")]
    public void Evaluate_fails_006_when_no_port_is_free()
    {
        var report = OnyxPreflight.Evaluate(FakeContainerRuntime.Healthy, new HostFacts(_roomy, DataRoot: null, new PortFacts(3000, PreferredFree: false, FirstFreeFallback: null)), _pins);

        var port = Check(report, "port");
        Assert.Equal(CheckState.Fail, port.State);
        Assert.Equal("SND-ONYX-006", port.Problem?.Code);
        Assert.Equal(3000, report.Port);
        Assert.False(report.Ok);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_catalogue_lists_every_onyx_code_the_preflight_can_return()
    {
        var codes = SindriErrors.All.Select(static e => e.Code).Where(static c => c.StartsWith("SND-ONYX-", StringComparison.Ordinal)).Order(StringComparer.Ordinal);

        Assert.Equal(["SND-ONYX-001", "SND-ONYX-002", "SND-ONYX-003", "SND-ONYX-004", "SND-ONYX-006"], codes, StringComparer.Ordinal);
    }
}
