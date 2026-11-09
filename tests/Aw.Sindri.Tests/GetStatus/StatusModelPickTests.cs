using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Views;
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

    private GetStatusService Status() => new(_h.Held, new ProfileReads(_h.Profiles, _h.Profiles), _h.Store, new ProjectorProgress(_h.Clock), new HealthServiceRows(new InMemoryViewStore(SliceHarness.Json), []), new InMemorySessionSnapshot());

    [Theory]
    [InlineData("on", "on")]
    [InlineData("off", "off")]
    [InlineData(null, "off")]
    public async Task The_status_says_on_only_for_an_approved_profile_that_asks_for_it(string? pick, string expected)
    {
        var live = Profile(pick);
        _h.Profiles.Live = live;
        _ = await _h.HoldLease();
        _ = await _h.Profiles.Save(live, Token);

        var status = (await Status().Handle(new GetStatusRequest(), Token)).Value;

        Assert.Equal(expected, status.Profile.ModelPick);
    }

}
