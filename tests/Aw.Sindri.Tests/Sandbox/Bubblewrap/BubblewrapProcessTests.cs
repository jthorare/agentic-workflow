using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Resources;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Testing.Sandbox;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>The bubblewrap variant for a plain process such as a toolkit (agent-first PR 16): argv assertions through the fake processes.</summary>
[Lane(TestLane.Integration)]
public sealed class BubblewrapProcessTests : IDisposable
{
    private static readonly BubblewrapOptions _options = new("bwrap", "systemd-run", "socat", "git", TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(20));

    private readonly string _root = Path.Combine(AppContext.BaseDirectory, "sandbox-fixtures", "proc-" + RealTime.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static BubblewrapSandbox Sandbox() => new(_options, new FakeProcesses(), new HostPaths(), new LaunchSecretRegistry(), new FakeTimeProvider());

    private SandboxPlan Plan(params PlanGroup[] groups) => ProcessSandboxPlanner.Build(
        SandboxMechanism.Bubblewrap,
        new ProcessSandboxRequest(
            LaunchId.From("LCH-" + new string('d', 32)),
            "toolkit:triage",
            SandboxRequests.SindriHome + "/toolkits/triage/bin/triage",
            _root + "/area",
            [new ResourceMount("state", SandboxRequests.SindriHome + "/resources/toolkit/triage/db/state.sqlite")],
            new Dictionary<string, string>(StringComparer.Ordinal) { [ResourceEnv.Database("state")] = SandboxRequests.SindriHome + "/resources/toolkit/triage/db/state.sqlite" },
            groups,
            new SandboxNetFacts("/run/user/1000/sindri/cd34ab12", 7480, 41235),
            SandboxRequests.Host)).Value;

    [Fact]
    public void Wrap_keeps_stdio_piped_and_adds_no_launch_secret()
    {
        var plan = Plan();

        var argv = Sandbox().WrapProcess(plan, ["/x/triage", "--serve"]).Value;

        Assert.Equal("bwrap", argv[0]);   // no /bin/sh secrets wrapper, no descriptor 3
        Assert.DoesNotContain("--args", argv, StringComparer.Ordinal);
        Assert.DoesNotContain("--new-session", argv, StringComparer.Ordinal);   // a new session would detach the pipes' process group handling
        Assert.Contains("--unshare-net", argv, StringComparer.Ordinal);
        Assert.Equal(["--", "/x/triage", "--serve"], argv.TakeLast(3), StringComparer.Ordinal);   // no init: this process has no host group, so nothing to forward
        Assert.DoesNotContain(argv, a => a.Contains("AW_SINDRI_LAUNCH_SECRET", StringComparison.Ordinal));
    }

    [Fact]
    public void Wrap_binds_the_resource_read_write_and_makes_the_private_tmp()
    {
        var plan = Plan();

        var argv = Sandbox().WrapProcess(plan, ["/x/triage"]).Value;

        var db = SandboxRequests.SindriHome + "/resources/toolkit/triage/db/state.sqlite";
        Assert.Contains(argv.Zip(argv.Skip(1), argv.Skip(2)), t => t.First == "--bind" && t.Second == db && t.Third == db);
        Assert.True(Directory.Exists(_root + "/area/tmp"));
    }

    [Fact]
    public void A_process_with_a_connection_runs_under_the_init_that_starts_the_forwarders()
    {
        var plan = Plan(new PlanGroup("github", ["api.github.com"]));

        var argv = Sandbox().WrapProcess(plan, ["/x/triage"]).Value;

        var start = argv.ToList().IndexOf("--") + 1;
        Assert.Equal(["/bin/sh", "-c", EgressInit.Render(plan.Network, "socat", EgressInit.InsideLog), EgressInit.Name, "/x/triage"], argv.Skip(start), StringComparer.Ordinal);
    }
}
