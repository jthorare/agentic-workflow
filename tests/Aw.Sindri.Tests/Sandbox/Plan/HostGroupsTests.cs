using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>Host groups (sandbox spec §3.2).</summary>
public sealed class HostGroupsTests
{
    private static readonly AgentId _claude = AgentId.From("claude-code");
    private static readonly AgentId _codex = AgentId.From("codex");

    private static GroupsInput Input(AgentId? target = null, bool write = true, bool attended = false, GithubCredentials github = GithubCredentials.None, SandboxSettings? settings = null, SandboxRepoSettings? repo = null, IReadOnlyList<string>? omit = null) =>
        new(settings, repo, target ?? _claude, write, attended, github, omit);

    private static IReadOnlyList<string> Names(IEnumerable<PlanGroup> groups) => [.. groups.Select(g => g.Name)];

    [Fact, Trait("Lane", "unit")]
    public void Model_group_follows_the_target()
    {
        Assert.Equal(["api.anthropic.com"], HostGroups.Resolve(Input()).Single(g => g.Name == "model").Hosts);
        Assert.Equal(["api.openai.com", "chatgpt.com"], HostGroups.Resolve(Input(_codex)).Single(g => g.Name == "model").Hosts);
    }

    [Fact, Trait("Lane", "unit")]
    public void Packages_group_only_for_write_scope()
    {
        Assert.Equal(["model", "packages"], Names(HostGroups.Resolve(Input(write: true))));
        Assert.Equal(["model"], Names(HostGroups.Resolve(Input(write: false))));
        Assert.Equal(["api.nuget.org", "*.nuget.org", "registry.npmjs.org"], HostGroups.Resolve(Input()).Single(g => g.Name == "packages").Hosts);
    }

    [Fact, Trait("Lane", "unit")]
    public void Github_group_only_with_agent_app_and_never_attended()
    {
        Assert.DoesNotContain("github", Names(HostGroups.Resolve(Input(github: GithubCredentials.None))), StringComparer.Ordinal);
        Assert.Contains("github", Names(HostGroups.Resolve(Input(github: GithubCredentials.AgentApp))), StringComparer.Ordinal);
        Assert.DoesNotContain("github", Names(HostGroups.Resolve(Input(github: GithubCredentials.AgentApp, attended: true))), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_profile_group_replaces_the_built_in_hosts_of_that_name()
    {
        var settings = new SandboxSettings(Network: new SandboxNetwork(Groups: new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal) { ["packages"] = ["registry.npmjs.org"] }));

        Assert.Equal(["registry.npmjs.org"], HostGroups.Resolve(Input(settings: settings)).Single(g => g.Name == "packages").Hosts);
    }

    [Fact, Trait("Lane", "unit")]
    public void Repo_groups_and_hosts_extend_the_session()
    {
        var repo = new SandboxRepoSettings(Groups: ["signing"], Hosts: ["pypi.org"]);

        var groups = HostGroups.Resolve(Input(repo: repo));

        Assert.Contains("signing", Names(groups), StringComparer.Ordinal);
        Assert.Contains("pypi.org", groups.Single(g => g.Name == "repo").Hosts, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_step_may_remove_a_group_from_itself() => Assert.Equal(["model"], Names(HostGroups.Resolve(Input(omit: ["packages"]))));

    [Theory, Trait("Lane", "unit")]
    [InlineData("api.nuget.org", "api.nuget.org", true)]
    [InlineData("*.nuget.org", "globalcdn.nuget.org", true)]
    [InlineData("*.nuget.org", "nuget.org", false)]
    [InlineData("*.nuget.org", "evilnuget.org", false)]
    [InlineData("api.nuget.org", "API.NUGET.ORG", true)]
    public void Host_pattern_matches_wildcards(string pattern, string host, bool expected) => Assert.Equal(expected, HostGroups.Matches(pattern, host));

    [Fact, Trait("Lane", "unit")]
    public void Allowing_names_the_group_or_returns_null()
    {
        var groups = HostGroups.Resolve(Input());

        Assert.Equal("model", HostGroups.Allowing(groups, "api.anthropic.com")?.Name);
        Assert.Null(HostGroups.Allowing(groups, "evil.example"));
    }
}
