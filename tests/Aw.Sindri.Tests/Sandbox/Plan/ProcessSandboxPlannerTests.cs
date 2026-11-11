using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Resources;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Sandbox.Resources;
using Aw.Sindri.Testing.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>The plan of a plain process such as a toolkit (agent-first PR 16): pure, no bubblewrap.</summary>
public sealed class ProcessSandboxPlannerTests
{
    private const string Sindri = SandboxRequests.SindriHome;
    private const string Command = Sindri + "/toolkits/triage/bin/triage";
    private const string Database = Sindri + "/resources/toolkit/triage/db/state.sqlite";
    private const string Disk = Sindri + "/resources/toolkit/triage/disk/scratch";

    private static ProcessSandboxRequest Request(params PlanGroup[] groups) => new(
        LaunchId.From("LCH-" + new string('c', 32)),
        "toolkit:triage",
        Command,
        SandboxRequests.RunArea,
        [new ResourceMount("state", Database), new ResourceMount("scratch", Disk)],
        new Dictionary<string, string>(StringComparer.Ordinal) { [ResourceEnv.Database("state")] = Database, [ResourceEnv.Disk("scratch")] = Disk },
        groups,
        new SandboxNetFacts("/run/user/1000/sindri/cd34ab12", 7480, 41235),
        SandboxRequests.Host);

