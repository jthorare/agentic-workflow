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
}
