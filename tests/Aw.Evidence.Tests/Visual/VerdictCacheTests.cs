using Aw.Evidence.Core.Ids;
using Aw.Evidence.Core.Judging;
using Aw.Evidence.Core.Scripts;
using Aw.Evidence.Core.Visual;
using Aw.Evidence.Testing;

namespace Aw.Evidence.Tests.Visual;

public sealed class VerdictCacheTests
{
    private static readonly VerdictCacheContext _context = new("visual-critique/v1", "judge-default", "abc123", "scenario", Fixtures: null, "desktop", "140.0", JevImages: false);
    private static readonly RunInstanceId _run1 = RunInstanceId.From("instance-1");
    private static readonly RunInstanceId _run2 = RunInstanceId.From("instance-2");

    private static string Key(VerdictCacheContext context, string after = "a", string? baseline = "b") =>
        new VerdictCacheKey(after, baseline, context).Compute(TestJson.Options);

    [Fact, Trait("Lane", "unit")]
    public void The_key_is_a_stable_sha256_of_the_pixels_and_their_context()
    {
        Assert.Equal(Key(_context), Key(_context));
        Assert.Equal(64, Key(_context).Length);
        Assert.NotEqual(Key(_context), Key(_context, after: "a2"), StringComparer.Ordinal);
        Assert.NotEqual(Key(_context), Key(_context, baseline: null), StringComparer.Ordinal);
        Assert.NotEqual(Key(_context), Key(_context with { Model = "other" }), StringComparer.Ordinal);
        Assert.NotEqual(Key(_context), Key(_context with { BrowserVersion = "141.0" }), StringComparer.Ordinal);
        Assert.NotEqual(Key(_context), Key(_context with { AppBuild = null }), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_key_changes_with_whether_jev_gets_images_and_a_v1_entry_never_matches()
    {
        Assert.NotEqual(Key(_context), Key(_context with { JevImages = true }), StringComparer.Ordinal);
        Assert.Equal("visual-critique/v2", VerdictCacheContext.CurrentPromptVersion);   // v1 verdicts came from the CLIs only
        Assert.NotEqual(Key(_context), Key(_context with { PromptVersion = VerdictCacheContext.CurrentPromptVersion }), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void With_a_known_app_build_a_verdict_is_reused_across_runs()
    {
        var cache = VerdictCache.Empty.With("k", new VisualVerdict(VisualDecision.Sloppy, ["gap"], Usage: null), _run1);

        Assert.Equal(VisualDecision.Sloppy, cache.Lookup("k", appBuildKnown: true, _run2)?.Decision);
    }

    [Fact, Trait("Lane", "unit")]
    public void With_an_unknown_app_build_a_verdict_is_trusted_only_within_the_run_invocation_that_formed_it()
    {
        var cache = VerdictCache.Empty.With("k", new VisualVerdict(VisualDecision.LooksRight, [], Usage: null), _run1);

        Assert.NotNull(cache.Lookup("k", appBuildKnown: false, _run1));
        Assert.Null(cache.Lookup("k", appBuildKnown: false, _run2));
        Assert.Null(cache.Lookup("other", appBuildKnown: true, _run1));
    }

    [Fact, Trait("Lane", "unit")]
    public void Storing_a_verdict_leaves_the_original_cache_untouched()
    {
        var empty = VerdictCache.Empty;

        var stored = empty.With("k", new VisualVerdict(VisualDecision.LooksOff, [], Usage: null), _run1);

        Assert.Empty(empty.Entries);
        _ = Assert.Single(stored.Entries);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_scenario_hash_ignores_planning_metadata_but_not_the_steps()
    {
        var script = A.Script().Build();

        Assert.Equal(VerdictCacheContext.ScenarioOf(script, TestJson.Options), VerdictCacheContext.ScenarioOf(script with { Planning = new PlanningMeta(Model: "m") }, TestJson.Options));
        Assert.NotEqual(VerdictCacheContext.ScenarioOf(script, TestJson.Options), VerdictCacheContext.ScenarioOf(script with { Viewports = [ViewportKind.Phone] }, TestJson.Options), StringComparer.Ordinal);
    }
}
