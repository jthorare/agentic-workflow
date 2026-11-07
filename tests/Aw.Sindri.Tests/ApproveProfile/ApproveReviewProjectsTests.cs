using Aw.Context.Testing.Engine;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Projects.Cards;
using Aw.Sindri.Features.ApproveProfile;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ApproveProfile;

/// <summary>ApproveProfileService runs ProfileChecks.ReviewProjects (PR review page spec §7): a 'review.projects' entry that is not on the Projects page is refused SND-REVIEW-002 and nothing is saved.</summary>
[Trait("Lane", "unit")]
public sealed class ApproveReviewProjectsTests
{
    private const string Known = "prj_0000000000000000000000000A";
    private const string Unknown = "prj_0000000000000000000000000B";
    private readonly SliceHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static LoadedProfile Live(string project)
    {
        var profile = ProfileRules.Check(new SindriProfile(1, "thor", Review: new PrReviewSettings(Projects: [new(project)]))).Value;
        return new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml");
    }

    private ApproveProfileService Service() => new(_h.Profiles, _h.Profiles, _h.Held, _h.Store, _h.Events, new ProfileChecks(FakeContextEngine.Instance, new FixedAgentCatalog(), projects: new OneCard()));

    [Fact]
    public async Task An_approval_naming_a_project_that_is_not_on_the_page_is_refused_and_saves_nothing()
    {
        var live = Live(Unknown);
        _h.Profiles.Live = live;
        _ = await _h.HoldLease();

        var refused = await Service().Handle(new ApproveProfileRequest(live.Hash, _h.Confirm(live.Hash)), Token);

        Assert.Equal("SND-REVIEW-002", refused.Error.Code);
        Assert.Null((await _h.Profiles.Latest(Token)).Value);
    }

    [Fact]
    public async Task An_approval_naming_a_project_on_the_page_goes_through()
    {
        var live = Live(Known);
        _h.Profiles.Live = live;
        _ = await _h.HoldLease();

        var approved = await Service().Handle(new ApproveProfileRequest(live.Hash, _h.Confirm(live.Hash)), Token);

        Assert.True(approved.Value.Approved);
    }

    private sealed class OneCard : IProjectPages
    {
        public ValueTask<Kernel.Result<ProjectsPage>> Read(bool all, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Kernel.Result<ProjectsPage>>(new ProjectsPage(Empty: null, Viewer: null, Freshness: [],
                [new ProjectCard(Known, ProjectOrigin.HandMade, "Sindri", Description: null, State: null, StateSetByOwner: false, Health: null, Lead: null, Target: null, OpenIssues: null,
                    Counting: false, OpenPrs: null, PrScope: null, LastActivity: null, FinishedAt: null, Repos: [], Links: [], Notes: null, Synced: true, Version: 1)],
                Finished: [], Hidden: []));
    }
}
