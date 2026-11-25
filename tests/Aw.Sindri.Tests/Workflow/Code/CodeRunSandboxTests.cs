using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Infrastructure.Workflow.Code;
using Aw.Sindri.Testing.Sandbox;

namespace Aw.Sindri.Tests.Workflow.Code;

/// <summary>What the Code runner's sandbox allows (agent-first PR 9c), asserted on the plan: no network, writes only to its private /tmp, the repo read-only.</summary>
[Lane(TestLane.Unit)]
public sealed class CodeRunSandboxTests
{
    private const string Repo = "/work/app";
    private const string Runner = SandboxRequests.SindriHome + "/bin/cs/abc/sindri-code-runner";

    private static SandboxPlan Plan(string? repo)
    {
        var request = CodeRunSandbox.Request(LaunchId.From("LCH-" + new string('e', 32)), Runner, SandboxRequests.RunArea, repo, SandboxRequests.Host);
        return ProcessSandboxPlanner.Build(SandboxMechanism.Bubblewrap, request).Value;
    }

    [Fact]
    public void Network_and_writes_outside_the_mounts_are_refused()
    {
        var plan = Plan(Repo);

        Assert.Empty(plan.Network.Groups);   // no host group: an empty network namespace
        Assert.Equal(NetworkMode.Enforce, plan.Network.Mode);
        Assert.Equal([SandboxRequests.RunArea + "/tmp"], plan.Mounts.Where(static m => m.Kind is PlanMountKind.ReadWrite or PlanMountKind.State or PlanMountKind.PerLaunchDir).Select(static m => m.Source), StringComparer.Ordinal);
        Assert.Contains(plan.Mounts, static m => m.Kind == PlanMountKind.ReadOnly && m.Path == Repo);
        Assert.Contains(plan.Mounts, static m => m.Kind == PlanMountKind.Deny && m.Path == SandboxRequests.StateDir);
    }

    [Fact]
    public void Without_a_repo_nothing_but_the_runner_and_the_system_is_readable()
    {
        var plan = Plan(repo: null);

        Assert.DoesNotContain(plan.Mounts, static m => m.Path == Repo);
    }
}
