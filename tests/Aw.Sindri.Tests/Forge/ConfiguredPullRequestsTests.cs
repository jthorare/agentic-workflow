using Aw.Kernel.Testing;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Host.Forge;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Forge;

[Lane(TestLane.Integration)]
public sealed class ConfiguredPullRequestsTests : IDisposable
{
    private static readonly PullRepo _repo = new("forge.test", "acme", "widgets");
    private readonly DirectoryInfo _state = Directory.CreateTempSubdirectory("aw-sindri-configured-");

    [Fact]
    public async Task A_profile_with_no_forge_answers_no_forge()
    {
        var pulls = await Configured(forge: null);

        var result = await pulls.Find(new FindPull(_repo, "feature/x", Marker: null), TestContext.Current.CancellationToken);

        Assert.Equal("SND-FORGE-001", result.Error.Code);
    }

    [Fact]
    public async Task No_approved_profile_answers_no_forge()
    {
        var pulls = new ConfiguredPullRequests(new InMemoryProfiles(), Environment, TimeProvider.System);

        var result = await pulls.Find(new FindPull(_repo, "feature/x", Marker: null), TestContext.Current.CancellationToken);

        Assert.Equal("SND-FORGE-001", result.Error.Code);
    }

    [Fact]
    public async Task The_file_adapter_opens_and_finds_a_pull_request()
    {
        var pulls = await Configured(new ForgeSettings(Adapter: "file", Hosts: ["forge.test"]));
        var ct = TestContext.Current.CancellationToken;

        var opened = await pulls.Open(new OpenPull(_repo, "feature/x", "main", new string('a', 40), "T", "B", Draft: false, "run-1"), ct);
        var found = await pulls.Find(new FindPull(_repo, "feature/x", "run-1"), ct);

        Assert.Equal(opened.Value, found.Value);
    }

    [Fact]
    public async Task A_host_outside_the_profiles_list_is_refused()
    {
        var pulls = await Configured(new ForgeSettings(Adapter: "file", Hosts: ["forge.test"]));

        var result = await pulls.Get("https://elsewhere.test/acme/widgets/pull/1", TestContext.Current.CancellationToken);

        Assert.Equal("SND-FORGE-002", result.Error.Code);
    }

    [Fact]
    public async Task The_github_adapter_takes_exactly_one_host()
    {
        var pulls = await Configured(new ForgeSettings(Adapter: "github", Hosts: ["github.com", "ghe.test"]));

        var result = await pulls.Get("https://github.com/acme/widgets/pull/1", TestContext.Current.CancellationToken);

        Assert.Equal("SND-FORGE-002", result.Error.Code);
    }

    public void Dispose() => _state.Delete(recursive: true);

    private string? Environment(string name) => name == "AW_STATE_DIR" ? _state.FullName : null;

    private async Task<ConfiguredPullRequests> Configured(ForgeSettings? forge)
    {
        var profiles = new InMemoryProfiles();
        var profile = new SindriProfile(1, "thor", Workflow: forge is null ? null : new WorkflowSettings(Forge: forge));
        _ = await profiles.Save(new LoadedProfile(profile, ProfileHash.From(new string('b', 64)), "/home/profile.yaml"), TestContext.Current.CancellationToken);
        return new ConfiguredPullRequests(profiles, Environment, TimeProvider.System);
    }
}
