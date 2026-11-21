using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.RunDoctor;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Setup;

/// <summary>Doctor's 'setup' check: ok when every step is done; otherwise a warning that names the steps still open, with the owner's next move.</summary>
public sealed class SetupDoctorTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<DoctorCheck> Run(InMemorySetupFacts facts) => (await All(facts)).Single(static c => c.Name == SetupDoctor.Name);

    private static async Task<IReadOnlyList<DoctorCheck>> All(InMemorySetupFacts facts) => await new SetupDoctor(facts).Run(Token);

    [Fact, Trait("Lane", "unit")]
    public async Task Every_step_done_is_ok()
    {
        var check = await Run(InMemorySetupFacts.AllDone());

        Assert.Equal(("setup", CheckState.Ok), (check.Name, check.State));
        Assert.Null(check.Fix);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_profile_that_names_no_source_leaves_the_sources_step_open_with_the_connect_command()
    {
        var facts = InMemorySetupFacts.AllDone();
        facts.SourcesAnswer = new SourcesFact(0);

        var check = await Run(facts);

        Assert.Equal(CheckState.Warn, check.State);
        Assert.Contains("sources (todo)", check.Detail, StringComparison.Ordinal);
        Assert.Equal("sindri setup", check.Fix);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_fresh_box_warns_names_every_open_step_and_the_move_of_the_first()
    {
        var check = await Run(new InMemorySetupFacts());

        Assert.Equal(CheckState.Warn, check.State);
        Assert.Contains("repo (needs-you)", check.Detail, StringComparison.Ordinal);
        Assert.Equal("sindri setup", check.Fix);   // the accounts step is step 2 now, so it is the first open one on a fresh box
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_step_that_cannot_be_read_is_a_warning_not_a_throw()
    {
        var facts = InMemorySetupFacts.AllDone();
        facts.SourcesAnswer = Core.SindriErrors.ApiInternal.With("boom");

        var check = await Run(facts);

        Assert.Equal(CheckState.Warn, check.State);
        Assert.Contains("sources", check.Detail, StringComparison.Ordinal);
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
}
