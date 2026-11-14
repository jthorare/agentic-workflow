using Aw.Sindri.Core;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ProfileLive;

public sealed class SetupDeriverTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task The_Profile_step_has_no_approval_states()
    {
        foreach (var presence in Enum.GetValues<ProfilePresence>())   // whatever states the fact can still take
        {
            var facts = new InMemorySetupFacts { RepoAnswer = InMemorySetupFacts.OneRepo, ProfileAnswer = new ProfileFact(presence) };

            var steps = await new SetupDeriver(facts).Derive(TestContext.Current.CancellationToken);

            var profile = steps.Single(static s => s.Id == SetupStepId.Profile);
            Assert.True(profile.State is SetupState.Done or SetupState.Todo, $"{presence}: {profile.State}");
            Assert.Null(profile.Action);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unreadable_profile_names_the_fix_on_its_failed_steps()
    {
        const string fix = "The profile file is there but can't be read; check its permissions.";
        var facts = new InMemorySetupFacts { RepoAnswer = SindriErrors.ProfileInvalid.With(fix), ProfileAnswer = SindriErrors.ProfileInvalid.With(fix) };

        var steps = await new SetupDeriver(facts).Derive(TestContext.Current.CancellationToken);

        Assert.All(steps.Where(static s => s.Id is SetupStepId.Repo or SetupStepId.Profile), s =>
        {
            Assert.Equal(SetupState.Failed, s.State);
            Assert.Equal(fix, s.Detail);
            Assert.DoesNotContain("SND-", s.Why + s.Detail, StringComparison.Ordinal);
        });
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_read_outside_the_profile_keeps_its_message_off_the_page()
    {
        var facts = new InMemorySetupFacts { MapAnswer = SindriErrors.LedgerUnavailable.With("/home/thor/.agentic-workflow/sindri-app/views.db: disk I/O error") };

        var steps = await new SetupDeriver(facts).Derive(TestContext.Current.CancellationToken);

        var map = steps.Single(static s => s.Id == SetupStepId.Map);
        Assert.Equal(SetupState.Failed, map.State);
        Assert.Null(map.Detail);
    }
}
