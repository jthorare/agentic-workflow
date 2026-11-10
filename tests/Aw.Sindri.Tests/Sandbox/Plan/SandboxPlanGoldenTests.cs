using Aw.Kernel.Testing;
using Aw.Sindri.Core.Resources;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Infrastructure.Sandbox;
using Req = Aw.Sindri.Testing.Sandbox.SandboxRequests;
using Aw.Sindri.Core.Sandbox.Resources;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>A resource's state mounts as bubblewrap and Seatbelt render them (PR 14): read-write at their own path, above the home and below the owner's denies.</summary>
public sealed class SandboxPlanGoldenTests
{
    private sealed class MissingHost : IHostPaths
    {
        public int? Uid() => 1000;

        public string? ReadText(string path) => null;

        public HostPathKind KindOf(string path) => HostPathKind.Missing;

        public string Resolve(string path) => path;
    }

    // the planner's own mounts for a workflow with a database and a disk: one read-write mount of the owner's folder
    private static readonly IReadOnlyList<ResourceMount> _resources = ResourcePlanner.Plan(
        ResourceOwner.Parse("workflow:web").Value,
        new ResourceSpec([new DatabaseResource("notes")], [new DiskResource("scratch", "1GiB")], Sandbox: null, []),
        Req.SindriHome + "/resources").Value.Mounts;

    private static SandboxPlan Plan(SandboxMechanism mechanism)
    {
        var result = SandboxPlanner.Build(mechanism, Req.WriteScope with { ResourceMounts = _resources });
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }

    private static void Match(string text, string golden) => Golden.Match(text, typeof(SandboxPlanGoldenTests).Assembly, "Sandbox/Plan/Golden/" + golden, Normalization.None);

    [Fact, Trait("Lane", "unit")]
    public void State_mounts_bind_read_write_in_bubblewrap_argv() =>
        Match(string.Join('\n', BubblewrapArgv.Build(Plan(SandboxMechanism.Bubblewrap), new MissingHost(), secretsFd: false)) + "\n", "state-bubblewrap.txt");

    [Fact, Trait("Lane", "unit")]
    public void State_mounts_are_write_allowances_in_the_seatbelt_profile() =>
        Match(SeatbeltProfile.Render(Plan(SandboxMechanism.Seatbelt), Req.RunArea, canonical: null), "state-seatbelt.sb");

    [Fact, Trait("Lane", "unit")]
    public void State_mounts_are_in_the_plans_writable_set()
    {
        var plan = Plan(SandboxMechanism.Bubblewrap);

        Assert.Equal(_resources.Select(r => r.HostPath), plan.Mounts.Where(m => m.Kind == PlanMountKind.State).Select(m => m.Path), StringComparer.Ordinal);
        Assert.All(_resources, r => Assert.Contains(r.HostPath, plan.Writable, StringComparer.Ordinal));
        Assert.Equal([Req.SindriHome + "/resources/workflow/web"], _resources.Select(r => r.HostPath), StringComparer.Ordinal);   // the folder, so notes.db-wal and -shm are writable too
    }
}
