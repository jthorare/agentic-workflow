using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Records;

namespace Aw.Context.Tests.Ports;

[Trait("Lane", "unit")]
public sealed class FakeLinearLookupStoreContractTests : LinearLookupStoreContract<FakeLinearLookupStore>
{
    protected override FakeLinearLookupStore Create() => new();

    protected override LinearTeamLookups? Read(FakeLinearLookupStore store, ExternalId teamId) => store.Team(teamId);
}
