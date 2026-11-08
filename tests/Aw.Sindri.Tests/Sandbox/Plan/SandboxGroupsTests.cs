using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>The host groups of a 'run_agent' node (issue 2258, PR 7).</summary>
public sealed class SandboxGroupsTests
{
    private static readonly AgentId _claude = AgentId.From("claude-code");

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _presets =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal) { ["linear"] = ["api.linear.app"] };

    private static GroupsInput Node(IReadOnlyList<string>? connections = null, string? repo = "client-repo") =>
        new(Settings: null, Repo: null, _claude, WriteScope: false, Attended: false, GithubCredentials.None, Omit: null, NodeRepo: repo, Connections: connections, ConnectionHosts: _presets);

    [Fact, Trait("Lane", "unit")]
    public void A_node_with_a_repo_gets_the_github_group_and_a_named_connection_adds_its_hosts()
    {
        var plain = HostGroups.Resolve(Node());
        var withConnection = HostGroups.Resolve(Node(["linear"]));
        var noRepo = HostGroups.Resolve(Node(repo: null));

        Assert.Equal(["github.com", "api.github.com"], plain.Single(g => g.Name == "github").Hosts);   // the repo opens github whatever the launch's credentials
        Assert.DoesNotContain(plain, g => g.Name.StartsWith("connection", StringComparison.Ordinal));
        Assert.Equal(["api.linear.app"], withConnection.Single(g => g.Name == "connection-linear").Hosts);
        Assert.Contains(withConnection, g => g.Name == "github");
        Assert.DoesNotContain(noRepo, g => g.Name == "github");
    }

    [Fact, Trait("Lane", "unit")]
    public void A_connection_with_no_preset_adds_nothing_until_connections_ship()
    {
        var before = HostGroups.Resolve(Node());

        var after = HostGroups.Resolve(Node(["jira"]));

        Assert.Equal(before.Select(g => g.Name), after.Select(g => g.Name), StringComparer.Ordinal);
        Assert.DoesNotContain(after, g => g.Name == "connection-jira");
    }
}
