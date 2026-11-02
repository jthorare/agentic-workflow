using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Tests.Profile;

/// <summary>What the Allow button may put in a repo's 'sandbox.repos' entry (sandbox spec §3.6, §3.7): a valid host, a home-relative path, and whether the approved profile already lists it.</summary>
public sealed class SandboxAllowancesTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("/home/o/.cache/tool", "/home/o", ".cache/tool")]
    [InlineData("/home/o/.cache/tool/", "/home/o/", ".cache/tool")]
    [InlineData("/home/o", "/home/o", null)]
    [InlineData("/home/other/x", "/home/o", null)]
    [InlineData("/home/ox/x", "/home/o", null)]
    [InlineData("/home/o/../etc", "/home/o", null)]
    public void A_path_is_made_relative_to_the_home(string path, string home, string? expected) =>
        Assert.Equal(expected, SandboxAllowances.HomeRelative(path, home));

    [Theory, Trait("Lane", "unit")]
    [InlineData(DenialKind.Network, "registry.npmjs.org", true)]
    [InlineData(DenialKind.Network, "*.nuget.org", true)]
    [InlineData(DenialKind.Network, "https://github.com", false)]
    [InlineData(DenialKind.Network, "github.com:443", false)]
    [InlineData(DenialKind.Path, ".cache/tool", true)]
    [InlineData(DenialKind.Path, "/abs", false)]
    [InlineData(DenialKind.Path, "a/../b", false)]
    [InlineData(DenialKind.Policy, "git push", false)]
    public void An_entry_the_profile_would_refuse_has_a_problem(DenialKind kind, string value, bool valid) =>
        Assert.Equal(valid, SandboxAllowances.Problem(kind, value) is null);

    [Fact, Trait("Lane", "unit")]
    public void Holds_looks_in_the_repos_hosts_and_home_allow()
    {
        var profile = new SindriProfile(1, "o", Sandbox: new SandboxSettings(Repos: new Dictionary<string, SandboxRepoSettings>(StringComparer.Ordinal)
        {
            ["web"] = new(Hosts: ["github.com"], HomeAllow: [new HomeAllowEntry(".cache/tool")]),
            ["api"] = new(),
        }));

        Assert.True(SandboxAllowances.Holds(profile, "web", DenialKind.Network, "github.com"));
        Assert.True(SandboxAllowances.Holds(profile, "web", DenialKind.Path, ".cache/tool"));
        Assert.False(SandboxAllowances.Holds(profile, "web", DenialKind.Network, "npm.org"));
        Assert.False(SandboxAllowances.Holds(profile, "api", DenialKind.Network, "github.com"));
        Assert.False(SandboxAllowances.Holds(profile, "mobile", DenialKind.Path, ".cache/tool"));
        Assert.False(SandboxAllowances.Holds(new SindriProfile(1, "o"), "web", DenialKind.Network, "github.com"));
    }
}
