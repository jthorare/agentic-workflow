using Aw.Judge.Testing;

namespace Aw.Judge.Tests.CheckTraits;

// A trait's optional trigger: a regex the excerpt must match before the trait is asked about it. No trigger, or an
// invalid one, judges the trait as before.
public sealed class TraitTriggerTests
{
    private static readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task A_trigger_the_excerpt_does_not_match_skips_the_trait_without_asking_jev()
    {
        using var repo = new TraitRepo();
        repo.Write(".agents/traits/no-todo.yaml", TraitRepo.TraitText + "\ntrigger: 'FIXME'\n");
        using var harness = new CheckTraitsHarness(repo, CheckTraitsHarness.Violating());

        var result = await harness.Service.Handle(A.CheckTraitsRequest().Edit("src/app.ts").Build(), _ct);

        Assert.Empty(result.Value.Findings);
        Assert.False(result.Value.JevUnavailable);
        Assert.Empty(harness.Jev.Asked);
        var skipped = Assert.Single(harness.Log.Entries, entry => entry["event"]?.GetValue<string>() == "trigger-skipped");
        Assert.Equal(1, skipped["count"]!.GetValue<int>());
        Assert.Equal(1, skipped["traits"]!["no-todo"]!.GetValue<int>());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_trigger_the_excerpt_matches_asks_jev()
    {
        using var repo = new TraitRepo();
        repo.Write(".agents/traits/no-todo.yaml", TraitRepo.TraitText + "\ntrigger: '\\bTODO\\b'\n");
        using var harness = new CheckTraitsHarness(repo, CheckTraitsHarness.Violating());

        var result = await harness.Service.Handle(A.CheckTraitsRequest().Edit("src/app.ts").Build(), _ct);

        _ = Assert.Single(result.Value.Findings);
        _ = Assert.Single(harness.Jev.Asked);
        Assert.DoesNotContain(harness.Log.Entries, entry => entry["event"]?.GetValue<string>() == "trigger-skipped");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_invalid_trigger_is_logged_and_the_trait_is_judged_as_if_it_had_none()
    {
        using var repo = new TraitRepo();
        repo.Write(".agents/traits/no-todo.yaml", TraitRepo.TraitText + "\ntrigger: '(['\n");
        using var harness = new CheckTraitsHarness(repo, CheckTraitsHarness.Violating());

        var result = await harness.Service.Handle(A.CheckTraitsRequest().Edit("src/app.ts").Build(), _ct);

        Assert.True(result.IsSuccess);
        _ = Assert.Single(result.Value.Findings);
        _ = Assert.Single(harness.Jev.Asked);
        var invalid = Assert.Single(harness.Log.Entries, entry => entry["event"]?.GetValue<string>() == "trait-trigger-invalid");
        Assert.Equal("no-todo", invalid["trait"]!.GetValue<string>());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_trait_without_a_trigger_is_asked_as_before()
    {
        using var repo = new TraitRepo();
        using var harness = new CheckTraitsHarness(repo, CheckTraitsHarness.Violating());

        var result = await harness.Service.Handle(A.CheckTraitsRequest().Edit("src/app.ts").Build(), _ct);

        _ = Assert.Single(result.Value.Findings);
        _ = Assert.Single(harness.Jev.Asked);
        Assert.DoesNotContain(harness.Log.Entries, entry => entry["event"]?.GetValue<string>() is { } name && (string.Equals(name, "trigger-skipped", StringComparison.Ordinal) || string.Equals(name, "trait-trigger-invalid", StringComparison.Ordinal)));
    }
}
