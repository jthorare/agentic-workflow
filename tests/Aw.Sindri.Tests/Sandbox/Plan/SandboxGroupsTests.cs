using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>The host groups of a 'run_agent' node's sandbox (PR 7): the repo's 'github' group, and the groups of the connections it names.</summary>
[Lane(TestLane.Unit)]
public sealed class SandboxGroupsTests
{
    private static GroupsInput Input(SandboxSettings? settings = null, IReadOnlyList<string>? add = null) =>
        new(settings, Repo: null, AgentId.From("claude-code"), WriteScope: true, Attended: false, GithubCredentials.None, Omit: null) { AddGroups = add };

    private static RunAgentConfig Config(IReadOnlyList<string> connections) =>
        new("web", [], Agent: null, "p", new Dictionary<string, string>(StringComparer.Ordinal), TimeSpan.FromHours(1), connections, Wall: null);

    [Fact]
    public void A_node_with_a_repo_gets_the_github_group_and_a_named_connection_adds_its_hosts()
    {
        var settings = new SandboxSettings(Network: new SandboxNetwork(Groups: new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal) { ["linear"] = ["api.linear.app"] }));
        var groups = RunAgentLaunches.GroupsFor(Config(["linear"]), static name => name == "linear" ? ["linear"] : []);

        var resolved = HostGroups.Resolve(Input(settings, groups));

        Assert.Equal(["github", "linear"], groups);
        Assert.Equal(["github.com", "api.github.com"], resolved.Single(g => g.Name == "github").Hosts);
        Assert.Equal(["api.linear.app"], resolved.Single(g => g.Name == "linear").Hosts);
        Assert.Contains("model", resolved.Select(g => g.Name), StringComparer.Ordinal);
    }

    [Fact]
    public void Until_connections_exist_only_the_repo_group_applies()
    {
        var groups = RunAgentLaunches.GroupsFor(Config(["linear"]), static _ => []);

        Assert.Equal(["github"], groups);
    }

    [Fact]
    public void A_launch_that_adds_no_groups_resolves_as_before_and_a_group_is_never_listed_twice()
    {
        var plain = HostGroups.Resolve(Input());
        var twice = HostGroups.Resolve(new GroupsInput(Settings: null, Repo: null, AgentId.From("claude-code"), WriteScope: true, Attended: false, GithubCredentials.AgentApp, Omit: null) { AddGroups = ["github", "github"] });

        Assert.DoesNotContain("github", plain.Select(g => g.Name), StringComparer.Ordinal);
        _ = Assert.Single(twice, g => g.Name == "github");
    }
}
