using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Resources;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Testing.Sandbox;
using Aw.Sindri.Tests.Sandbox.Bubblewrap;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Sandbox.Seatbelt;

/// <summary>The Seatbelt variant for a plain process such as a toolkit (agent-first PR 16, #2489) on Linux with fakes: the wrapped command and the profile file.</summary>
[Lane(TestLane.Integration)]
public sealed class SeatbeltProcessTests : IDisposable
{
    private static readonly SeatbeltOptions _options = new("sandbox-exec", "git", TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(20));

    private readonly string _root = Path.Combine(AppContext.BaseDirectory, "sandbox-fixtures", "sproc-" + RealTime.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private SeatbeltSandbox Sandbox() => new(_options, new FakeProcesses(), new LaunchSecretRegistry(), new FakeTimeProvider(), _ => 51234, _ => _root + "/short");

    private SandboxPlan Plan(params PlanGroup[] groups)
    {
        var result = Sandbox().PlanProcess(new ProcessSandboxRequest(
            LaunchId.From("LCH-" + new string('d', 32)),
            "toolkit:triage",
            SandboxRequests.SindriHome + "/toolkits/triage/bin/triage",
            _root + "/area",
            [new ResourceMount("state", SandboxRequests.SindriHome + "/resources/toolkit/triage/db/state.sqlite")],
            new Dictionary<string, string>(StringComparer.Ordinal) { [ResourceEnv.Database("state")] = SandboxRequests.SindriHome + "/resources/toolkit/triage/db/state.sqlite" },
            groups,
            new SandboxNetFacts("/run/user/1000/sindri/cd34ab12", 7480, 41235),
            SandboxRequests.Host));
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }

    [Fact]
    public void Wrap_runs_sandbox_exec_on_the_profile_keeps_stdio_piped_and_adds_no_launch_secret()
    {
        var plan = Plan(new PlanGroup("github", ["api.github.com"]));

        var argv = Sandbox().WrapProcess(plan, ["/x/triage", "--serve"]).Value;

        var profile = _root + "/area/seatbelt.sb";
        Assert.Equal(["sandbox-exec", "-f", profile, "--", "/x/triage", "--serve"], argv.TakeLast(6), StringComparer.Ordinal);   // no /bin/sh wrapper: the pipes are the caller's
        Assert.Equal(["/usr/bin/env", "-i"], argv.Take(2), StringComparer.Ordinal);
        Assert.True(File.Exists(profile));
        Assert.DoesNotContain(argv, a => a.Contains("AW_SINDRI_LAUNCH_SECRET", StringComparison.Ordinal) || a.Contains("GH_TOKEN", StringComparison.Ordinal) || a.Contains("GITHUB_TOKEN", StringComparison.Ordinal));
        Assert.Contains(ResourceEnv.Database("state") + "=" + SandboxRequests.SindriHome + "/resources/toolkit/triage/db/state.sqlite", argv, StringComparer.Ordinal);
        Assert.DoesNotContain(File.ReadAllText(profile), "AW_SINDRI_LAUNCH_SECRET", StringComparison.Ordinal);
    }

    [Fact]
    public void Wrap_makes_the_private_tmp_and_points_home_and_tmpdir_into_it()
    {
        var argv = Sandbox().WrapProcess(Plan(), ["/x/triage"]).Value;

        Assert.True(Directory.Exists(_root + "/area/tmp"));
        Assert.Contains("TMPDIR=" + _root + "/area/tmp", argv, StringComparer.Ordinal);
        Assert.Contains("HOME=" + _root + "/area/tmp/home", argv, StringComparer.Ordinal);
    }

    [Fact]
    public void A_process_is_planned_and_wrapped_where_the_agent_path_would_refuse_nothing()
    {
        var plan = Plan();

        Assert.Equal(SandboxMechanism.Seatbelt, plan.Mechanism);
        Assert.True(Sandbox().WrapProcess(plan, ["/x/triage"]).IsSuccess);
    }
}
