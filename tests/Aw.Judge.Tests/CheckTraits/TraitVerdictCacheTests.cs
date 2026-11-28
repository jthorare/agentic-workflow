using Aw.Judge.Core.Providers;
using Aw.Judge.Core.Traits;
using Aw.Judge.Features.CheckTraits;
using Aw.Judge.Testing;

namespace Aw.Judge.Tests.CheckTraits;

// The verdict cache: a trait's verdict on an excerpt Jev already judged is reused across runs (each hook run is a
// fresh process, so the cache is on disk in production; in-memory here), and still logged, with cached: true.
public sealed class TraitVerdictCacheTests
{
    private static readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task A_second_check_of_the_same_diff_reuses_the_verdict_without_asking_jev()
    {
        using var repo = new TraitRepo();
        using var harness = new CheckTraitsHarness(repo, CheckTraitsHarness.Violating());

        var first = await harness.Service.Handle(A.CheckTraitsRequest().Edit("src/app.ts").Build(), _ct);
        var second = await harness.Service.Handle(A.CheckTraitsRequest().Edit("src/app.ts").Build(), _ct);

        _ = Assert.Single(harness.Jev.Asked);
        Assert.Equal(first.Value.Findings, second.Value.Findings);
        Assert.False(second.Value.JevUnavailable);
        var assessments = harness.Log.Entries.Where(entry => entry["event"]?.GetValue<string>() == "assessment").ToList();
        Assert.Equal([false, true], assessments.Select(entry => entry["cached"]!.GetValue<bool>()));
        Assert.All(assessments, entry => Assert.Matches("^[0-9a-f]{16}$", entry["hash"]!.GetValue<string>()));
        Assert.Equal(assessments[0]["hash"]!.GetValue<string>(), assessments[1]["hash"]!.GetValue<string>());
        Assert.Contains(harness.Log.Entries, entry => entry["event"]?.GetValue<string>() == "verdict-cache" && entry["hits"]!.GetValue<int>() == 1 && entry["misses"]!.GetValue<int>() == 0);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_changed_excerpt_is_asked_again()
    {
        using var repo = new TraitRepo();
        using var harness = new CheckTraitsHarness(repo, CheckTraitsHarness.Violating());

        _ = await harness.Service.Handle(A.CheckTraitsRequest().Edit("src/app.ts").Build(), _ct);
        repo.Write("src/app.ts", "export const a = 1;\n// TODO: remove soon\n");
        _ = await harness.Service.Handle(A.CheckTraitsRequest().Edit("src/app.ts").Build(), _ct);

        Assert.Equal(2, harness.Jev.Asked.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_changed_trait_file_is_asked_again()
    {
        using var repo = new TraitRepo();
        using var harness = new CheckTraitsHarness(repo, CheckTraitsHarness.Violating());

        _ = await harness.Service.Handle(A.CheckTraitsRequest().Edit("src/app.ts").Build(), _ct);
        repo.Write(".agents/traits/no-todo.yaml", TraitRepo.TraitText + "\n# reworded\n");
        _ = await harness.Service.Handle(A.CheckTraitsRequest().Edit("src/app.ts").Build(), _ct);

        Assert.Equal(2, harness.Jev.Asked.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_call_is_not_cached()
    {
        using var repo = new TraitRepo();
        var cache = new InMemoryTraitVerdictCache();
        using (var down = new CheckTraitsHarness(repo, FakeBatchClassifier.Unavailable(), cache: cache))
        {
            var failed = await down.Service.Handle(A.CheckTraitsRequest().Edit("src/app.ts").Build(), _ct);
            Assert.True(failed.Value.JevUnavailable);
        }

        using var up = new CheckTraitsHarness(repo, CheckTraitsHarness.Violating(), cache: cache);
        var result = await up.Service.Handle(A.CheckTraitsRequest().Edit("src/app.ts").Build(), _ct);

        _ = Assert.Single(up.Jev.Asked);
        _ = Assert.Single(result.Value.Findings);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_jev_excluded_a_cached_verdict_is_not_used()
    {
        using var repo = new TraitRepo();
        using var harness = new CheckTraitsHarness(repo, CheckTraitsHarness.Violating());
        _ = await harness.Service.Handle(A.CheckTraitsRequest().Edit("src/app.ts").Build(), _ct);

        var result = await harness.Service.Handle(A.CheckTraitsRequest().Edit("src/app.ts").Allowing(ProviderName.Rules).Build(), _ct);

        Assert.True(result.Value.JevUnavailable);
        Assert.Equal("excluded", result.Value.Reason);
        Assert.Empty(result.Value.Findings);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_source_hash_ignores_where_the_hunk_sits_but_not_what_it_says()
    {
        var at10 = new DiffSource("src/app.ts", 10, 12, "@@ -9,2 +10,3 @@ fn\n+// TODO\n");
        var at40 = new DiffSource("src/app.ts", 40, 42, "@@ -39,2 +40,3 @@ fn\n+// TODO\n");
        var other = new DiffSource("src/app.ts", 10, 12, "@@ -9,2 +10,3 @@ fn\n+// FIXME\n");

        Assert.Equal(TraitVerdictKeys.SourceHash(at10), TraitVerdictKeys.SourceHash(at40), StringComparer.Ordinal);
        Assert.NotEqual(TraitVerdictKeys.SourceHash(at10), TraitVerdictKeys.SourceHash(other), StringComparer.Ordinal);
    }
}
