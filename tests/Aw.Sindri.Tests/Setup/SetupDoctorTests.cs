using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.RunDoctor;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Setup;

/// <summary>Doctor's 'setup' check: ok when every step is done or skipped; otherwise a warning that names the steps still open, with the owner's next move.</summary>
public sealed class SetupDoctorTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<DoctorCheck> Run(InMemorySetupFacts facts) => (await All(facts)).Single(static c => c.Name == SetupDoctor.Name);

    private static async Task<IReadOnlyList<DoctorCheck>> All(InMemorySetupFacts facts, FakeRecipeBuild? build = null) => await new SetupDoctor(facts, build).Run(Token);

    [Fact, Trait("Lane", "unit")]
    public async Task Every_step_done_is_ok()
    {
        var check = await Run(InMemorySetupFacts.AllDone());

        Assert.Equal(("setup", CheckState.Ok), (check.Name, check.State));
        Assert.Null(check.Fix);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_skipped_onyx_still_counts_as_finished()
    {
        var facts = InMemorySetupFacts.AllDone();
        facts.OnyxAnswer = new Core.Setup.OnyxFact(Core.Onyx.OnyxState.NotSetUp, Unreachable: false, BlockedByCode: null) { Deferred = true };

        Assert.Equal(CheckState.Ok, (await Run(facts)).State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_fresh_box_warns_and_names_the_first_open_step_and_its_move()
    {
        var check = await Run(new InMemorySetupFacts());

        Assert.Equal(CheckState.Warn, check.State);
        Assert.Contains("repo", check.Detail, StringComparison.Ordinal);
        Assert.Equal("sindri repo add <path>", check.Fix);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_step_that_cannot_be_read_is_a_warning_not_a_throw()
    {
        var facts = InMemorySetupFacts.AllDone();
        facts.OnyxAnswer = Aw.Sindri.Core.SindriErrors.ApiInternal.With("boom");

        var check = await Run(facts);

        Assert.Equal(CheckState.Warn, check.State);
        Assert.Contains("onyx", check.Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_box_with_a_repo_gets_a_warning_that_no_secret_check_hook_is_installed()
    {
        var check = (await All(InMemorySetupFacts.AllDone())).Single(static c => c.Name == "setup:repo-hook");

        Assert.Equal(CheckState.Warn, check.State);
        Assert.Contains("No secret-check hook yet", check.Detail, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(check.Fix));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_box_without_a_repo_has_no_hook_row() =>
        Assert.DoesNotContain(await All(new InMemorySetupFacts()), static c => c.Name == "setup:repo-hook");

    [Fact, Trait("Lane", "unit")]
    public async Task An_embedded_recipe_set_that_differs_from_the_checkouts_head_warns_with_both_hashes()
    {
        var check = (await All(InMemorySetupFacts.AllDone(), new FakeRecipeBuild(new RecipeTrees("aaaa111", "bbbb222")))).Single(static c => c.Name == "setup:recipes-tree");

        Assert.Equal(CheckState.Warn, check.State);
        Assert.Contains("aaaa111", check.Detail, StringComparison.Ordinal);
        Assert.Contains("bbbb222", check.Detail, StringComparison.Ordinal);
        Assert.Contains("sindri-app --rebuild", check.Fix, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_embedded_recipe_set_that_matches_head_is_ok()
    {
        var check = (await All(InMemorySetupFacts.AllDone(), new FakeRecipeBuild(new RecipeTrees("aaaa111", "aaaa111")))).Single(static c => c.Name == "setup:recipes-tree");

        Assert.Equal(CheckState.Ok, check.State);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Without_a_build_stamp_or_a_checkout_there_is_no_recipes_row(bool stampOnly)
    {
        var build = new FakeRecipeBuild(stampOnly ? new RecipeTrees("aaaa111", Head: null) : null);

        Assert.DoesNotContain(await All(InMemorySetupFacts.AllDone(), build), static c => c.Name == "setup:recipes-tree");
    }
}
