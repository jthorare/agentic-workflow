using Aw.Kernel.Json;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Host;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.ListSessions;
using Aw.Sindri.Tests.Sandbox.Plan;
using Req = Aw.Sindri.Testing.Sandbox.SandboxRequests;

namespace Aw.Sindri.Tests.Sandbox;

/// <summary>What a session row says about its sandbox, read from the launch's stored plan and its cgroup scope (a temp directory stands in for /sys/fs/cgroup).</summary>
public sealed class DiskSessionSandboxesTests : IDisposable
{
    private const string Slice = "sindri.slice/sindri-steps.slice";
    private const string Launch = "LCH-00000000000000000000000000000001";
    private static readonly System.Text.Json.JsonSerializerOptions _json = AwJson.CreateOptions(SindriJsonContext.Default);

    private readonly TempHome _home = new();
    private readonly string _cgroups = Directory.CreateTempSubdirectory("aw-cgroup-").FullName;

    public void Dispose()
    {
        _home.Dispose();
        Directory.Delete(_cgroups, recursive: true);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string LaunchDirectory => Path.Combine(_home.Path, "launch");   // the launcher's directory, where the writers put the plan

    private DiskSessionSandboxes Reader() =>
        new(LaunchDirectory, _json, new CgroupFiles(_cgroups), Slice);

    private void Plan(SandboxPlan plan)
    {
        var file = LaunchPaths.PlanFile(LaunchDirectory, LaunchId.From(Launch));
        _ = Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllBytes(file, AwJson.Serialize(plan, _json));
    }

    private void Scope(long memoryCurrent)
    {
        var scope = Directory.CreateDirectory(Path.Combine(_cgroups, Slice, $"sindri-{Launch}.scope")).FullName;
        File.WriteAllText(Path.Combine(scope, "memory.events"), "low 0\nhigh 0\nmax 0\noom 0\noom_kill 0\n");
        File.WriteAllText(Path.Combine(scope, "memory.current"), memoryCurrent.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n");
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_readable_plan_and_a_live_scope_give_the_folder_the_limit_and_the_memory_in_use()
    {
        var plan = SandboxPlannerTests.Build(Req.WriteScope);
        Plan(plan);
        Scope(memoryCurrent: 2_254_857_830);

        var sandbox = await Reader().Read(LaunchId.From(Launch), plan.ToSummary(), Token);

        Assert.Equal(new SessionSandbox(SessionSandboxState.On, Attended: false, Req.Worktree, new SessionMemory(2_254_857_830, plan.Limits.MemoryMaxBytes)), sandbox);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_attended_plan_is_served_as_attended()
    {
        var plan = SandboxPlannerTests.Build(Req.WriteScope) with { Attended = true };
        Plan(plan);

        var sandbox = await Reader().Read(LaunchId.From(Launch), plan.ToSummary(), Token);

        Assert.True(sandbox.Attended);
        Assert.Equal(SessionSandboxState.On, sandbox.State);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_scope_that_is_gone_has_no_memory_in_use_but_keeps_the_limit()
    {
        var plan = SandboxPlannerTests.Build(Req.WriteScope);
        Plan(plan);

        var sandbox = await Reader().Read(LaunchId.From(Launch), plan.ToSummary(), Token);

        Assert.Equal(new SessionMemory(UsedBytes: null, plan.Limits.MemoryMaxBytes), sandbox.Memory);
        Assert.Equal(SessionSandboxState.On, sandbox.State);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_missing_plan_file_is_unavailable_and_keeps_attended_from_the_summary()
    {
        var summary = SandboxPlannerTests.Build(Req.WriteScope).ToSummary() with { Attended = true };

        var sandbox = await Reader().Read(LaunchId.From(Launch), summary, Token);

        Assert.Equal(new SessionSandbox(SessionSandboxState.Unavailable, Attended: true, Folder: null, Memory: null), sandbox);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_plan_file_that_does_not_parse_is_unavailable()
    {
        var file = LaunchPaths.PlanFile(LaunchDirectory, LaunchId.From(Launch));
        _ = Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        await File.WriteAllTextAsync(file, "{ not a plan", Token);

        var sandbox = await Reader().Read(LaunchId.From(Launch), SandboxData.Summary(), Token);

        Assert.Equal(new SessionSandbox(SessionSandboxState.Unavailable, Attended: false, Folder: null, Memory: null), sandbox);
    }
}
