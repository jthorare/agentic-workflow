using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Tests.Sandbox.Plan;
using Req = Aw.Sindri.Testing.Sandbox.SandboxRequests;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>A plan whose run directory and run area are real temporary directories, so a test can prepare, wrap and read what was written. Only the root (read-only), /dev, /proc and the run directory are mounted.</summary>
internal sealed class TempRunArea : IDisposable
{
    internal TempRunArea(params string[] launcherVariables)
    {
        Root = Path.Combine(AppContext.BaseDirectory, "sandbox-fixtures", "area-" + RealTime.NewGuid().ToString("N")[..8]);   // not /tmp: a sandbox replaces it
        Directory.CreateDirectory(Root + "/run");
        Directory.CreateDirectory(Root + "/area");
        var built = SandboxPlannerTests.Build(Req.WriteScope);
        Plan = built with
        {
            RunDirectory = Root + "/run",
            Worktree = null,
            Mounts = [.. built.Mounts.Where(m => m.Kind is PlanMountKind.Dev or PlanMountKind.Proc || m.Path == "/"), new PlanMount(Root + "/run", PlanMountKind.ReadWrite, Reason: "test")],   // the agent's working directory
            Environment = [new PlanEnvVar("AW_SANDBOX_PLAN", Root + "/area/sandbox-plan.json", "test"), .. launcherVariables.Select(n => new PlanEnvVar(n, Value: null, Reason: "test"))],
        };
    }

    internal string Root { get; }

    internal SandboxPlan Plan { get; }

    public void Dispose() => Directory.Delete(Root, recursive: true);
}
