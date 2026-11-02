using Aw.Context.Core.Engine;
using Aw.Context.Core.Records;
using Aw.Context.Testing.Audience;

namespace Aw.Context.Tests.Ports;

[Trait("Lane", "unit")]
public sealed class FakeAudiencePolicyContractTests : AudiencePolicyContract<FakeAudiencePolicy>
{
    protected override FakeAudiencePolicy Create() => new(new Dictionary<ContextRepoName, IReadOnlyList<AudienceKey>> { [Repo] = [OwnRepo, Team] });
}
