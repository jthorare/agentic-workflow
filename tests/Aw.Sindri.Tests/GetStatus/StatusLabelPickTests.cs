using System.Text.Json;
using Aw.Context.Testing.Engine;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.ApproveProfile;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.GetStatus;

/// <summary>The status says whether the Jev label pick is off, on, or on without a key, from the approved profile (model routing spec 8.5), so the app never derives it.</summary>
[Lane(TestLane.Unit)]
public sealed class StatusLabelPickTests
{
    private readonly SliceHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static LoadedProfile Profile(string? pick)
    {
        using var context = JsonDocument.Parse(pick is null ? """{"labeling":{"enabled":true}}""" : """{"models":{"labeling":{"pick":"PICK"}}}""".Replace("PICK", pick, StringComparison.Ordinal));
        var profile = ProfileRules.Check(new SindriProfile(1, "thor", Context: context.RootElement.Clone())).Value;
        return new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml");
    }

    private GetStatusService Status(bool key) => new(_h.Held, new ProfileReads(_h.Profiles, _h.Profiles, new Key(key)), _h.Store, _h.Events, new ProjectorProgress(_h.Clock), new HealthServiceRows(new InMemoryViewStore(SliceHarness.Json), []), new InMemorySessionSnapshot());

    private ApproveProfileService Approve() => new(_h.Profiles, _h.Profiles, _h.Held, _h.Store, _h.Events, new ProfileChecks(FakeContextEngine.Instance, new FixedAgentCatalog()));

    [Theory]
    [InlineData("jev", true, "on")]
    [InlineData("jev", false, "needs-key")]
    [InlineData("off", true, "off")]
    [InlineData("off", false, "off")]
    [InlineData(null, false, "off")]
    public async Task The_status_says_on_off_or_needs_key_for_an_approved_profile(string? pick, bool key, string expected)
    {
        var live = Profile(pick);
        _h.Profiles.Live = live;
        _ = await _h.HoldLease();
        _ = await Approve().Handle(new ApproveProfileRequest(live.Hash, Answer: null), Token);

        var status = (await Status(key).Handle(new GetStatusRequest(), Token)).Value;

        Assert.Equal(expected, status.Profile.LabelPick);
    }

    [Fact]
    public async Task A_live_profile_that_turns_it_on_is_off_until_it_is_approved()
    {
        var off = Profile("off");
        _h.Profiles.Live = off;
        _ = await _h.HoldLease();
        _ = await Approve().Handle(new ApproveProfileRequest(off.Hash, Answer: null), Token);
        _h.Profiles.Live = Profile("jev");

        var status = (await Status(key: true).Handle(new GetStatusRequest(), Token)).Value;

        Assert.Equal(("off", true), (status.Profile.LabelPick, status.Profile.NeedsApproval));
    }

    [Fact]
    public async Task A_host_with_no_key_probe_counts_the_key_present()
    {
        var live = Profile("jev");
        _h.Profiles.Live = live;
        _ = await _h.HoldLease();
        _ = await Approve().Handle(new ApproveProfileRequest(live.Hash, Answer: null), Token);
        var bare = new GetStatusService(_h.Held, new ProfileReads(_h.Profiles, _h.Profiles), _h.Store, _h.Events, new ProjectorProgress(_h.Clock), new HealthServiceRows(new InMemoryViewStore(SliceHarness.Json), []), new InMemorySessionSnapshot());

        Assert.Equal("on", (await bare.Handle(new GetStatusRequest(), Token)).Value.Profile.LabelPick);
    }

    private sealed class Key(bool present) : IJevKeyProbe
    {
        public bool Present() => present;
    }
}
