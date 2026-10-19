using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Infrastructure.Sandbox;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>The probe names what is missing with the -014 reasons of the spec (§6), cheapest check first, and says nothing when the box can sandbox.</summary>
[Lane(TestLane.Unit)]
public sealed class BubblewrapProbeTests
{
    private const string AllControllers = "cpuset cpu io memory hugetlb pids rdma misc\n";

    private static BubblewrapOptions Options { get; } = new("bwrap", "systemd-run", "socat", "git", TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(20));

    private static FakeProcesses Box(bool bwrap = true, bool userNamespace = true, bool scope = true, bool limits = true, bool socat = true)
    {
        var processes = new FakeProcesses();
        if (bwrap)
        {
            _ = processes.When((p, a) => p == "bwrap" && a.SequenceEqual(["--version"], StringComparer.Ordinal), FakeProcesses.Ok("bubblewrap 0.9.0\n"));
            _ = processes.When((p, a) => p == "bwrap" && a.Contains("--unshare-user", StringComparer.Ordinal), userNamespace ? FakeProcesses.Ok() : FakeProcesses.Failed("bwrap: No permissions to creating new namespace\nsecond line"));
        }

        _ = processes.When((p, a) => p == "systemd-run" && a.Contains("MemoryMax=64M", StringComparer.Ordinal), limits ? FakeProcesses.Ok() : FakeProcesses.Failed("Failed to set unit properties: MemoryMax is not available"));
        _ = processes.When((p, _) => p == "systemd-run", scope ? FakeProcesses.Ok() : FakeProcesses.Failed("Failed to connect to bus"));
        _ = processes.When((p, _) => p == "socat", socat ? FakeProcesses.Ok() : FakeProcesses.Failed("nope"));
        return processes;
    }

    private static BubblewrapSandbox Sandbox(FakeProcesses processes, FakeHostPaths? host = null) =>
        new(Options, processes, host ?? new FakeHostPaths(), new LaunchSecretRegistry(), new FakeTimeProvider());

    private static string? Reason(FakeProcesses processes, FakeHostPaths? host = null)
    {
        var result = Sandbox(processes, host).Probe();
        return result.IsError ? $"{result.Error.Code}:{SandboxRefusal.ReasonOf(result.Error)}" : null;
    }

    [Fact]
    public void A_box_that_can_sandbox_reports_the_bubblewrap_mechanism_and_its_version()
    {
        var result = Sandbox(Box()).Probe();

        Assert.Equal(SandboxMechanism.Bubblewrap, result.Value.Mechanism);
        Assert.Equal("bubblewrap 0.9.0", result.Value.Detail);
    }

    [Fact]
    public void No_bwrap_is_014_no_bwrap() => Assert.Equal("SND-LAUNCH-014:no-bwrap", Reason(Box(bwrap: false)));

    [Fact]
    public void A_user_namespace_that_cannot_be_made_is_014_with_its_first_line_of_stderr()
    {
        var result = Sandbox(Box(userNamespace: false)).Probe();

        Assert.Equal("no-user-namespace", SandboxRefusal.ReasonOf(result.Error));
        Assert.Contains("No permissions to creating new namespace", result.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("second line", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void No_user_scope_is_014_no_user_manager() => Assert.Equal("SND-LAUNCH-014:no-user-manager", Reason(Box(scope: false)));

    [Fact]
    public void No_socat_is_014_no_forwarder() => Assert.Equal("SND-LAUNCH-014:no-forwarder", Reason(Box(socat: false)));

    [Fact]
    public void A_manager_that_accepts_a_plain_scope_but_not_a_memory_and_process_limit_is_014_no_cgroup_controllers()
    {
        var result = Sandbox(Box(limits: false)).Probe();

        Assert.Equal("no-cgroup-controllers", SandboxRefusal.ReasonOf(result.Error));
        Assert.Contains("MemoryMax is not available", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_delegation_list_without_a_needed_controller_is_014_naming_it_and_it_is_read_for_the_real_uid()
    {
        var host = new FakeHostPaths { RealUid = 1234 };
        host.Files["/sys/fs/cgroup/user.slice/user-1234.slice/user@1234.service/cgroup.controllers"] = "cpu io memory\n";

        var result = Sandbox(Box(), host).Probe();

        Assert.Equal("no-cgroup-controllers", SandboxRefusal.ReasonOf(result.Error));
        Assert.Contains("pids", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_full_delegation_list_passes()
    {
        var host = new FakeHostPaths { RealUid = 1234 };
        host.Files["/sys/fs/cgroup/user.slice/user-1234.slice/user@1234.service/cgroup.controllers"] = AllControllers;

        Assert.Null(Reason(Box(), host));
    }

    [Fact]
    public void The_uid_is_the_real_uid_from_proc_self_status_not_the_audit_login_uid() =>
        Assert.Equal(1000, CgroupControllers.UidFrom("Name:\tsindri\nUid:\t1000\t1001\t1002\t1003\nGid:\t1000\n"));

    [Theory]
    [InlineData(null)]
    [InlineData("Name:\tx\n")]
    [InlineData("Uid:\tnot-a-number\n")]
    public void An_unreadable_status_has_no_uid_and_so_claims_nothing(string? status) => Assert.Null(CgroupControllers.UidFrom(status));

    [Fact]
    public void The_programs_come_from_the_redirect_variables_and_none_of_them_switches_the_sandbox_off()
    {
        var env = new Dictionary<string, string>(StringComparer.Ordinal) { [SandboxEnvironment.Bwrap] = "/opt/bwrap", [SandboxEnvironment.SystemdRun] = "/opt/sd", [SandboxEnvironment.Socat] = "/opt/socat" };

        var options = BubblewrapOptions.From(name => env.GetValueOrDefault(name));

        Assert.Equal(("/opt/bwrap", "/opt/sd", "/opt/socat"), (options.Bwrap, options.SystemdRun, options.Socat));
        Assert.Equal("bwrap", BubblewrapOptions.From(static _ => null).Bwrap);
    }
}
