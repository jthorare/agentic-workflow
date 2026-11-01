using System.Text;
using System.Text.Json;
using Aw.Context.Core.Engine;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Host.Models;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ContextCalls.Routing;

/// <summary>'context.models' as the router reads it (model routing spec §4 "Settings"): from the approved profile only, once per approved hash.</summary>
public sealed class ApprovedModelsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static LoadedProfile Profile(string context)
    {
        var loaded = SliceHarness.Profile();
        var withContext = loaded.Profile with { Context = JsonDocument.Parse(context).RootElement };
        return new LoadedProfile(withContext, ProfileHashing.Of(Encoding.UTF8.GetBytes(context)), "/home/profile.yaml");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_approved_profile_is_the_defaults()
    {
        var profiles = new InMemoryProfiles();

        var settings = await new ApprovedModels(profiles).Current(Token);

        Assert.Empty(settings.Kinds);
        Assert.Null(settings.Default.Harnesses);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_approved_profiles_models_are_read()
    {
        var profiles = new InMemoryProfiles();
        _ = await profiles.Save(Profile("""{"models":{"default":{"harnesses":["codex"]}}}"""), Token);

        var settings = await new ApprovedModels(profiles).Current(Token);

        Assert.Equal([ModelHarness.Codex], settings.Default.Harnesses);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_new_approval_is_picked_up_without_a_restart_and_the_same_hash_is_not_read_twice()
    {
        var profiles = new InMemoryProfiles();
        var reader = new ApprovedModels(profiles);
        _ = await profiles.Save(Profile("""{"models":{"default":{"harnesses":["codex"]}}}"""), Token);
        var first = await reader.Current(Token);
        var again = await reader.Current(Token);
        _ = await profiles.Save(Profile("""{"models":{"default":{"harnesses":["cursor"]}}}"""), Token);
        var changed = await reader.Current(Token);

        Assert.Same(first, again);
        Assert.Equal([ModelHarness.Cursor], changed.Default.Harnesses);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_old_conceptScan_models_key_reaches_the_router_as_names()
    {
        var profiles = new InMemoryProfiles();
        _ = await profiles.Save(Profile("""{"conceptScan":{"models":{"claude":{"cheap":"old-cheap","strong":"old-strong"}}}}"""), Token);

        var settings = await new ApprovedModels(profiles).Current(Token);

        Assert.Equal("old-cheap", settings.Names.Written(ModelHarness.ClaudeCode, Context.Core.Models.ModelTier.Cheap));
    }
}
