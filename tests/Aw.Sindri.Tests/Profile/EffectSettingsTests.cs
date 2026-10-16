using System.Text;
using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Profile;

/// <summary>
/// The profile's effect keys (engine spec §5.7, §7.2 S1-D11; engine P6 unit (d)): 'workflow.effects', the allowlist,
/// and 'workflow.dispatchDelay', the undo window (default 10s, seconds allowed like a service cadence, 5s to 24h).
/// A profile without them keeps its canonical JSON, so an approval made before P6 still holds.
/// </summary>
public sealed class EffectSettingsTests
{
    private static readonly SindriProfile _valid = new(1, "thor");

    [Fact, Trait("Lane", "unit")]
    public void Without_the_keys_there_is_no_allowlist_and_the_delay_is_10s()
    {
        var settings = EffectSettings.Of(_valid with { Workflow = new("1m") });

        Assert.Null(settings.Allowlist);
        Assert.Equal(TimeSpan.FromSeconds(10), settings.DispatchDelay);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_keys_give_the_allowlist_and_the_delay()
    {
        var settings = EffectSettings.Of(_valid with { Workflow = new(Effects: ["effect.notify.desk", "effect.claim.ref"], DispatchDelay: "60s") });

        Assert.Equal(new HashSet<BlockId> { BlockId.From("effect.notify.desk"), BlockId.From("effect.claim.ref") }, settings.Allowlist);
        Assert.Equal(TimeSpan.FromSeconds(60), settings.DispatchDelay);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_empty_allowlist_allows_nothing_and_is_not_the_absent_key()
    {
        var settings = EffectSettings.Of(_valid with { Workflow = new(Effects: []) });

        Assert.NotNull(settings.Allowlist);
        Assert.Empty(settings.Allowlist);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("5s", 5)]
    [InlineData("2m", 120)]
    [InlineData("24h", 86_400)]
    public void The_dispatch_delay_reads_like_a_cadence(string text, int seconds)
    {
        Assert.True(ProfileRules.Check(_valid with { Workflow = new(DispatchDelay: text) }).IsSuccess);
        Assert.Equal(TimeSpan.FromSeconds(seconds), EffectSettings.Of(_valid with { Workflow = new(DispatchDelay: text) }).DispatchDelay);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(null, "2s", "workflow.dispatchDelay: '2s' is not a delay")]
    [InlineData(null, "25h", "workflow.dispatchDelay: '25h' is not a delay")]
    [InlineData(null, "1d", "workflow.dispatchDelay: '1d' is not a delay")]
    [InlineData("notify.desk", null, "workflow.effects[0]: 'notify.desk' is not an effect id")]
    [InlineData("step.produce", null, "workflow.effects[0]: 'step.produce' is not an effect id")]
    public void A_bad_effect_key_names_its_key_path(string? effect, string? delay, string message)
    {
        var error = ProfileRules.Check(_valid with { Workflow = new(Effects: effect is null ? null : [effect], DispatchDelay: delay) }).Error;

        Assert.Equal(SindriErrors.ProfileInvalid.Code, error.Code);
        Assert.StartsWith(message, error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_diff_names_the_effect_keys()
    {
        var live = ProfileRules.Check(_valid).Value;

        Assert.Equal(["workflow.dispatchDelay", "workflow.effects"],
            ProfileDiff.Between(live, live with { Workflow = new(Effects: ["effect.notify.desk"], DispatchDelay: "60s") }));
        Assert.Equal(["workflow.effects"],
            ProfileDiff.Between(live with { Workflow = new(Effects: ["effect.notify.desk"]) }, live with { Workflow = new(Effects: ["effect.notify.desk", "effect.claim.ref"]) }));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_profile_without_the_keys_keeps_its_canonical_json_and_hash()
    {
        var before = ProfileRules.Check(_valid with { Workflow = new("1m") }).Value;
        var json = AwJson.Serialize(before, SliceHarness.Json);

        using var document = JsonDocument.Parse(json);
        Assert.Equal("""{"deskTimeout":"1m"}""", document.RootElement.GetProperty("workflow").GetRawText());
        Assert.DoesNotContain("effects", Encoding.UTF8.GetString(json), StringComparison.Ordinal);
        Assert.DoesNotContain("dispatchDelay", Encoding.UTF8.GetString(json), StringComparison.Ordinal);
        Assert.NotEqual(ProfileHashing.Of(json),
            ProfileHashing.Of(AwJson.Serialize(before with { Workflow = new("1m", DispatchDelay: "10s") }, SliceHarness.Json)));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_dev_profile_validates_with_desk_notices_allowlisted_and_a_60s_delay()
    {
        using var files = new ProfileFiles();
        files.Write(ShippedRecipes.Read("dev/profile.yaml"));

        var loaded = await files.Source.Load(TestContext.Current.CancellationToken);

        Assert.False(loaded.IsError, loaded.IsError ? loaded.Error.Message : null);
        var settings = EffectSettings.Of(loaded.Value.Profile);
        Assert.Equal(new HashSet<BlockId> { BlockId.From("effect.notify.desk"), BlockId.From("effect.claim.ref") }, settings.Allowlist);
        Assert.Equal(TimeSpan.FromSeconds(60), settings.DispatchDelay);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task LoadRecipe_reads_the_dispatch_delay_from_the_approved_profile_not_the_live_file()
    {
        var profiles = new InMemoryProfiles();
        var times = new ProfileWorkflowTimes(profiles, profiles);
        var ct = TestContext.Current.CancellationToken;

        var none = await times.DispatchDelay(ct);
        profiles.Live = Loaded(_valid with { Workflow = new(DispatchDelay: "5s") });
        _ = await profiles.Save(Loaded(_valid with { Workflow = new(DispatchDelay: "60s") }), ct);
        var approved = await times.DispatchDelay(ct);

        Assert.Equal(TimeSpan.FromSeconds(10), none.Value);
        Assert.Equal(TimeSpan.FromSeconds(60), approved.Value);
    }

    private static LoadedProfile Loaded(SindriProfile profile) =>
        new(profile, ProfileHash.From(new string('a', 64)), "/home/profile.yaml");
}