    [Fact, Trait("Lane", "unit")]
    public void A_process_plan_mounts_only_declared_resources_and_opens_only_connection_hosts()
    {
        var plan = ProcessSandboxPlanner.Build(SandboxMechanism.Bubblewrap, Request(new PlanGroup("github", ["api.github.com"]))).Value;

        Assert.Equal([Database, Disk], plan.Mounts.Where(m => m.Kind == PlanMountKind.State).Select(m => m.Path), StringComparer.Ordinal);
        Assert.Equal([SandboxRequests.RunArea + "/tmp"], plan.Mounts.Where(m => m.Kind == PlanMountKind.ReadWrite).Select(m => m.Source), StringComparer.Ordinal);   // the only other writable place is its private /tmp
        Assert.Equal([("github", "api.github.com")], plan.Network.Groups.Select(g => (g.Name, g.Hosts.Single())));
        Assert.Equal(NetworkMode.Enforce, plan.Network.Mode);
        Assert.Null(plan.Worktree);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_process_with_no_connection_has_no_host_group()
    {
        var plan = ProcessSandboxPlanner.Build(SandboxMechanism.Bubblewrap, Request()).Value;

        Assert.Empty(plan.Network.Groups);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_binary_is_read_only_above_the_deny_of_Sindri_home_and_Sindri_state_stays_denied()
    {
        var plan = ProcessSandboxPlanner.Build(SandboxMechanism.Bubblewrap, Request()).Value;
        var deny = plan.Mounts.ToList().FindIndex(m => m.Kind == PlanMountKind.Deny && m.Path == Sindri);
        var binary = plan.Mounts.ToList().FindIndex(m => m.Kind == PlanMountKind.ReadOnly && m.Path == Sindri + "/toolkits/triage/bin");

        Assert.True(deny >= 0 && binary > deny);
        Assert.Contains(plan.Mounts, m => m.Kind == PlanMountKind.Deny && m.Path == SandboxRequests.StateDir);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_environment_has_the_resource_variables_no_launch_secret_and_no_secret_shaped_name()
    {
        var plan = ProcessSandboxPlanner.Build(SandboxMechanism.Bubblewrap, Request()).Value;

        Assert.Equal(Database, plan.Environment.Single(e => e.Name == ResourceEnv.Database("state")).Value);
        Assert.All(plan.Environment, e => Assert.NotNull(e.Value));   // a null value is the launcher's secret slot: a process has none
        Assert.Empty(EnvironmentAllowlist.Violations(plan.Environment.Select(e => e.Name)));
        Assert.Equal(GithubCredentials.None, plan.Github);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_same_request_gives_the_same_plan_hash()
    {
        var a = ProcessSandboxPlanner.Build(SandboxMechanism.Bubblewrap, Request()).Value;
        var b = ProcessSandboxPlanner.Build(SandboxMechanism.Bubblewrap, Request()).Value;

        Assert.Equal(12, a.PlanHash.Length);
        Assert.Equal(a.PlanHash, b.PlanHash);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_relative_command_a_command_with_no_parent_folder_or_a_relative_resource_path_is_refused()
    {
        Assert.True(ProcessSandboxPlanner.Build(SandboxMechanism.Bubblewrap, Request() with { Command = "triage" }).IsError);
        Assert.True(ProcessSandboxPlanner.Build(SandboxMechanism.Bubblewrap, Request() with { Command = "/tool" }).IsError);   // no parent folder to bind as the binary's
        Assert.True(ProcessSandboxPlanner.Build(SandboxMechanism.Bubblewrap, Request() with { ResourceMounts = [new ResourceMount("state", "db/state.sqlite")] }).IsError);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_read_only_path_is_a_read_only_mount_above_the_deny_of_Sindri_home_and_changes_the_plan_hash()
    {
        const string Repo = "/work/app";
        var plain = ProcessSandboxPlanner.Build(SandboxMechanism.Bubblewrap, Request()).Value;
        var plan = ProcessSandboxPlanner.Build(SandboxMechanism.Bubblewrap, Request() with { ReadOnlyPaths = [Repo + "/"] }).Value;
        var mounts = plan.Mounts.ToList();

        Assert.Contains(mounts, m => m.Kind == PlanMountKind.ReadOnly && m.Path == Repo);   // the trailing slash is trimmed
        Assert.True(mounts.FindIndex(m => m.Path == Repo) > mounts.FindIndex(m => m.Kind == PlanMountKind.Deny && m.Path == Sindri));
        Assert.DoesNotContain(plain.Mounts, m => m.Path == Repo);
        Assert.False(string.Equals(plain.PlanHash, plan.PlanHash, StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_relative_read_only_path_is_refused() =>
        Assert.True(ProcessSandboxPlanner.Build(SandboxMechanism.Bubblewrap, Request() with { ReadOnlyPaths = ["repo"] }).IsError);

    [Fact, Trait("Lane", "unit")]
    public void Read_only_paths_are_deduplicated_and_an_ancestor_is_mounted_before_its_descendants()
    {
        var plan = ProcessSandboxPlanner.Build(SandboxMechanism.Bubblewrap, Request() with { ReadOnlyPaths = ["/work/app/src", "/work/app/", "/work/app", "/work/app/src/"] }).Value;
        var paths = plan.Mounts.Where(m => m.Kind == PlanMountKind.ReadOnly && m.Path.StartsWith("/work/app", StringComparison.Ordinal)).Select(m => m.Path).ToList();

        Assert.Equal(["/work/app", "/work/app/src"], paths);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_read_only_ancestor_comes_before_a_writable_resource_under_it()
    {
        var request = Request() with { ResourceMounts = [new ResourceMount("state", "/a/b/c")], ReadOnlyPaths = ["/a"] };
        var mounts = ProcessSandboxPlanner.Build(SandboxMechanism.Bubblewrap, request).Value.Mounts.ToList();

        var ancestor = mounts.FindIndex(m => m.Kind == PlanMountKind.ReadOnly && m.Path == "/a");
        var writable = mounts.FindIndex(m => m.Kind == PlanMountKind.State && m.Path == "/a/b/c");
        Assert.True(ancestor >= 0 && writable > ancestor);   // bwrap applies mounts in order: a later read-only /a would shadow /a/b/c
    }

    [Fact, Trait("Lane", "unit")]
    public void A_read_only_path_under_a_writable_resource_is_mounted_after_it()
    {
        var request = Request() with { ResourceMounts = [new ResourceMount("state", "/x/res")], ReadOnlyPaths = ["/x/res/ro"] };
        var mounts = ProcessSandboxPlanner.Build(SandboxMechanism.Bubblewrap, request).Value.Mounts.ToList();

        var writable = mounts.FindIndex(m => m.Kind == PlanMountKind.State && m.Path == "/x/res");
        var readOnly = mounts.FindIndex(m => m.Kind == PlanMountKind.ReadOnly && m.Path == "/x/res/ro");
        Assert.True(writable >= 0 && readOnly > writable);   // the later, deeper mount keeps the path read-only; the other order made it writable again
    }
}
