using Aw.Context.Core.Audience;
using Aw.Context.Core.Engine;
using Aw.Context.Testing.Audience;

namespace Aw.Context.Tests.Ports;

[Trait("Lane", "unit")]
public sealed class ProfileAudiencePolicyContractTests : AudiencePolicyContract<ProfileAudiencePolicy>
{
    protected override ProfileAudiencePolicy Create() =>
        new(new ContextSettings { GitHub = new GitHubSource { Repos = ["acme/split-panes"] }, LinearTeams = ["team-a"] }, [Repo]);
}
