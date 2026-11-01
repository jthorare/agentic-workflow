using System.Text.Json;
using Aw.Context.Testing.Engine;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.ApproveProfile;
using Aw.Sindri.Features.ValidateProfile;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ContextCalls.Routing;

/// <summary>The old 'context.conceptScan.models' key beside 'context.models.names' (model routing spec §9): `profile validate` and the approval summary print the one warning line; the new key wins.</summary>
public sealed class ModelsWarningTests
{
    private const string Both = """{"models":{"names":{"codex":{"cheap":"gpt-new"}}},"conceptScan":{"models":{"claude":{"cheap":"old"}}}}""";
    private const string OldOnly = """{"conceptScan":{"models":{"claude":{"cheap":"old"}}}}""";
    private const string Line = "context.conceptScan.models is replaced by context.models.names; the new key is used";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static LoadedProfile Loaded(string context)
    {
        var profile = SliceHarness.Profile().Profile with { Context = JsonDocument.Parse(context).RootElement };
        return new LoadedProfile(profile, ProfileHashing.Of(System.Text.Encoding.UTF8.GetBytes(context)), "/home/profile.yaml");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Validate_prints_the_warning_when_both_keys_are_there()
    {
        var h = new SliceHarness();
        h.Profiles.Live = Loaded(Both);

        var validated = await new ValidateProfileService(h.Profiles, h.Profiles, h.Store, h.Events, FakeContextEngine.Instance).Handle(new ValidateProfileRequest(), Token);

        Assert.Equal([Line], validated.Value.Warnings);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Validate_prints_nothing_for_the_old_key_alone()
    {
        var h = new SliceHarness();
        h.Profiles.Live = Loaded(OldOnly);

        var validated = await new ValidateProfileService(h.Profiles, h.Profiles, h.Store, h.Events, FakeContextEngine.Instance).Handle(new ValidateProfileRequest(), Token);

        Assert.Null(validated.Value.Warnings);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_approval_summary_lists_the_warning()
    {
        var h = new SliceHarness();
        h.Profiles.Live = Loaded(Both);
        var service = new ApproveProfileService(h.Profiles, h.Profiles, h.Held, h.Store, h.Events, new ProfileChecks(FakeContextEngine.Instance, new FixedAgentCatalog()));

        var shown = (await service.Handle(new ApproveProfileRequest(Hash: null, Answer: null), Token)).Value;

        Assert.Equal([Line], shown.Warnings);
    }
}
