using Aw.Kernel.Json;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.GetRepoStatus;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.GetRepoStatus;

/// <summary>'repo status': the four states against the approved profile, with the live profile only naming a repo that waits for approval.</summary>
public sealed class GetRepoStatusTests
{
    private readonly InMemoryProfiles _profiles = new();
    private readonly FakeRepoLocator _locator = new FakeRepoLocator().In("/work/demo/src", "/work/demo").In("/work/wt/src", "/work/demo");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static LoadedProfile Profile(params RepoEntry[] repos)
    {
        var profile = new SindriProfile(1, "thor", Repos: repos);
        return new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml");
    }

    private async Task<RepoStatus> Status(string directory) =>
        (await new GetRepoStatusService(_profiles, _profiles, _locator).Handle(new GetRepoStatusRequest(directory), Token)).Value.Status;

    [Fact, Trait("Lane", "unit")]
    public async Task GetRepoStatus_is_outside_git_for_a_directory_git_does_not_know()
        => Assert.Equal(new RepoStatus(RepoState.OutsideGit, Path: null, Name: null), await Status("/elsewhere"));

    [Fact, Trait("Lane", "unit")]
    public async Task GetRepoStatus_has_no_approved_profile_before_the_first_approval()
        => Assert.Equal(new RepoStatus(RepoState.NoApprovedProfile, "/work/demo", Name: null), await Status("/work/demo/src"));

    [Fact, Trait("Lane", "unit")]
    public async Task GetRepoStatus_answers_as_for_no_approved_profile_when_the_live_profile_is_invalid()
    {
        _profiles.Live = Core.SindriErrors.ProfileInvalid.With("/home/profile.yaml: line 3: unknown key 'tennant'");

        var result = await new GetRepoStatusService(_profiles, _profiles, _locator).Handle(new GetRepoStatusRequest("/work/demo/src"), Token);

        Assert.False(result.IsError);
        Assert.Equal(new RepoStatus(RepoState.NoApprovedProfile, "/work/demo", Name: null), result.Value.Status);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task GetRepoStatus_is_onboarded_when_the_approved_profile_names_the_checkout_even_from_a_worktree()
    {
        _ = await _profiles.Save(Profile(new RepoEntry("demo", "/work/demo")), Token);

        Assert.Equal(new RepoStatus(RepoState.Onboarded, "/work/demo", "demo"), await Status("/work/wt/src"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task GetRepoStatus_is_not_onboarded_when_the_approved_profile_lacks_the_repo()
    {
        _ = await _profiles.Save(Profile(new RepoEntry("other", "/work/other")), Token);

        Assert.Equal(new RepoStatus(RepoState.NotOnboarded, "/work/demo", Name: null), await Status("/work/demo/src"));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_nudge_is_one_line_only_for_a_repo_that_is_not_onboarded()
    {
        Assert.Equal("sindri: demo is not onboarded (no secret scan or index); run: sindri repo onboard", RepoStatusRules.NudgeLine(new RepoStatus(RepoState.NotOnboarded, "/work/demo", Name: null)));
        Assert.Equal("sindri: demo is not onboarded yet; run: sindri repo onboard", RepoStatusRules.NudgeLine(new RepoStatus(RepoState.NotOnboarded, "/work/demo", "demo")));
        Assert.All(
            [new RepoStatus(RepoState.Onboarded, "/work/demo", "demo"), new RepoStatus(RepoState.NoApprovedProfile, "/work/demo", Name: null), new RepoStatus(RepoState.OutsideGit, Path: null, Name: null)],
            static s => Assert.Equal(string.Empty, RepoStatusRules.NudgeLine(s)));
    }
}
