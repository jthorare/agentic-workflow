using System.Text;
using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Blocks.Gate;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Workflow.Blocks;

/// <summary>'gate.route.startable' (spec §12, engine P7 plan D7): a stub over the approved profile's 'workflow.route.startable'.</summary>
public sealed class RouteStartableGateTests
{
    private static readonly SindriProfile _valid = new(1, "thor");

    [Fact, Trait("Lane", "unit")]
    public async Task Startable_all_passes_and_none_fails()
    {
        Assert.Equal(GateVerdict.Pass, (await Evaluate(_valid with { Workflow = new(Route: new("all")) })).Verdict);
        var stopped = await Evaluate(_valid with { Workflow = new(Route: new("none")) });

        Assert.Equal(GateVerdict.Fail, stopped.Verdict);
        Assert.Equal(["workflow.route.startable is 'none' in the profile, so the router stops this item (fix: set it to 'all', then resume)"], stopped.Reasons);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_profile_without_the_key_and_no_approved_profile_both_start_items()
    {
        Assert.Equal(GateVerdict.Pass, (await Evaluate(_valid)).Verdict);
        Assert.Equal(GateVerdict.Pass, (await Evaluate(profile: null)).Verdict);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_route_key_is_read_from_the_profile()
    {
        Assert.True(RouteSettings.StartableOf(_valid));
        Assert.True(RouteSettings.StartableOf(_valid with { Workflow = new(Route: new("all")) }));
        Assert.False(RouteSettings.StartableOf(_valid with { Workflow = new(Route: new("none")) }));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unknown_startable_value_is_a_profile_error()
    {
        var error = ProfileRules.Check(_valid with { Workflow = new(Route: new("some")) }).Error;

        Assert.Equal(SindriErrors.ProfileInvalid.Code, error.Code);
        Assert.StartsWith("workflow.route.startable: 'some' is not all or none", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_set_route_shows_in_the_profile_diff_and_an_absent_one_changes_no_hash()
    {
        var changes = ProfileDiff.Between(_valid, _valid with { Workflow = new(Route: new("none")) });

        Assert.Contains("workflow.route.startable", changes, StringComparer.Ordinal);
        Assert.DoesNotContain("route", Encoding.UTF8.GetString(AwJson.Serialize(ProfileRules.Check(_valid).Value, SliceHarness.Json)), StringComparison.Ordinal);
    }

    private static async Task<GateResult> Evaluate(SindriProfile? profile)
    {
        var profiles = new InMemoryProfiles();
        if (profile is not null)
        {
            _ = await profiles.Save(new LoadedProfile(profile, ProfileHash.From(new string('b', 64)), "/home/profile.yaml"), TestContext.Current.CancellationToken);
        }

        return await new RouteStartableGate(profiles).Evaluate(
            new GateContext(RouteStartableGate.Descriptor.Id, new Dictionary<string, JsonElement>(StringComparer.Ordinal), new Dictionary<FactKey, JsonElement>(), Threshold: null),
            TestContext.Current.CancellationToken);
    }
}
