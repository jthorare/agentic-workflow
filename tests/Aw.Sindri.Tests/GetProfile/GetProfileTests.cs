using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.GetProfile;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.GetProfile;

/// <summary>GET /profile (API spec §3.1): the 'profile' view's one row.</summary>
public sealed class GetProfileTests
{
    private static readonly ProfileHash _a = ProfileHash.From(new string('a', 64));
    private static readonly ProfileHash _b = ProfileHash.From(new string('b', 64));
    private readonly ViewsHarness _v = new();
    private readonly FakeRepoInspector _git = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task GetProfile_shows_approved_and_live_hash_and_the_changed_paths()
    {
        var approvedAt = _v.Slice.Clock.GetUtcNow();
        await _v.Fold(EventTypes.ProfileApproved, new ProfileApproved(_a, []));
        _v.Slice.Clock.Advance(TimeSpan.FromSeconds(30));
        await _v.Fold(EventTypes.ProfileChecked, new ProfileChecked(_v.Slice.Ids.New<CommandId>(), _a, _b, Drifted: true,
            ["services.heartbeat.every"], [ServiceName.From("heartbeat")]));

        var result = await Service().Handle(A.GetProfileRequest().Build(), Token);

        Assert.True(result.IsSuccess);
        var profile = result.Value;
        Assert.Equal((_a, approvedAt, _b, approvedAt.AddSeconds(30), true), (profile.Approved, profile.ApprovedAt, profile.Live, profile.CheckedAt, profile.Changed));
        Assert.Equal(["services.heartbeat.every"], profile.ChangedPaths);
        Assert.Equal(_v.Progress.Current.AsOf, profile.AsOf);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Before_any_event_it_answers_nulls_and_not_changed()
    {
        var profile = (await Service().Handle(new GetProfileRequest(), Token)).Value;

        Assert.Equal((null, null, null, null, false), (profile.Approved, profile.ApprovedAt, profile.Live, profile.CheckedAt, profile.Changed));
        Assert.Empty(profile.ChangedPaths);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Nothing_approved_yet_it_summarises_the_live_profile_with_the_real_path_and_file_facts()
    {
        var live = LiveProfile(new SindriProfile(1, "t", Repos: [new RepoEntry("web", "/link/web")], Workflow: new WorkflowSettings(Effects: ["effect.claim.ref"])));
        _ = _git.Has("/link/web", "/work/web").Weighs("/link/web", files: 120, bytes: (5 * 1024 * 1024) + 300_000, realPath: "/work/web");

        var summary = (await Service(live: live).Handle(new GetProfileRequest(), Token)).Value.Summary;

        var repo = Assert.Single(summary!.Repos);
        Assert.Equal(("web", "/work/web", 120, 5.3), (repo.Name, repo.Path, repo.Files, repo.Megabytes));
        Assert.Equal(ProfileChanges.RepoSentences("web", "/work/web"), repo.Sentences);
        Assert.Equal(["Allows the effect \"claim ref\""], summary.Allowance);
        Assert.Empty(summary.Connections);
        Assert.Empty(summary.HumanOnly);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_changed_profile_summarises_only_what_the_approved_one_does_not_name()
    {
        await _v.Fold(EventTypes.ProfileApproved, new ProfileApproved(_a, []));
        await _v.Fold(EventTypes.ProfileChecked, new ProfileChecked(_v.Slice.Ids.New<CommandId>(), _a, _b, Drifted: true, ["repos"], []));
        var approved = new LoadedProfile(new SindriProfile(1, "t", Repos: [new RepoEntry("web", "/work/web")]), _a, "/p.yaml");
        var live = LiveProfile(new SindriProfile(1, "t", Repos: [new RepoEntry("web", "/work/web"), new RepoEntry("api", "/work/api")]));

        var summary = (await Service(profile: approved, live: live).Handle(new GetProfileRequest(), Token)).Value.Summary;

        Assert.Equal(["api"], summary!.Repos.Select(static r => r.Name), StringComparer.Ordinal);
        Assert.Equal(1, _git.Weighed);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_approved_unchanged_profile_has_no_summary()
    {
        var approved = new LoadedProfile(new SindriProfile(1, "t"), _a, "/p.yaml");

        var response = (await Service(profile: approved, live: LiveProfile(new SindriProfile(1, "t", Repos: [new RepoEntry("web", "/work/web")]))).Handle(new GetProfileRequest(), Token)).Value;

        Assert.Null(response.Summary);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_repo_git_cannot_read_is_previewed_with_the_profiles_path_and_no_facts()
    {
        var live = LiveProfile(new SindriProfile(1, "t", Repos: [new RepoEntry("gone", "/work/gone")]));

        var repo = Assert.Single((await Service(live: live).Handle(new GetProfileRequest(), Token)).Value.Summary!.Repos);

        Assert.Equal(("/work/gone", 0, 0.0), (repo.Path, repo.Files, repo.Megabytes));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task When_nothing_is_new_there_is_no_summary()
    {
        var live = LiveProfile(new SindriProfile(1, "t"));

        Assert.Null((await Service(live: live).Handle(new GetProfileRequest(), Token)).Value.Summary);
    }

    private static LoadedProfile LiveProfile(SindriProfile profile) => new(profile, _b, "/live.yaml");

    private GetProfileService Service(LoadedProfile? profile = null, LoadedProfile? live = null) =>
        new(_v.Views, _v.Progress, new Approved(profile), new Live(live), new RepoGitCache(_git, _v.Slice.Clock));

    private sealed class Live(LoadedProfile? profile) : IProfileSource
    {
        public ValueTask<Result<LoadedProfile>> Load(CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<LoadedProfile>>(profile is null ? SindriErrors.ProfileMissing.With("no live profile") : profile);
    }

    private sealed class Approved(LoadedProfile? profile) : IApprovedProfile
    {
        public ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken) => ValueTask.FromResult<Result<LoadedProfile?>>(profile);
    }
}
