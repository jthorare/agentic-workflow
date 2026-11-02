using Aw.Context.Testing.Engine;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.ApproveProfile;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.GetStatus;

/// <summary>The status says whether model choice is on, computed from the approved profile (model-pick spec 8), so the app never derives it; the approval shows the notice when it is.</summary>
[Lane(TestLane.Unit)]
public sealed class StatusModelPickTests
{
    private readonly SliceHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static LoadedProfile Profile(string? pick)
    {
        var profile = ProfileRules.Check(new SindriProfile(1, "thor", Workflow: new WorkflowSettings(Agent: new WorkflowAgentSettings(Pick: pick)))).Value;
        return new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml");
    }

    private GetStatusService Status() => new(_h.Held, new ProfileReads(_h.Profiles, _h.Profiles), _h.Store, _h.Events, new ProjectorProgress(_h.Clock), new HealthServiceRows(new InMemoryViewStore(SliceHarness.Json), []), new InMemorySessionSnapshot());

    private ApproveProfileService Approve() => new(_h.Profiles, _h.Profiles, _h.Held, _h.Store, _h.Events, new ProfileChecks(FakeContextEngine.Instance, new FixedAgentCatalog()));

    [Theory]
    [InlineData("on", "on")]
    [InlineData("off", "off")]
    [InlineData(null, "off")]
    public async Task The_status_says_on_only_for_an_approved_profile_that_asks_for_it(string? pick, string expected)
    {
        var live = Profile(pick);
        _h.Profiles.Live = live;
        _ = await _h.HoldLease();
        _ = await Approve().Handle(new ApproveProfileRequest(live.Hash, Answer: null), Token);

        var status = (await Status().Handle(new GetStatusRequest(), Token)).Value;

        Assert.Equal(expected, status.Profile.ModelPick);
    }

    [Fact]
    public async Task A_live_profile_that_turns_it_on_is_not_on_until_it_is_approved()
    {
        var off = Profile("off");
        _h.Profiles.Live = off;
        _ = await _h.HoldLease();
        _ = await Approve().Handle(new ApproveProfileRequest(off.Hash, Answer: null), Token);
        _h.Profiles.Live = Profile("on");

        var status = (await Status().Handle(new GetStatusRequest(), Token)).Value;

        Assert.Equal(("off", true), (status.Profile.ModelPick, status.Profile.NeedsApproval));
    }

    [Fact]
    public async Task The_approval_carries_the_notice_when_the_pick_is_on_and_none_when_it_is_off()
    {
        _ = await _h.HoldLease();
        _h.Profiles.Live = Profile("on");
        var on = (await Approve().Handle(new ApproveProfileRequest(Hash: null, Answer: null), Token)).Value;
        _h.Profiles.Live = Profile("off");
        var off = (await Approve().Handle(new ApproveProfileRequest(Hash: null, Answer: null), Token)).Value;

        Assert.Equal("Model choice is on: each agent step's brief and input files (up to 100,000 characters, secrets and denied paths removed), and for the concept scan its map summary, are sent to Jev to pick a model.", on.Notice);
        Assert.Null(off.Notice);
    }
}
