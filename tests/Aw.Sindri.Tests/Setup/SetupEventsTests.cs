using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Setup;

/// <summary>The setup-once events (spec §7.5): each is catalogued with its key shape and the views it invalidates, and the public form leaves out what is private.</summary>
public sealed class SetupEventsTests
{
    private readonly SliceHarness _h = new();

    public static TheoryData<string, string> Keys => new()
    {
        { "setup.step.changed", "setup.step.changed:<id>:<state>:<n>" },
        { "repo.added", "repo.added:<repo>" },
        { "repo.onboarded", "repo.onboarded:<repo>:<hash>" },
        { "recipe.registration.completed", "recipe.registration.completed:<epoch>" },
        { "mcp.registered", "mcp.registered:<commandId>" },
        { "setup.consent.given", "setup.consent.given:<profileHash>" },
        { "onyx.deferred", "onyx.deferred:<commandId>" },
    };

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Keys))]
    public void Each_setup_event_is_public_and_catalogued_with_its_key_shape(string type, string key)
    {
        var entry = Assert.Single(EventCatalog.All, e => e.Type == type);

        Assert.True(entry.Public);
        Assert.NotNull(entry.PublicPayload);
        Assert.Equal(key, entry.KeyShape);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("setup.step.changed", "setup")]
    [InlineData("repo.added", "setup")]
    [InlineData("mcp.registered", "setup")]
    [InlineData("recipe.registration.completed", "setup")]
    [InlineData("recipe.registration.completed", "recipes")]
    public void The_events_that_move_a_step_invalidate_the_views_they_change(string type, string view) =>
        Assert.Contains(view, Assert.Single(EventCatalog.All, e => e.Type == type).Invalidates, StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void The_setup_and_recipes_views_are_named_views()
    {
        Assert.Contains("setup", ApiViews.All, StringComparer.Ordinal);
        Assert.Contains("recipes", ApiViews.All, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_public_repo_added_leaves_the_path_out()
    {
        var row = Row("repo.added", """{"commandId":"00000000-0000-0000-0000-000000000001","repo":"web","path":"/work/web"}""");

        var shown = PublicEvents.For(row, SliceHarness.Json, new PassThroughScrubber(), includeNonPublic: false).Value!;

        Assert.Equal("web", shown.Payload.GetProperty("repo").GetString());
        Assert.False(shown.Payload.TryGetProperty("path", out _));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_consent_event_names_the_steps_and_the_profile_it_was_given_for()
    {
        var row = Row("setup.consent.given", """{"commandId":"00000000-0000-0000-0000-000000000001","steps":["mcp","map"],"profileHash":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}""");

        var shown = PublicEvents.For(row, SliceHarness.Json, new PassThroughScrubber(), includeNonPublic: false).Value!;

        Expect.Sequence(["mcp", "map"], shown.Payload.GetProperty("steps").EnumerateArray().Select(step => step.GetString()!));
        Assert.Equal("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", shown.Payload.GetProperty("profileHash").GetString());
    }

    private LedgerEvent Row(string type, string payload)
    {
        var entry = _h.Events.Entry(EventTypes.ProcessStopped, new ProcessStopped(1, 1, StopReason.Stop), "k");
        return new LedgerEvent(Sequence: 1, Epoch: 1, entry.Envelope with { Type = type, Payload = payload }, IdempotencyKey: "k", Item: null);
    }
}
